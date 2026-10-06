using System.Buffers.Binary;
using System.Text.Json;
using ZeldaCompanion;

static class ShuffleTests {
    public static async Task Run(string assets,string output) {
        assets=Path.GetFullPath(assets);output=Path.GetFullPath(output);Directory.CreateDirectory(output);
        var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception("FAILED "+message);checks.Add(message);Console.WriteLine("PASS "+message);}
        void Reject(Action work,string message){bool refused=false;try{work();}catch(InvalidDataException){refused=true;}Check(refused,message);}
        byte[] original=File.ReadAllBytes(assets);string originalHash=TreasureShuffle.Hash(original);var table=TreasureShuffle.ChestTable(original);
        // Independent protected-byte mask from the native file table, never from
        // the generator's reported eligible locations. Only known cash rewards
        // and explicitly enabled optional heart pieces may differ.
        var cash=new HashSet<byte>{0x34,0x35,0x36,0x40,0x41,0x46};
        for(int n=0;n<1000;n++){
            var options=new ShuffleOptions(n%2==0?"Balanced":"Surprise",(n/2)%2==1);var result=TreasureShuffle.GenerateBytes(original,n.ToString(),options);var allowed=new HashSet<int>();var before=new List<byte>();var after=new List<byte>();
            for(int i=0;i<168;i++){int p=table.Offset+i*3;bool big=(BinaryPrimitives.ReadUInt16LittleEndian(original.AsSpan(p))&0x8000)!=0;byte item=original[p+2];if(!big&&(cash.Contains(item)||(options.HeartPieces&&item==0x17))){allowed.Add(p+2);before.Add(item);after.Add(result.Assets[p+2]);}}
            if(original.Length!=result.Assets.Length||result.Eligible!=allowed.Count||!before.Order().SequenceEqual(after.Order()))throw new Exception("Inventory or format changed at seed "+n);
            for(int p=0;p<original.Length;p++)if(original[p]!=result.Assets[p]&&!allowed.Contains(p))throw new Exception($"Protected byte changed at seed {n}, offset {p}");
            if(options.Style=="Balanced")foreach(int p in allowed){bool beforeLarge=original[p] is 0x40 or 0x41 or 0x46,afterLarge=result.Assets[p] is 0x40 or 0x41 or 0x46;if(beforeLarge!=afterLarge)throw new Exception("Large reward left its tier");}
            if(result.Changed==0)throw new Exception("Seed made no changes: "+n);
        }
        Check(true,"1000 seeds across all four style/heart combinations preserve every protected byte, exact reward totals and balanced tiers");
        var a=TreasureShuffle.GenerateBytes(original,"Hyrule with friends",new("Surprise",true));var b=TreasureShuffle.GenerateBytes(original,"Hyrule with friends",new("Surprise",true));Check(a.Assets.SequenceEqual(b.Assets)&&a.Recipe==b.Recipe,"seed generation is exactly reproducible");
        Check(a.Recipe.Id!=TreasureShuffle.GenerateBytes(original,"Hyrule with friends",new("Balanced",true)).Recipe.Id,"options are part of seed and save identity");
        Check(TreasureShuffle.GenerateBytes(original,"  café  ",new()).Recipe==TreasureShuffle.GenerateBytes(original,"cafe\u0301",new()).Recipe,"seed normalization is stable and explicitly canonical");
        var bad=(byte[])original.Clone();bad[table.Offset+2]^=1;Reject(()=>TreasureShuffle.GenerateBytes(bad,"a",new()),"modified or unknown base assets cannot be randomized");Reject(()=>TreasureShuffle.GenerateBytes(original,"",new()),"blank seeds are refused");Reject(()=>TreasureShuffle.GenerateBytes(original,"a\nb",new()),"control characters are refused");Reject(()=>TreasureShuffle.GenerateBytes(original,"a",new("Chaos")),"unsupported modes are refused");
        string data=Path.Combine(output,"UserData");var library=new TreasureShuffle(data,assets);var recipe=library.Generate("Hyrule with friends",new("Surprise",true));Check(library.Verify(recipe.Id).Recipe==recipe,"installed seed regenerates and verifies before play");
        string save=Path.Combine(library.SeedFolder(recipe.Id),"Game","saves","save0.sav");byte[] saveData=[1,2,3,4,5];File.WriteAllBytes(save,saveData);library.Generate(recipe.Seed,recipe.Options);Check(File.ReadAllBytes(save).SequenceEqual(saveData),"regenerating an existing seed preserves its saves");string backup=library.Backup(recipe.Id);Check(File.ReadAllBytes(Path.Combine(backup,"save0.sav")).SequenceEqual(saveData),"pre-launch save backup is byte-exact");
        string exported=Path.Combine(output,"friends.z3seed.json");library.Export(recipe.Id,exported);var other=new TreasureShuffle(Path.Combine(output,"OtherUserData"),assets);var imported=other.Import(exported);Check(imported==recipe&&File.ReadAllBytes(other.AssetPath(imported.Id)).SequenceEqual(a.Assets),"a friend can reproduce an exported recipe with locally generated assets");Check(new FileInfo(exported).Length<1024&&!File.ReadAllText(exported).Contains("Assets"),"export is a small asset-free recipe");
        File.WriteAllText(exported,JsonSerializer.Serialize(recipe with {Seed="tampered"}));Reject(()=>other.Import(exported),"tampered seed recipes are refused before installation");File.WriteAllText(exported,JsonSerializer.Serialize(recipe with {Version=0}));Reject(()=>other.Import(exported),"older or unknown seed versions are refused");File.WriteAllText(exported,JsonSerializer.Serialize(recipe with {Id="../../bad"}));Reject(()=>other.Import(exported),"path traversal in seed identity is refused");File.WriteAllBytes(exported,new byte[17000]);Reject(()=>other.Import(exported),"oversized recipes are refused");
        var modified=File.ReadAllBytes(library.AssetPath(recipe.Id));modified[table.Offset+2]^=1;File.WriteAllBytes(library.AssetPath(recipe.Id),modified);Reject(()=>library.Verify(recipe.Id),"changed seed asset packs are refused at launch");Check(File.ReadAllBytes(save).SequenceEqual(saveData),"refused seed verification preserves saves");File.WriteAllBytes(library.AssetPath(recipe.Id),a.Assets);
        foreach(var options in new[]{new ShuffleOptions("Balanced",false),new("Surprise",false),new("Balanced",true),new("Surprise",true)})library.Generate("Native test",options);
        // Actual relay matching uses the complete randomized asset hash.
        await using(var relay=new Relay("http://127.0.0.1:24829",_=>{})){await relay.Start();var clients=new List<Bridge>();try{string code="";for(int i=0;i<8;i++){var client=new Bridge(_=>{});clients.Add(client);await client.Connect("ws://127.0.0.1:24829/room",new Hello(i==0?"create":"join",code,"Seed player "+(i+1),Protocol.Build,recipe.OutputHash,new string('A',64),true));code=client.Welcome.Code;}Check(clients[7].Welcome.Slot==8,"eight peers with matching randomized game data can join");await clients[7].DisposeAsync();clients.RemoveAt(7);await Task.Delay(100);await using var wrong=new Bridge(_=>{});bool denied=false;try{await wrong.Connect("ws://127.0.0.1:24829/room",new Hello("join",code,"Wrong seed",Protocol.Build,originalHash,new string('A',64),true));}catch(IOException){denied=true;}Check(denied,"vanilla game data cannot join a randomized room");}finally{foreach(var c in clients)await c.DisposeAsync();}}
        Check(TreasureShuffle.HashFile(assets)==originalHash,"source game data remains byte-exact after all tests");
        File.WriteAllText(Path.Combine(output,"shuffle-results.json"),JsonSerializer.Serialize(new{seedsChecked=1000,optionCombinations=4,checks,recipe,eligible=a.Eligible,changed=a.Changed,fullPlaythroughTested=false},new JsonSerializerOptions{WriteIndented=true}));
    }
}
