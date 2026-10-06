using System.Diagnostics;
using System.Security.Cryptography;

namespace ZeldaCompanion;

internal sealed class RandomizerPage {
    readonly TreasureShuffle library=new(Install.Data,Path.Combine(Install.Game,"zelda3_assets.dat"));
    readonly TextBox seed=new(){MaxLength=80,AccessibleName="Treasure Shuffle seed"};
    readonly ComboBox style=Theme.Combo(310),selection=Theme.Combo(620);
    readonly CheckBox hearts=Theme.Check("Shuffle chest heart pieces with treasure");
    readonly Label details=Theme.Text("",11,Theme.Muted),feedback=Theme.Text("",10,Theme.Gold);
    readonly ActionButton fresh=Theme.Button("New seed",false,120),generate=Theme.Button("Generate seed",true,190),play=Theme.Button("Play selected seed",true,205),online=Theme.Button("Use for online",false,175);
    readonly ActionButton import=Theme.Button("Import recipe…",false,170),export=Theme.Button("Export recipe…",false,170),spoiler=Theme.Button("Spoiler log",false,145),saves=Theme.Button("Seed saves",false,145);
    bool enabled=true;
    public RandomizerPage(PageColumn page,Func<Func<Task>,Task> run,Func<ShuffleRecipe,Task> launch,Action<ShuffleRecipe> useOnline,Action refreshOnline,Action<string,string,bool> status,Action<string> log) {
        page.Add(Theme.Text("Another adventure",28,null,FontStyle.Bold),8);
        page.Add(Theme.Text("Treasure Shuffle · native seeds for solo or eight-player co-op.\nShuffle rupee chests and, optionally, chest heart pieces. The story route stays original.",11,Theme.Muted),20);
        page.Add(Theme.Text("Create a seed",15,null,FontStyle.Bold),10);
        seed.Text=RandomNumberGenerator.GetInt32(int.MaxValue).ToString();page.Add(Theme.Row(Theme.Field(seed,380),fresh),8);
        page.Add(Theme.Text("A number or phrase. Matching seed, options and game data give the same treasure.",10,Theme.Muted),14);
        style.Items.AddRange(["Balanced · keep large rewards together","Surprise · mix all eligible rewards"]);style.SelectedIndex=0;style.AccessibleName="Shuffle style";style.Width=430;page.Add(style,10);page.Add(hearts,6);
        page.Add(Theme.Text("Progression protection is always on. Bombs, arrows, equipment, all keys,\nmaps, compasses and big chests stay fixed. Pots, shops and farmable rewards stay original.",10,Theme.Muted),12);
        page.Add(generate,8);page.Add(feedback,20);page.Add(new Panel{Height=1,BackColor=Theme.Line},18);
        page.Add(Theme.Text("Your seed library",15,null,FontStyle.Bold),10);page.Add(selection,10);page.Add(details,12);page.Add(Theme.Row(import,export,spoiler,saves),20);
        page.Add(Theme.Text("Play the same seed with friends",14,null,FontStyle.Bold),8);
        page.Add(Theme.Text("Export a small recipe and send it to friends. They choose Import recipe with their\nown USA game data, then select that seed on Online. Different seeds cannot join.\nEvery seed has its own solo and online saves. Existing adventures are kept separate.",11,Theme.Muted),18);
        page.Add(Theme.Text("Looking for the full ALttP Randomizer?",14,null,FontStyle.Bold),8);
        var website=Theme.Button("About ALttPR",false,180);page.Add(website,8);
        page.Add(Theme.Text("ALttPR shuffles major equipment and can change entrances, goals and logic.\nIts patched Japanese ROMs and seed codes are not compatible with this native port.\nTreasure Shuffle is a smaller Companion generator with an unchanged story route.\nGeneration checks are not a claim that a full randomized playthrough was completed.",10,Theme.Muted),0);
        var view=(Panel)page.Parent!;view.AutoScroll=false;view.Controls.Remove(page);var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,BackColor=Theme.Canvas};scroll.Controls.Add(page);
        var actions=Theme.Row(play,online);actions.Dock=DockStyle.Bottom;actions.AutoSize=false;actions.Height=56;actions.Padding=new Padding(0,8,0,4);actions.WrapContents=false;view.Controls.Add(scroll);view.Controls.Add(actions);
        fresh.Click+=(_,_)=>seed.Text=RandomNumberGenerator.GetInt32(int.MaxValue).ToString();
        selection.SelectedIndexChanged+=(_,_)=>UpdateDetails();
        generate.Click+=async(_,_)=>await run(async()=>{var options=new ShuffleOptions(style.SelectedIndex==0?"Balanced":"Surprise",hearts.Checked);string text=seed.Text;var recipe=await Task.Run(()=>library.Generate(text,options));Refresh(recipe.Id);refreshOnline();feedback.Text="Seed ready. Play it below, or use it for an online room.";scroll.ScrollControlIntoView(details);status("Seed generated","Required progression and supply sources remain original. This seed has separate saves.",false);log("Generated Treasure Shuffle v1 seed identity "+recipe.Id);});
        play.Click+=async(_,_)=>await run(()=>launch(Selected()));
        online.Click+=(_,_)=>{try{var recipe=Selected();library.Verify(recipe.Id);useOnline(recipe);}catch(Exception e){status("Seed needs attention",e.Message,true);}};
        import.Click+=async(_,_)=>await run(async()=>{using var picker=new OpenFileDialog{Title="Import a Companion Treasure Shuffle recipe",Filter="Companion seed recipe (*.z3seed.json)|*.z3seed.json"};if(picker.ShowDialog(page.FindForm())!=DialogResult.OK)return;var recipe=await Task.Run(()=>library.Import(picker.FileName));seed.Text=recipe.Seed;style.SelectedIndex=recipe.Options.Style=="Balanced"?0:1;hearts.Checked=recipe.Options.HeartPieces;Refresh(recipe.Id);refreshOnline();feedback.Text="Recipe imported and reproduced locally. Its saves start separately.";status("Seed imported","Choose Play selected seed or Use for online.",false);});
        export.Click+=async(_,_)=>await run(()=>{var recipe=Selected();using var picker=new SaveFileDialog{Title="Export the recipe to share with friends",Filter="Companion seed recipe (*.z3seed.json)|*.z3seed.json",FileName="TreasureShuffle-"+recipe.Id+".z3seed.json"};if(picker.ShowDialog(page.FindForm())==DialogResult.OK){library.Export(recipe.Id,picker.FileName);feedback.Text="Recipe exported. It contains no game assets or saves.";status("Recipe exported","Friends import it in Randomizer using their own original USA game data.",false);}return Task.CompletedTask;});
        spoiler.Click+=(_,_)=>Open(Path.Combine(library.SeedFolder(Selected().Id),"spoiler.txt"));
        saves.Click+=(_,_)=>Open(Path.Combine(library.SeedFolder(Selected().Id),"Game","saves"));
        website.Click+=(_,_)=>Open("https://alttpr.com/en");Refresh();
        void Open(string path){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){status("Could not open that information",e.Message,true);}}
    }
    ShuffleRecipe Selected()=>(selection.SelectedItem as SeedItem)?.Recipe??throw new InvalidOperationException("Generate or import a seed first.");
    public void Refresh(string? id=null){id??=(selection.SelectedItem as SeedItem)?.Recipe.Id;selection.Items.Clear();selection.Items.AddRange(library.List().Select(r=>new SeedItem(r)).ToArray());if(selection.Items.Count>0){selection.SelectedIndex=Enumerable.Range(0,selection.Items.Count).FirstOrDefault(i=>((SeedItem)selection.Items[i]!).Recipe.Id==id);}UpdateDetails();}
    void UpdateDetails(){if(selection.SelectedItem is not SeedItem item)details.Text="No seeds yet. Generate one above, or import a friend's Companion recipe.";else details.Text=$"{item.Recipe.Options.Style} · Heart pieces {(item.Recipe.Options.HeartPieces?"shuffled":"original")} · {item.Recipe.Id}\nOwn solo saves and online campaigns. Game data is checked again before launch.";Enable(enabled);}
    public void Enable(bool value){enabled=value;seed.Enabled=style.Enabled=hearts.Enabled=fresh.Enabled=selection.Enabled=value;generate.Enabled=import.Enabled=value&&Install.Ready;play.Enabled=online.Enabled=export.Enabled=spoiler.Enabled=saves.Enabled=value&&Install.Ready&&selection.SelectedItem is SeedItem;}
    internal sealed record SeedItem(ShuffleRecipe Recipe){public override string ToString()=>$"{Recipe.Seed} · {Recipe.Options.Style}{(Recipe.Options.HeartPieces?" + hearts":"")} · {Recipe.Id[..8]}";}
}
