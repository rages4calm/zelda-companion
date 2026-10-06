using System.Text;

namespace ZeldaCompanion;

public sealed record ControlAction(string Group,string Label,string Key,int Index,string Keyboard,string Controller);

public sealed class ControlBindings
{
    public static readonly string[] PadButtons=["A","B","X","Y","Back","Guide","Start","L3","R3","Lb","Rb","DpadUp","DpadDown","DpadLeft","DpadRight","L2","R2"];
    public static readonly IReadOnlyList<ControlAction> Actions=BuildActions();
    readonly string file,original;
    readonly Dictionary<string,string[]> keyboard=new(StringComparer.OrdinalIgnoreCase),controller=new(StringComparer.OrdinalIgnoreCase);
    public ControlBindings(string file){this.file=file;original=File.ReadAllText(file);Load("KeyMap",keyboard);Load("GamepadMap",controller);}
    static IReadOnlyList<ControlAction> BuildActions(){
        var a=new List<ControlAction>();string[] labels=["Move up","Move down","Move left","Move right","Select / save menu","Start / inventory","Action / dash","Sword","Map","Equipped tool","L shoulder","R shoulder"];
        string[] keys=["Up","Down","Left","Right","Right Shift","Return","x","z","s","a","c","v"];
        string[] pads=["DpadUp","DpadDown","DpadLeft","DpadRight","Back","Start","B","A","Y","X","Lb","Rb"];
        for(int i=0;i<labels.Length;i++)a.Add(new("Game controls",labels[i],"Controls",i,keys[i],pads[i]));
        foreach(var x in new[]{("Fullscreen","Toggle fullscreen","Alt+Return"),("PauseDimmed","Pause (dim screen)","p"),("Pause","Pause (keep screen)","Shift+p"),("Turbo","Hold to speed up","Tab"),("Reset","Restart game","Ctrl+r"),("VolumeUp","Volume up","Shift+="),("VolumeDown","Volume down","Shift+-"),("WindowBigger","Larger game window","Ctrl+Up"),("WindowSmaller","Smaller game window","Ctrl+Down")})a.Add(new("Game shortcuts",x.Item2,x.Item1,0,x.Item3,""));
        foreach(var x in new[]{("Load","Load states","Load state"),("Save","Save states","Save state"),("Replay","Replay states","Replay state")})
            for(int i=0;i<20;i++)a.Add(new(x.Item2,$"{x.Item3} · slot {i+1}",x.Item1,i,i<10?(x.Item1=="Save"?"Shift+":x.Item1=="Replay"?"Ctrl+":"")+"F"+(i+1):"",""));
        foreach(var x in new[]{("StopReplay","Stop replay","l"),("ReplayTurbo","Fast replay","t"),("ClearKeyLog","Clear input replay history","k"),("DisplayPerf","Show performance","f"),("ToggleRenderer","Switch renderer","r"),("CheatLife","Refill health / magic","w"),("CheatKeys","Give dungeon key","o"),("CheatEquipment","Refill supplies","Shift+w"),("CheatWalkThroughWalls","Walk through walls","Ctrl+e")})a.Add(new("Extra shortcuts",x.Item2,x.Item1,0,x.Item3,""));
        return a;
    }
    void Load(string section,Dictionary<string,string[]> target){
        bool active=false;foreach(var line in original.Split('\n')){string s=line.Split('#')[0].Trim();if(s.StartsWith('[')){active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);continue;}int eq=s.IndexOf('=');if(active&&eq>=0)target[s[..eq].Trim()]=s[(eq+1)..].Split(',').Select(v=>v.Trim()).ToArray();}
        foreach(var group in Actions.GroupBy(a=>a.Key)){
            if(target.ContainsKey(group.Key))continue;
            target[group.Key]=group.Select(a=>section=="KeyMap"?a.Keyboard:a.Controller).ToArray();
        }
    }
    public string Get(ControlAction a,bool pad){var map=pad?controller:keyboard;return map.TryGetValue(a.Key,out var values)&&a.Index<values.Length?values[a.Index]:"";}
    void Set(ControlAction a,bool pad,string value){var map=pad?controller:keyboard;var values=map[a.Key];if(values.Length<=a.Index)Array.Resize(ref values,a.Index+1);values[a.Index]=value;map[a.Key]=values;}
    public ControlAction? Conflict(ControlAction a,bool pad,string value)=>string.IsNullOrWhiteSpace(value)?null:Actions.FirstOrDefault(b=>b!=a&&Normalize(Get(b,pad),pad)==Normalize(value,pad));
    public void Assign(ControlAction a,bool pad,string value,bool swap=false){ValidateBinding(value,pad);var other=Conflict(a,pad,value);if(other!=null&&!swap)throw new InvalidOperationException($"Already assigned to {other.Label}.");string old=Get(a,pad);if(other!=null)Set(other,pad,old);Set(a,pad,value);}
    public void Defaults(string? group=null){foreach(var a in Actions.Where(a=>group==null||a.Group==group)){Set(a,false,a.Keyboard);Set(a,true,a.Controller);}}
    public static string Normalize(string value,bool pad){
        if(value=="")return "";var p=value.Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(pad)return string.Join('+',p[..^1].Select(PadName).Order().Append(PadName(p[^1])));
        var k=KeyboardParts(value);return string.Join('+',k.Modifiers.Select(v=>v.ToLowerInvariant()).Order())+":"+ControllerInput.KeyCode(k.Key);
    }
    static string PadName(string value)=>value.ToLowerInvariant() switch{"l1"=>"lb","r1"=>"rb",var v=>v};
    static (List<string> Modifiers,string Key) KeyboardParts(string value){var mods=new List<string>();while(true){string? prefix=new[]{"Ctrl+","Alt+","Shift+"}.FirstOrDefault(p=>value.StartsWith(p,StringComparison.OrdinalIgnoreCase));if(prefix==null)break;mods.Add(prefix[..^1]);value=value[prefix.Length..];}return(mods,value);}
    public static void ValidateBinding(string value,bool pad){
        if(value=="")return;if(value.Contains(',')||value.Contains('#')||value.Contains('\n')||value.Contains('\r'))throw new ArgumentException("That input cannot be assigned. Choose another key or button.");
        if(pad){var parts=value.Split('+',StringSplitOptions.TrimEntries);if(parts.Any(p=>!PadButtons.Contains(p,StringComparer.OrdinalIgnoreCase)&&p.ToLowerInvariant() is not("l1" or "r1"))||parts.Select(PadName).Distinct().Count()!=parts.Length)throw new ArgumentException("Choose a supported controller button or combination.");}
        else{var k=KeyboardParts(value);if(k.Modifiers.Distinct().Count()!=k.Modifiers.Count||ControllerInput.KeyCode(k.Key)==0)throw new ArgumentException("That key was not recognized. Press another key.");}
    }
    public void Save(string backupFolder){
        foreach(bool pad in new[]{false,true}){var used=new Dictionary<string,string>();foreach(var a in Actions){string v=Get(a,pad);ValidateBinding(v,pad);if(v=="")continue;string n=Normalize(v,pad);if(used.TryGetValue(n,out var other))throw new InvalidOperationException($"{(pad?"Controller":"Keyboard")} {v} is assigned to both {other} and {a.Label}. Change one before saving.");used[n]=a.Label;}}
        if(File.ReadAllText(file)!=original)throw new IOException("Settings changed outside this editor. Close and reopen Controls before saving.");
        var lines=original.Replace("\r\n","\n").Split('\n').ToList();UpdateSection(lines,"KeyMap",keyboard);UpdateSection(lines,"GamepadMap",controller);
        Directory.CreateDirectory(backupFolder);string backup=Path.Combine(backupFolder,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-controls.ini");File.Copy(file,backup);
        string temp=file+".controls-"+Guid.NewGuid().ToString("N")+".tmp";
        try{File.WriteAllText(temp,string.Join(Environment.NewLine,lines),new UTF8Encoding(false));File.Move(temp,file,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static void UpdateSection(List<string> lines,string section,Dictionary<string,string[]> values){
        var pending=values.Where(v=>Actions.Any(a=>a.Key==v.Key)).ToDictionary(v=>v.Key,v=>string.Join(", ",v.Value),StringComparer.OrdinalIgnoreCase);
        bool active=false,found=false;int insertion=lines.Count;
        for(int i=0;i<lines.Count;i++){string s=lines[i].Split('#')[0].Trim();if(s.StartsWith('[')){if(active)insertion=i;active=s.Equals("["+section+"]",StringComparison.OrdinalIgnoreCase);found|=active;continue;}if(!active)continue;insertion=i+1;int eq=s.IndexOf('=');if(eq<0)continue;string key=s[..eq].Trim();if(pending.TryGetValue(key,out var value)){lines[i]=key+" = "+value;pending.Remove(key);}}
        if(!found){lines.Add("");lines.Add("["+section+"]");insertion=lines.Count;}
        lines.InsertRange(insertion,pending.Select(v=>v.Key+" = "+v.Value));
    }
}
