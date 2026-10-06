using System.Text.Json;

namespace ZeldaCompanion;

internal sealed class Preferences
{
    public int Look {get;set;}
    public string PlayerName {get;set;}="Player";
    public bool ShareTools {get;set;}=true;
    public bool SharedCombat {get;set;}
    public bool ShowNames {get;set;}=true;
    public string LastCampaignId {get;set;}="";
    static string PathName=>Path.Combine(Install.Data,"launcher-preferences.json");
    public static Preferences Load(){try{return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PathName))??new();}catch{return new();}}
    public void Save(){Directory.CreateDirectory(Install.Data);File.WriteAllText(PathName,JsonSerializer.Serialize(this));}
}
