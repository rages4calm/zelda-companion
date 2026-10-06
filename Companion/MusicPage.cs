using System.Diagnostics;

namespace ZeldaCompanion;

internal sealed class MusicPage
{
    sealed record Item(SoundtrackPack? Pack){public override string ToString()=>Pack?.Title??"Original SNES music";}
    readonly SoundtrackLibrary library=new(Install.Data);
    readonly ComboBox selection=Theme.Combo(620);
    readonly NumericUpDown volume=new(){Minimum=0,Maximum=100,Value=70,Width=95,Font=Theme.Font(),BackColor=Theme.Rail,ForeColor=Theme.Ink,AccessibleName="MSU music volume percent"};
    readonly CheckBox resume=Theme.Check("Resume overworld music after returning from a building",true);
    readonly CheckBox deluxe=Theme.Check("Use extended tracks (MSU Deluxe packs only)");
    readonly Label details=Theme.Text("",11,Theme.Muted),feedback=Theme.Text("",11,Theme.Muted);
    readonly Label deluxeHint=Theme.Text("Leave extended tracks off for ordinary packs. Turn it on only when\nthe pack's author says it uses MSU Deluxe.",10,Theme.Muted);
    readonly ActionButton importZip=Theme.Button("Import ZIP…",false,155),importFolder=Theme.Button("Import folder…",false,175),save=Theme.Button("Use this soundtrack",true,210);
    readonly ActionButton browse=Theme.Button("Find music packs",false,190),open=Theme.Button("Open music folder",false,190);
    readonly Func<Func<Task>,Task> run;
    readonly Func<CancellationToken> cancellation;
    readonly Action<string,string,bool> status;
    readonly Action<string> log;
    bool enabled=true,refreshing;
    public MusicPage(PageColumn page,Func<Func<Task>,Task> run,Func<CancellationToken> cancellation,Action<string,string,bool> status,Action<string> log){
        this.run=run;this.cancellation=cancellation;this.status=status;this.log=log;
        page.Add(Theme.Text("Choose your soundtrack",28,null,FontStyle.Bold),8);
        page.Add(Theme.Text("Keep the original music or use a fan-made MSU-1 soundtrack.\nGame sound effects keep playing. Each online player chooses their own music.",11,Theme.Muted),24);
        page.Add(Theme.Text("Soundtrack",15,null,FontStyle.Bold),10);page.Add(selection,10);page.Add(details,18);
        page.Add(Theme.Row(save,importZip,importFolder),20);page.Add(feedback,16);page.Add(Rule(),20);
        page.Add(Theme.Text("Music balance",16,null,FontStyle.Bold),12);page.Add(Theme.Row(Theme.Text("MSU music volume",11),volume,Theme.Text("%",11,Theme.Muted)),12);page.Add(resume,8);page.Add(deluxe,10);
        page.Add(deluxeHint,18);page.Add(Theme.Row(browse,open),20);
        page.Add(Theme.Text("Bring an A Link to the Past audio pack",14,null,FontStyle.Bold),8);
        page.Add(Theme.Text("Import a ZIP or an extracted folder containing numbered PCM or OPUZ tracks.\nFor .7z or .rar downloads, extract them first, then choose Import folder.\nThe launcher keeps its own copy. You can remove the download afterward.\nROM patches and .msu video files are not needed by this native port.",11,Theme.Muted),16);
        page.Add(Theme.Text("Music downloads stay in UserData/Soundtracks on this PC.\nShare the clean software ZIP; friends obtain their own soundtrack packs.",10,Theme.Muted),0);
        selection.SelectedIndexChanged+=(_,_)=>{if(!refreshing){UpdateDetails();feedback.Text="Choose Use this soundtrack to apply this selection on your next launch.";}};
        foreach(var c in new Control[]{volume,resume,deluxe}){if(c is NumericUpDown n)n.ValueChanged+=(_,_)=>Draft();else ((CheckBox)c).CheckedChanged+=(_,_)=>Draft();}
        importZip.Click+=async(_,_)=>await run(async()=>{using var picker=new OpenFileDialog{Title="Choose an A Link to the Past MSU-1 soundtrack ZIP",Filter="Soundtrack ZIP (*.zip)|*.zip"};if(picker.ShowDialog(page.FindForm())!=DialogResult.OK)return;await Import(()=>library.ImportZip(picker.FileName,Progress,cancellation()));});
        importFolder.Click+=async(_,_)=>await run(async()=>{using var picker=new FolderBrowserDialog{Description="Choose the folder containing one A Link to the Past soundtrack's PCM or OPUZ tracks",UseDescriptionForTitle=true};if(picker.ShowDialog(page.FindForm())!=DialogResult.OK)return;await Import(()=>library.ImportFolder(picker.SelectedPath,Progress,cancellation()));});
        save.Click+=async(_,_)=>await run(()=>{var item=(Item)selection.SelectedItem!;var choice=new SoundtrackChoice(item.Pack?.Id??"",(int)volume.Value,resume.Checked,deluxe.Checked);library.Save(choice,Path.Combine(Install.Game,"zelda3.ini"));string message=item.Pack==null?"Original SNES music will play on your next game launch.":$"{item.Pack.Title} will play on your next launch. Missing tracks use the original music.";feedback.Text="Saved. A settings backup was created.";status("Soundtrack saved",message,false);log("Saved personal soundtrack choice: "+item);return Task.CompletedTask;});
        browse.Click+=(_,_)=>Open("https://www.zeldix.net/t791-the-legend-of-zelda-a-link-to-the-past");open.Click+=(_,_)=>{Directory.CreateDirectory(library.Folder);Open(library.Folder);};
        SoundtrackChoice choice=new();try{choice=library.Choice();}catch(Exception e){feedback.Text=e.Message;}
        Refresh(choice.PackId);refreshing=true;volume.Value=Math.Clamp(choice.Volume,0,100);resume.Checked=choice.Resume;deluxe.Checked=choice.Deluxe;refreshing=false;UpdateDetails();
        if(feedback.Text=="")feedback.Text=library.Managed?"Your saved choice applies on the next game launch.":Install.GetIni("Sound","EnableMSU","false") is "false" or "0"?"Original SNES music is active. Import a pack to add upgraded music.":"Existing advanced MSU settings are kept until you save a selection here.";
    }
    static Control Rule()=>new Panel{Height=1,BackColor=Theme.Line,Dock=DockStyle.Top,Margin=Padding.Empty};
    void Draft(){if(!refreshing)feedback.Text="Unsaved music options. Use this soundtrack to apply them.";}
    async Task Import(Func<Task<SoundtrackPack>> work){try{var pack=await work();Refresh(pack.Id);feedback.Text=$"Imported {pack.Tracks.Length} tracks. Choose Use this soundtrack to enable them.";status("Soundtrack imported","Your music is copied into this launcher. Choose Use this soundtrack to enable it.",false);}catch(Exception e){feedback.Text=e is OperationCanceledException?"Import cancelled. Your existing soundtrack is unchanged.":e.Message;throw;}}
    void Progress(string message){feedback.Text=message;status("Importing soundtrack",message,false);}
    void Refresh(string id){refreshing=true;selection.Items.Clear();selection.Items.Add(new Item(null));selection.Items.AddRange(library.Packs().Select(p=>new Item(p)).ToArray());selection.SelectedIndex=Enumerable.Range(0,selection.Items.Count).FirstOrDefault(i=>((Item)selection.Items[i]!).Pack?.Id==id);refreshing=false;UpdateDetails();}
    void UpdateDetails(){var pack=(selection.SelectedItem as Item)?.Pack;if(pack==null)details.Text="The game's original soundtrack. No downloaded audio is required.";else{int[] missing=SoundtrackLibrary.MissingStandard(pack);details.Text=$"{pack.Tracks.Length} tracks · {pack.Bytes/1024/1024} MB · {pack.Format.ToUpperInvariant()} audio\n"+(missing.Length==0?"All 34 standard music tracks are present.":$"{missing.Length} standard tracks are absent; those use the original music.");}Enable(enabled);}
    public void Enable(bool value){enabled=value;selection.Enabled=importZip.Enabled=importFolder.Enabled=save.Enabled=value;bool enhanced=(selection.SelectedItem as Item)?.Pack!=null;volume.Enabled=resume.Enabled=deluxe.Enabled=value&&enhanced;resume.Visible=deluxe.Visible=deluxeHint.Visible=enhanced;}
    void Open(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){feedback.Text=e.Message;status("Could not open music information",e.Message,true);}}
}
