using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZeldaCompanion;

public sealed record ShuffleOptions(string Style = "Balanced", bool HeartPieces = false);
public sealed record ShuffleRecipe(int Version, string Seed, ShuffleOptions Options, string BaseHash, string Id, string OutputHash);
public sealed record ShuffleChest(int Index, int Room, int Slot, bool Big, byte Before, byte After);
public sealed record ShuffleResult(byte[] Assets, ShuffleRecipe Recipe, ShuffleChest[] Chests) {
    public int Eligible => Chests.Length;
    public int Changed => Chests.Count(c => c.Before != c.After);
}

// Independently authored native-data shuffle. Never applies SNES ROM patches.
public sealed class TreasureShuffle(string data, string baseAssets) {
    public const int Version = 1;
    public const string SupportedBase = "0FE2E4BD75D70F06FB9A74CD3A9CB336C838149B831B56E8792114A89292C793";
    static readonly JsonSerializerOptions Json = new() { WriteIndented=true, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
    static readonly HashSet<byte> Money = [0x34,0x35,0x36,0x40,0x41,0x46];
    public string Folder => Path.Combine(data,"Randomizer");
    public string SeedFolder(string id) { ValidateId(id); return Path.Combine(Folder,id); }
    public string AssetPath(string id) => Path.Combine(SeedFolder(id),"Game","zelda3_assets.dat");
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    public static string HashFile(string path) { using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)); }
    static void ValidateId(string id) { if(id.Length!=24||!id.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid seed identity. Generate or import the seed again."); }
    public static string NormalizeSeed(string seed) { seed=seed.Trim().Normalize(NormalizationForm.FormC);if(seed.Length is <1 or >80||seed.Any(char.IsControl))throw new InvalidDataException("Enter a seed of 1–80 letters, numbers or spaces.");return seed; }
    public static void ValidateOptions(ShuffleOptions options) { if(options.Style is not ("Balanced" or "Surprise"))throw new InvalidDataException("Unknown shuffle style. Choose Balanced or Surprise."); }
    public static (int Offset,int Length) ChestTable(byte[] assets) {
        if(assets.Length<88||!assets.AsSpan(0,16).SequenceEqual("Zelda3_v0     \n\0"u8))throw new InvalidDataException("Unsupported game-data format. Set up the original USA ROM first.");
        uint count=BinaryPrimitives.ReadUInt32LittleEndian(assets.AsSpan(80)),keys=BinaryPrimitives.ReadUInt32LittleEndian(assets.AsSpan(84));
        if(count!=165||keys>20000||88L+4*count+keys>assets.Length)throw new InvalidDataException("Invalid game-data header.");
        long offset=88L+4*count+keys;int chestOffset=0,chestLength=0;
        for(int i=0;i<count;i++){offset=(offset+3)&~3L;uint length=BinaryPrimitives.ReadUInt32LittleEndian(assets.AsSpan(88+i*4));if(offset+length>assets.Length)throw new InvalidDataException("Game data is truncated.");if(i==8){chestOffset=(int)offset;chestLength=(int)length;}offset+=length;}
        if(offset!=assets.Length||chestLength!=168*3)throw new InvalidDataException("This chest-table revision is unsupported.");
        return(chestOffset,chestLength);
    }
    public static ShuffleResult GenerateBytes(byte[] original,string seed,ShuffleOptions options) {
        seed=NormalizeSeed(seed);ValidateOptions(options);string baseHash=Hash(original);
        if(baseHash!=SupportedBase)throw new InvalidDataException("Treasure Shuffle supports the original USA English game data only. Custom or modified asset packs cannot be randomized. Choose your clean USA ROM on Play.");
        var table=ChestTable(original);var assets=(byte[])original.Clone();var eligible=new List<ShuffleChest>();var slots=new Dictionary<int,int>();
        for(int i=0;i<table.Length/3;i++){int p=table.Offset+3*i;ushort packed=BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(p));int room=packed&0x7fff;int slot=slots.GetValueOrDefault(room);slots[room]=slot+1;byte item=original[p+2];bool big=(packed&0x8000)!=0;
            // Only an explicit item allowlist; every other location is protected.
            if(!big&&(Money.Contains(item)||(options.HeartPieces&&item==0x17)))eligible.Add(new(i,room,slot,big,item,item));}
        string canonical=$"treasure-shuffle-v{Version}\n{baseHash}\n{options.Style}\n{options.HeartPieces}\n{seed}";
        var random=new StableRandom(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        foreach(var group in eligible.GroupBy(c=>options.Style=="Surprise"?0:Money.Contains(c.Before)&&c.Before is 0x40 or 0x41 or 0x46?1:0)){
            var locations=group.ToArray();var items=locations.Select(c=>c.Before).ToArray();for(int i=items.Length-1;i>0;i--){int j=random.Next(i+1);(items[i],items[j])=(items[j],items[i]);}
            for(int i=0;i<locations.Length;i++)assets[table.Offset+locations[i].Index*3+2]=items[i];}
        var chests=eligible.Select(c=>c with {After=assets[table.Offset+c.Index*3+2]}).ToArray();string id=Hash(Encoding.UTF8.GetBytes(canonical))[..24];
        return new(assets,new(Version,seed,options,baseHash,id,Hash(assets)),chests);
    }
    public ShuffleRecipe[] List() {
        if(!Directory.Exists(Folder))return [];
        return Directory.EnumerateDirectories(Folder).Where(d=>Path.GetFileName(d).Length==24&&Path.GetFileName(d).All(Uri.IsHexDigit)).Select(d=>{
            try{var r=ReadRecipe(Path.Combine(d,"seed.json"));return r.Id==Path.GetFileName(d)?r:null;}catch(IOException){return null;}catch(JsonException){return null;}catch(InvalidDataException){return null;}
        }).Where(r=>r!=null).Select(r=>r!).OrderBy(r=>r.Seed,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public ShuffleRecipe Generate(string seed,ShuffleOptions options) {
        var result=GenerateBytes(File.ReadAllBytes(baseAssets),seed,options);string dest=SeedFolder(result.Recipe.Id);
        if(Directory.Exists(dest)){Verify(result.Recipe.Id);return result.Recipe;}
        Directory.CreateDirectory(Folder);string staging=Path.Combine(Folder,".creating-"+Guid.NewGuid().ToString("N"));
        try{Directory.CreateDirectory(Path.Combine(staging,"Game","saves"));File.WriteAllBytes(Path.Combine(staging,"Game","zelda3_assets.dat"),result.Assets);File.WriteAllText(Path.Combine(staging,"seed.json"),JsonSerializer.Serialize(result.Recipe,Json));
            var spoiler=new StringBuilder($"Treasure Shuffle v{Version}\nSeed: {result.Recipe.Seed}\nStyle: {options.Style}; heart pieces: {options.HeartPieces}\nIdentity: {result.Recipe.Id}\n\n{result.Eligible} eligible chests; {result.Changed} changed. Required equipment, keys, maps, compasses and all big chests stay original.\n\n");
            foreach(var c in result.Chests)spoiler.AppendLine($"Room {c.Room} / chest {c.Slot+1}: {ItemName(c.Before)} -> {ItemName(c.After)}");
            File.WriteAllText(Path.Combine(staging,"spoiler.txt"),spoiler.ToString());Directory.Move(staging,dest);return result.Recipe;
        }finally{if(Directory.Exists(staging))Directory.Delete(staging,true);}
    }
    public ShuffleResult Verify(string id) {
        var recipe=ReadRecipe(Path.Combine(SeedFolder(id),"seed.json"));var generated=GenerateBytes(File.ReadAllBytes(baseAssets),recipe.Seed,recipe.Options);
        if(recipe!=generated.Recipe||recipe.Id!=id||HashFile(AssetPath(id))!=generated.Recipe.OutputHash)throw new InvalidDataException("This seed's data or recipe has changed. Its saves are preserved. Restore the original seed data from a backup before playing.");
        return generated;
    }
    public static ShuffleRecipe ReadRecipe(string path) {
        if(!File.Exists(path)||new FileInfo(path).Length>16384)throw new InvalidDataException("Choose a small Companion .z3seed.json recipe, not a ROM or ALttPR patch.");
        ShuffleRecipe recipe;try{recipe=JsonSerializer.Deserialize<ShuffleRecipe>(File.ReadAllText(path),Json)??throw new InvalidDataException("Empty seed recipe.");}catch(JsonException e){throw new InvalidDataException("Invalid seed recipe. Export it again from Zelda Companion.",e);}
        if(recipe.Version!=Version||recipe.Options==null||recipe.Seed==null||recipe.BaseHash!=SupportedBase)throw new InvalidDataException("Unsupported seed or game-data revision. Use a Treasure Shuffle v1 recipe for the original USA game.");
        NormalizeSeed(recipe.Seed);ValidateOptions(recipe.Options);ValidateId(recipe.Id??"");if(recipe.OutputHash==null||recipe.OutputHash.Length!=64||!recipe.OutputHash.All(Uri.IsHexDigit))throw new InvalidDataException("Invalid seed output hash.");return recipe;
    }
    public ShuffleRecipe Import(string file) {
        var recipe=ReadRecipe(file);var expected=GenerateBytes(File.ReadAllBytes(baseAssets),recipe.Seed,recipe.Options);
        if(recipe!=expected.Recipe)throw new InvalidDataException("The recipe identity does not match its seed and options. Ask your friend for a fresh exported recipe.");return Generate(recipe.Seed,recipe.Options);
    }
    public void Export(string id,string destination) {
        var r=Verify(id).Recipe;string full=Path.GetFullPath(destination),root=Path.GetFullPath(SeedFolder(id))+Path.DirectorySeparatorChar;
        if(full.StartsWith(root,StringComparison.OrdinalIgnoreCase)||full.Equals(Path.GetFullPath(baseAssets),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Export the recipe outside this seed's data and saves folder.");
        File.WriteAllText(full,JsonSerializer.Serialize(r,Json));
    }
    public string Backup(string id) {
        Verify(id);string source=Path.Combine(SeedFolder(id),"Game","saves"),target=Path.Combine(SeedFolder(id),"Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));Directory.CreateDirectory(target);
        if(Directory.Exists(source))foreach(var file in Directory.GetFiles(source)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("A seed save cannot be a file link.");File.Copy(file,Path.Combine(target,Path.GetFileName(file)));}
        return target;
    }
    public static string ItemName(byte item)=>item switch{0x17=>"Heart piece",0x28=>"3 bombs",0x34=>"1 rupee",0x35=>"5 rupees",0x36=>"20 rupees",0x40=>"100 rupees",0x41=>"50 rupees",0x43=>"1 arrow",0x44=>"10 arrows",0x46=>"300 rupees",_=>$"Protected item 0x{item:X2}"};
    sealed class StableRandom(byte[] key) {
        uint counter;byte[] buffer=[];int index;
        uint NextUInt(){if(index>=buffer.Length){byte[] input=new byte[36];key.CopyTo(input,0);BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(32),counter++);buffer=SHA256.HashData(input);index=0;}uint n=BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(index));index+=4;return n;}
        public int Next(int bound){uint b=(uint)bound,threshold=unchecked(0u-b)%b,n;do{n=NextUInt();}while(n<threshold);return(int)(n%b);}
    }
}
