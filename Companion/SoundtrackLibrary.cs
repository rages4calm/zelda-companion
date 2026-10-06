using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ZeldaCompanion;

public sealed record SoundtrackPack(string Id,string Title,string Format,int[] Tracks,long Bytes,Dictionary<string,string> Sha256,string Source);
public sealed record SoundtrackChoice(string PackId="",int Volume=70,bool Resume=true,bool Deluxe=false);

public sealed class SoundtrackLibrary
{
    const long FileLimit=512L*1024*1024, TotalLimit=4L*1024*1024*1024;
    readonly string data;
    public string Folder=>Path.Combine(data,"Soundtracks");
    string ChoiceFile=>Path.Combine(data,"soundtrack-settings.json");
    static readonly Regex IdPattern=new("^[a-f0-9]{20}$",RegexOptions.CultureInvariant);
    static readonly Regex TrackPattern=new(@"^(.*?)(\d{1,3})\.(pcm|opuz)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public SoundtrackLibrary(string dataFolder){data=Path.GetFullPath(dataFolder);}
    public bool Managed=>File.Exists(ChoiceFile);
    public SoundtrackChoice Choice(){if(!Managed)return new();try{return JsonSerializer.Deserialize<SoundtrackChoice>(File.ReadAllText(ChoiceFile))??throw new InvalidDataException();}catch(Exception e){throw new IOException("The saved soundtrack choice could not be read. Choose a soundtrack again and save it.",e);}}
    public SoundtrackPack[] Packs(){if(!Directory.Exists(Folder))return [];return Directory.GetDirectories(Folder).Where(p=>IdPattern.IsMatch(Path.GetFileName(p))).Select(p=>{try{var pack=JsonSerializer.Deserialize<SoundtrackPack>(File.ReadAllText(Path.Combine(p,"pack.json")));return pack!=null&&pack.Id==Path.GetFileName(p)&&pack.Format is "pcm" or "opuz"?pack:null;}catch{return null;}}).OfType<SoundtrackPack>().OrderBy(p=>p.Title,StringComparer.CurrentCultureIgnoreCase).ToArray();}
    string PackFolder(string id){if(!IdPattern.IsMatch(id))throw new InvalidDataException("Choose an installed soundtrack.");return Path.Combine(Folder,id);}
    void OwnedStage(string path){string full=Path.GetFullPath(path);if(Path.GetDirectoryName(full)!=Path.GetFullPath(Folder)||!Regex.IsMatch(Path.GetFileName(full),"^\\.import-[a-f0-9]{32}$"))throw new IOException("The temporary music folder is outside this library.");}
    public static int[] MissingStandard(SoundtrackPack pack)=>Enumerable.Range(1,35).Where(n=>n!=15&&!pack.Tracks.Contains(n)).ToArray();
    sealed record TrackSource(int Number,string Format,string Group,long Length,Func<Stream> Open);
    static TrackSource? Source(string name,long size,Func<Stream> open){var match=TrackPattern.Match(Path.GetFileName(name.Replace('\\','/')));if(!match.Success)return null;int n=int.Parse(match.Groups[2].Value);if(n>255)throw new InvalidDataException("Track numbers must be between 0 and 255.");string format=match.Groups[3].Value.ToLowerInvariant();string group=(Path.GetDirectoryName(name.Replace('\\','/'))??"")+"/"+match.Groups[1].Value.ToLowerInvariant()+"."+format;return new(n,format,group,size,open);}
    static void ValidateHeader(string path,string format){using var f=File.OpenRead(path);Span<byte> header=stackalloc byte[8];if(f.Read(header)!=8)throw new InvalidDataException("A soundtrack track is too short: "+Path.GetFileName(path));if(format=="pcm"){if(!header[..4].SequenceEqual("MSU1"u8)||(f.Length-8)%4!=0||f.Length<=8)throw new InvalidDataException("Invalid MSU-1 PCM audio: "+Path.GetFileName(path));uint loop=BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);if(loop>=(f.Length-8)/4)throw new InvalidDataException("The track's loop point is outside its audio: "+Path.GetFileName(path));}else{
        if(!header[..4].SequenceEqual("OPUZ"u8)||f.Length<20)throw new InvalidDataException("Invalid native OPUZ audio: "+Path.GetFileName(path));Span<byte> range=stackalloc byte[10];var offsets=new List<uint>();bool ended=false;
        for(int i=0;i<512;i++){if(f.Read(range)!=10)break;uint offset=BinaryPrimitives.ReadUInt32LittleEndian(range[..4]);uint samples=BinaryPrimitives.ReadUInt32LittleEndian(range[4..8]);ushort flags=BinaryPrimitives.ReadUInt16LittleEndian(range[8..]);if(offset<18||offset>=f.Length||(offset&0xf0000000)!=0||samples==0)throw new InvalidDataException("Invalid OPUZ audio range: "+Path.GetFileName(path));offsets.Add(offset);if((flags&0x8000)!=0){ended=true;break;}}
        if(!ended||offsets.Any(offset=>offset<f.Position))throw new InvalidDataException("Invalid OPUZ range table: "+Path.GetFileName(path));
    }}
    public async Task<SoundtrackPack> ImportZip(string file,Action<string> progress,CancellationToken cancel=default,string? title=null,string source=""){
        using var archive=ZipFile.OpenRead(file);if(archive.Entries.Count>4096)throw new InvalidDataException("This ZIP contains too many files. Import the extracted soundtrack folder instead.");
        var tracks=new List<TrackSource>();foreach(var entry in archive.Entries){cancel.ThrowIfCancellationRequested();string name=entry.FullName.Replace('\\','/');if(name.StartsWith('/')||name.Split('/').Contains("..")||name.Contains(':'))throw new InvalidDataException("This ZIP contains an unsafe path. Choose another soundtrack archive.");var track=Source(name,entry.Length,entry.Open);if(track!=null)tracks.Add(track);}
        return await Import(tracks,title??Path.GetFileNameWithoutExtension(file),source,progress,cancel);
    }
    public async Task<SoundtrackPack> ImportFolder(string folder,Action<string> progress,CancellationToken cancel=default,string? title=null,string source=""){
        var root=new DirectoryInfo(folder);if(!root.Exists||(root.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Choose an extracted soundtrack folder.");var tracks=new List<TrackSource>();var options=new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint,IgnoreInaccessible=false};int count=0;
        foreach(var file in root.EnumerateFiles("*",options)){cancel.ThrowIfCancellationRequested();if(++count>4096)throw new InvalidDataException("This folder contains too many files. Choose the folder containing the soundtrack tracks.");var track=Source(Path.GetRelativePath(folder,file.FullName),file.Length,()=>File.OpenRead(file.FullName));if(track!=null)tracks.Add(track);}
        return await Import(tracks,title??root.Name,source,progress,cancel);
    }
    async Task<SoundtrackPack> Import(List<TrackSource> tracks,string title,string source,Action<string> progress,CancellationToken cancel){
        if(tracks.Count==0)throw new InvalidDataException("No numbered .pcm or .opuz tracks were found. Choose an A Link to the Past MSU-1 pack. Extract .7z or .rar archives first, then Import folder.");
        if(tracks.Select(t=>t.Group).Distinct().Count()!=1)throw new InvalidDataException("This contains multiple track sets or formats. Extract it and choose one soundtrack's folder.");
        if(tracks.Count>256||tracks.GroupBy(t=>t.Number).Any(g=>g.Count()>1))throw new InvalidDataException("This soundtrack has duplicate track numbers. Choose a folder containing one pack.");
        if(tracks.Any(t=>t.Length<=8||t.Length>FileLimit)||tracks.Sum(t=>t.Length)>TotalLimit)throw new InvalidDataException("This pack exceeds the supported size (512 MB per track, 4 GB total).");
        Directory.CreateDirectory(Folder);string stage=Path.Combine(Folder,".import-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var hashes=new Dictionary<string,string>();long bytes=0;
        try{
            foreach(var track in tracks.OrderBy(t=>t.Number)){cancel.ThrowIfCancellationRequested();string name=$"track-{track.Number}.{track.Format}";progress($"Importing track {track.Number} ({hashes.Count+1} of {tracks.Count})…");string target=Path.Combine(stage,name);
                using(var input=track.Open())using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None,1024*1024,true)){var buffer=new byte[1024*1024];long copied=0;int read;while((read=await input.ReadAsync(buffer,cancel))>0){copied+=read;if(copied>FileLimit||bytes+copied>TotalLimit)throw new InvalidDataException("The audio exceeds the supported size.");await output.WriteAsync(buffer.AsMemory(0,read),cancel);}if(copied!=track.Length)throw new IOException("A soundtrack file changed or could not be read completely. Try importing it again.");bytes+=copied;}
                ValidateHeader(target,track.Format);await using var f=File.OpenRead(target);hashes[name]=Convert.ToHexString(await SHA256.HashDataAsync(f,cancel)).ToLowerInvariant();
            }
            string canonical=string.Join('\n',hashes.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>p.Key+"="+p.Value));string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..20].ToLowerInvariant();
            var pack=new SoundtrackPack(id,string.IsNullOrWhiteSpace(title)?"Imported soundtrack":title.Trim()[..Math.Min(title.Trim().Length,100)],tracks[0].Format,tracks.Select(t=>t.Number).Order().ToArray(),bytes,hashes,source);
            File.WriteAllText(Path.Combine(stage,"pack.json"),JsonSerializer.Serialize(pack,Json),new UTF8Encoding(false));cancel.ThrowIfCancellationRequested();string dest=PackFolder(id);OwnedStage(stage);
            if(!Directory.Exists(dest))Directory.Move(stage,dest);else{
                bool intact=Packs().Any(p=>p.Id==id);foreach(var h in hashes){string existing=Path.Combine(dest,h.Key);if(!File.Exists(existing)){intact=false;break;}await using var f=File.OpenRead(existing);if(Convert.ToHexString(await SHA256.HashDataAsync(f,cancel)).ToLowerInvariant()!=h.Value){intact=false;break;}}
                if(!intact){string previous=Path.Combine(Folder,".previous-"+id+"-"+Guid.NewGuid().ToString("N"));if(Path.GetDirectoryName(Path.GetFullPath(dest))!=Path.GetFullPath(Folder)||Path.GetDirectoryName(Path.GetFullPath(previous))!=Path.GetFullPath(Folder))throw new IOException("Music repair would leave the library.");cancel.ThrowIfCancellationRequested();Directory.Move(dest,previous);try{Directory.Move(stage,dest);}catch{if(!Directory.Exists(dest))Directory.Move(previous,dest);throw;}}
            }
            return Packs().Single(p=>p.Id==id);
        }finally{if(Directory.Exists(stage)){OwnedStage(stage);Directory.Delete(stage,true);}}
    }
    public SoundtrackPack? ValidateChoice(SoundtrackChoice choice){if(choice.Volume is <0 or >100)throw new InvalidDataException("Music volume must be between 0 and 100%.");if(choice.PackId=="")return null;var pack=Packs().SingleOrDefault(p=>p.Id==choice.PackId)??throw new IOException("The selected soundtrack is missing. Import it again or choose Original SNES music.");string folder=PackFolder(pack.Id);foreach(int n in pack.Tracks){string f=Path.Combine(folder,$"track-{n}.{pack.Format}");if(!File.Exists(f))throw new IOException($"Track {n} is missing. Import the pack again or choose Original SNES music.");ValidateHeader(f,pack.Format);}return pack;}
    public void Save(SoundtrackChoice choice,string gameIni){var pack=ValidateChoice(choice);if(!File.Exists(gameIni))throw new IOException("The game settings file is missing. Extract the complete launcher package again.");Directory.CreateDirectory(data);string backup=Path.Combine(data,"SettingsBackups");Directory.CreateDirectory(backup);string original=Path.Combine(backup,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff")+"-soundtrack.ini");File.Copy(gameIni,original);
        string temporary=ChoiceFile+"."+Guid.NewGuid().ToString("N")+".tmp";bool applied=false;try{File.WriteAllText(temporary,JsonSerializer.Serialize(choice,Json),new UTF8Encoding(false));Apply(gameIni,choice,pack);applied=true;File.Move(temporary,ChoiceFile,true);}catch{if(applied)File.Copy(original,gameIni,true);throw;}finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public void PrepareForLaunch(string gameDirectory,Action<string> log){if(!Managed)return;var choice=Choice();var pack=ValidateChoice(choice);Apply(Path.Combine(gameDirectory,"zelda3.ini"),choice,pack);if(pack!=null)log($"MSU soundtrack: {pack.Title}; {pack.Tracks.Length} {pack.Format.ToUpperInvariant()} tracks, {choice.Volume}% music volume.");}
    void Apply(string ini,SoundtrackChoice choice,SoundtrackPack? pack){string temporary=ini+"."+Guid.NewGuid().ToString("N")+".tmp";try{File.Copy(ini,temporary);Install.SetIni(temporary,"Sound","EnableMSU",pack==null?"false":choice.Deluxe?(pack.Format=="opuz"?"deluxe-opuz":"deluxe"):pack.Format=="opuz"?"opuz":"true");if(pack!=null){string prefix=Path.GetRelativePath(Path.GetDirectoryName(ini)!,PackFolder(pack.Id)).Replace('\\','/')+"/track-";Install.SetIni(temporary,"Sound","MSUPath",prefix);Install.SetIni(temporary,"Sound","AudioFreq",pack.Format=="pcm"?"44100":"48000");Install.SetIni(temporary,"Sound","AudioChannels","2");}Install.SetIni(temporary,"Sound","MSUVolume",choice.Volume+"%");Install.SetIni(temporary,"Sound","ResumeMSU",choice.Resume?"1":"0");File.Move(temporary,ini,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}}
}
