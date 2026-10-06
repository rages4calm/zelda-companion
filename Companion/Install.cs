using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ZeldaCompanion;

public static class Install
{
    public static string Root => AppContext.BaseDirectory;
    public static string Game => Path.Combine(Root,"Game");
    public static string Data => Path.Combine(Root,"UserData");
    public static bool Ready => File.Exists(Path.Combine(Game,"zelda3_assets.dat"));
    public static string GetIni(string section,string key,string fallback="") {
        string file=Path.Combine(Game,"zelda3.ini");if(!File.Exists(file))return fallback;
        bool active=false;
        foreach(var line in File.ReadLines(file)){
            string s=line.Split('#')[0].Trim();
            if(s.StartsWith('[')){active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);continue;}
            int eq=s.IndexOf('=');if(active&&eq>=0&&s[..eq].Trim().Equals(key,StringComparison.OrdinalIgnoreCase))return s[(eq+1)..].Trim();
        }
        return fallback;
    }
    public static void SetIni(string file,string section,string key,string value) {
        var lines=File.ReadAllLines(file).ToList(); bool active=false,found=false;
        for (int i=0;i<lines.Count;i++) {
            string s=lines[i].Trim();
            if (s.StartsWith('[')) active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);
            if (active && Regex.IsMatch(s,"^"+Regex.Escape(key)+"\\s*=",RegexOptions.IgnoreCase)) { lines[i]=$"{key} = {value}"; found=true; break; }
        }
        if (!found) lines.AddRange(["",$"[{section}]",$"{key} = {value}"]);
        File.WriteAllLines(file,lines,new UTF8Encoding(false));
    }
    public static string GameplayHash() {
        var lines=File.ReadAllLines(Path.Combine(Game,"zelda3.ini")); string section="";
        var values=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach (var line in lines) {
            string s=line.Split('#')[0].Trim(); if (s.StartsWith('[')) section=s.ToLowerInvariant();
            int eq=s.IndexOf('='); if (eq<0) continue;
            string key=s[..eq].Trim().ToLowerInvariant();
            if (section=="[features]" || (section=="[general]" && key is "extendedaspectratio" or "language")) values[section+key]=s[(eq+1)..].Trim().ToLowerInvariant();
        }
        string canonical=string.Join('\n',values.Select(v=>v.Key+"="+v.Value))+"\nengine="+Protocol.HashFile(Path.Combine(Game,"online.exe"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
    static string CampaignFolder(string? seedId){if(seedId!=null&&(seedId.Length!=24||!seedId.All(Uri.IsHexDigit)))throw new IOException("Invalid online seed.");return seedId==null?Path.Combine(Data,"Online"):Path.Combine(Data,"Online","Seeds",seedId);}
    public static (string Id,DateTime Saved)[] SavedCampaigns(string? seedId=null){
        string folder=CampaignFolder(seedId);if(!Directory.Exists(folder))return [];
        return Directory.EnumerateDirectories(folder).Where(d=>Path.GetFileName(d).Length==24&&Path.GetFileName(d).All(Uri.IsHexDigit)&&Directory.Exists(Path.Combine(d,"saves")))
            .Select(d=>(Id:Path.GetFileName(d),Files:Directory.GetFiles(Path.Combine(d,"saves")))).Where(x=>x.Files.Length>0)
            .Select(x=>(x.Id,Saved:x.Files.Max(File.GetLastWriteTimeUtc))).OrderByDescending(x=>x.Saved).ToArray();
    }
    public static string OnlineProfile(string relay,string code,string? savedId=null,string? seedId=null) {
        // One private campaign per relay/room. No implicit import of a solo save.
        string campaignFolder=CampaignFolder(seedId);
        var shuffle=new TreasureShuffle(Data,Path.Combine(Game,"zelda3_assets.dat"));if(seedId!=null)shuffle.Verify(seedId);
        if(savedId!=null&&(savedId.Length!=24||!savedId.All(Uri.IsHexDigit)||!Directory.Exists(Path.Combine(campaignFolder,savedId))))throw new IOException("That saved online campaign is no longer available. Choose a new campaign.");
        string id=savedId??Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(relay+"|"+code)))[..24];
        string path=Path.Combine(campaignFolder,id); Directory.CreateDirectory(path);
        foreach (string name in new[]{"online.exe","SDL2.dll"}) File.Copy(Path.Combine(Game,name),Path.Combine(path,name),true);
        File.Copy(seedId==null?Path.Combine(Game,"zelda3_assets.dat"):shuffle.AssetPath(seedId),Path.Combine(path,"zelda3_assets.dat"),true);
        CopyTree(Path.Combine(Game,"glsl-shaders"),Path.Combine(path,"glsl-shaders"));
        // The compatibility hash describes these exact settings; refresh them on every join.
        string profileIni=Path.Combine(path,"zelda3.ini");
        if (File.Exists(profileIni)) File.Copy(profileIni,profileIni+".previous",true);
        File.Copy(Path.Combine(Game,"zelda3.ini"),profileIni,true);
        Directory.CreateDirectory(Path.Combine(path,"saves"));
        string backup=Path.Combine(path,"Backups",DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
        CopyTree(Path.Combine(path,"saves"),backup);
        return path;
    }
    public static void CopyTree(string source,string dest) {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(source)) File.Copy(f,Path.Combine(dest,Path.GetFileName(f)),true);
        foreach (var d in Directory.GetDirectories(source)) CopyTree(d,Path.Combine(dest,Path.GetFileName(d)));
    }
    public static async Task Extract(string rom,Action<string> log,CancellationToken cancellation=default) {
        byte[] bytes=await File.ReadAllBytesAsync(rom);
        int start=bytes.Length%0x100000==512 ? 512 : 0;
        if (Convert.ToHexString(SHA1.HashData(bytes.AsSpan(start)))!="6D4F10A8B10E10DBE624CB23CF03B88BB8252973")
            throw new InvalidDataException("This build requires the original USA ROM. Patched ROMs and other regions are unsupported.");
        string tool=Path.Combine(Game,"assets");
        Directory.CreateDirectory(Path.Combine(tool,"sprites"));
        var info=new ProcessStartInfo(Path.Combine(tool,"python.exe")) { WorkingDirectory=tool,UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true,RedirectStandardInput=true };
        info.ArgumentList.Add("restool.py"); info.ArgumentList.Add("--extract-from-rom"); info.ArgumentList.Add("--rom"); info.ArgumentList.Add(rom);
        using var p=Process.Start(info) ?? throw new IOException("Could not start the asset extractor.");
        p.StandardInput.Close();
        async Task Pump(StreamReader s) { while (await s.ReadLineAsync() is {} line) log(line); }
        var outTask=Pump(p.StandardOutput); var errTask=Pump(p.StandardError);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation);timeout.CancelAfter(TimeSpan.FromMinutes(5));
        try { await p.WaitForExitAsync(timeout.Token); } catch(OperationCanceledException) { if(!p.HasExited)p.Kill(true);await p.WaitForExitAsync();if(cancellation.IsCancellationRequested)throw;throw new IOException("Asset extraction timed out; see the log."); }
        await Task.WhenAll(outTask,errTask);
        if (p.ExitCode!=0 || !Ready) throw new IOException("Asset extraction failed; see the log.");
        log("Game data built locally. The original ROM was not copied into this package.");
    }
}
