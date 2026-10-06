using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ZeldaCompanion;

static class RandomizerDiagnostics {
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(object form,string name)=>(T)form.GetType().GetField(name,Flags)!.GetValue(form)!;
    static void Pump(int ms){var watch=Stopwatch.StartNew();while(watch.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(10);}}
    static void Until(Func<bool> predicate,int timeout=20000){var watch=Stopwatch.StartNew();while(!predicate()){if(watch.ElapsedMilliseconds>timeout)throw new TimeoutException("UI test timed out");Pump(20);}}
    public static int FirstRun(string rom,string output){try{
        if(Install.Ready)throw new Exception("Fresh-package test must start without extracted game data.");
        string hash=TreasureShuffle.HashFile(rom);
        using(var form=new MainForm()){
            var page=Field<object>(form,"randomizer");
            if(Field<Button>(page,"generate").Enabled||Field<Button>(page,"import").Enabled)throw new Exception("First run must require ROM setup before seed generation/import.");
            var topics=Field<ComboBox>(form,"guideTopics");if(topics.Items.Count!=8||topics.Items[7]?.ToString()!="Randomizer & progression")throw new Exception("Randomizer guide must be discoverable.");
            topics.SelectedIndex=7;
        }
        Task.Run(()=>Install.Extract(rom,Console.WriteLine)).GetAwaiter().GetResult();
        if(TreasureShuffle.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat"))!=TreasureShuffle.SupportedBase)throw new Exception("Bundled extractor produced an unsupported asset revision.");
        if(TreasureShuffle.HashFile(rom)!=hash)throw new Exception("Original ROM changed during setup.");
        Console.WriteLine("PASS clean ZIP first-run setup uses bundled tools and produces the exact supported original assets");
        int result=Run(output,"",false);if(result!=0)return result;
        File.WriteAllText(Path.Combine(output,"first-run-results.json"),JsonSerializer.Serialize(new{bundledExtractor=true,originalRomUnchanged=true,generatedBaseHash=TreasureShuffle.SupportedBase,randomizerGuideTopics=8,actualNativeSeedLaunch=true},new JsonSerializerOptions{WriteIndented=true}));return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
    public static int Run(string output,string helper,bool capture=true){try{
        Directory.CreateDirectory(output);var checks=new List<string>();void Check(bool value,string text){if(!value)throw new Exception("FAILED "+text);checks.Add(text);Console.WriteLine("PASS "+text);}
        void Capture(Form form,string name){if(!capture)return;Pump(150);var info=new ProcessStartInfo("python"){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add(helper);info.ArgumentList.Add(form.Handle.ToInt64().ToString());info.ArgumentList.Add(Path.Combine(output,name+".png"));using var process=Process.Start(info)!;process.WaitForExit();Check(process.ExitCode==0,"captured owned window: "+name);}
        void Page(MainForm form,string id)=>typeof(MainForm).GetMethod("ShowPage",Flags)!.Invoke(form,[id]);
        string original=TreasureShuffle.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat"));string originalSave=Path.Combine(Install.Game,"saves","preserve-test.txt");Directory.CreateDirectory(Path.GetDirectoryName(originalSave)!);File.WriteAllText(originalSave,"original saves remain separate");
        var library=new TreasureShuffle(Install.Data,Path.Combine(Install.Game,"zelda3_assets.dat"));
        using var form=new MainForm();form.Show();Page(form,"randomizer");var page=Field<object>(form,"randomizer");Capture(form,"randomizer-empty");Check(!Field<Button>(page,"play").Enabled,"empty library disables play with visible setup guidance");
        Field<TextBox>(page,"seed").Text="Hyrule with friends";Field<ComboBox>(page,"style").SelectedIndex=1;Field<CheckBox>(page,"hearts").Checked=true;Field<Button>(page,"generate").PerformClick();Until(()=>library.List().Length==1&&!Field<bool>(form,"busy"));var recipe=library.List().Single();Check(recipe.Seed=="Hyrule with friends"&&recipe.Options==new ShuffleOptions("Surprise",true),"actual Generate seed button installs requested options");Capture(form,"randomizer-generated");
        form.ClientSize=new System.Drawing.Size(924,681);Capture(form,"randomizer-small");
        Field<Button>(page,"online").PerformClick();var seedSelector=Field<ComboBox>(form,"onlineSeed");Check(seedSelector.SelectedIndex==1,"Use for online selects the seed in the real Online form");Capture(form,"randomizer-online");
        string vanilla=Install.OnlineProfile("test-relay","test-code"),seeded=Install.OnlineProfile("test-relay","test-code",seedId:recipe.Id);Check(vanilla!=seeded&&seeded.Contains(Path.Combine("Seeds",recipe.Id)),"same room has separate vanilla and per-seed campaign namespaces");Check(TreasureShuffle.HashFile(Path.Combine(seeded,"zelda3_assets.dat"))==recipe.OutputHash,"online campaign receives exact selected randomized data");File.WriteAllText(Path.Combine(seeded,"saves","profile-test.txt"),"seeded progress");string savedId=Path.GetFileName(seeded);Install.OnlineProfile("test-relay","test-code",savedId,recipe.Id);Check(File.ReadAllText(Path.Combine(seeded,"saves","profile-test.txt"))=="seeded progress","continuing a seeded campaign preserves save files");Check(Install.SavedCampaigns().Length==0&&Install.SavedCampaigns(recipe.Id).Single().Id==savedId,"campaign selector cannot cross seed namespaces");
        seedSelector.SelectedIndex=0;Page(form,"randomizer");form.ClientSize=new System.Drawing.Size(1000,760);Field<Button>(page,"play").PerformClick();Until(()=>Field<Process?>(form,"game")!=null);var game=Field<Process>(form,"game");Pump(2600);Check(!game.HasExited&&game.StartInfo.WorkingDirectory.Contains(recipe.Id),"Play selected seed starts the actual native game from the seed's folder");Check(!Field<Button>(page,"generate").Enabled&&!Field<ComboBox>(form,"onlineSeed").Enabled,"running game locks seed changes and generation");
        game.CloseMainWindow();Until(()=>Field<Process?>(form,"game")==null);Check(Directory.Exists(Path.Combine(library.SeedFolder(recipe.Id),"Backups")),"native seed launch makes its own save backup");Check(TreasureShuffle.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat"))==original&&File.ReadAllText(originalSave)=="original saves remain separate","native randomized launch preserves original assets and solo saves");Capture(form,"randomizer-after-play");form.Close();Pump(100);
        File.WriteAllText(Path.Combine(output,"randomizer-ui-results.json"),JsonSerializer.Serialize(new{checks,recipe,nativeGameLaunched=true,fullPlaythroughTested=false},new JsonSerializerOptions{WriteIndented=true}));return 0;
    }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
