using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using ZeldaCompanion;

static class Program
{
    static readonly BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Get<T>(MainForm f,string name)=>(T)typeof(MainForm).GetField(name,Fields)!.GetValue(f)!;
    static readonly List<string> checks=[];
    static void Check(bool ok,string text){if(!ok)throw new Exception("FAILED "+text);checks.Add(text);Console.WriteLine("PASS "+text);}
    static async Task Until(Func<bool> condition,int ms=10000){var timer=Stopwatch.StartNew();while(!condition()){if(timer.ElapsedMilliseconds>ms)throw new TimeoutException("UI condition timed out");await Task.Delay(50);}}
    [STAThread] static int Main(string[] args) {
        ApplicationConfiguration.Initialize();
        if(args.Length==3&&args[0]=="--randomizer")return RandomizerDiagnostics.Run(args[1],args[2]);
        if(args.Length==3&&args[0]=="--randomizer-first-run")return RandomizerDiagnostics.FirstRun(args[1],args[2]);
        if(args.Length==3&&args[0]=="--coop-ui")return CoopDiagnostics.Run(args[1],args[2]);
        if(args.Length==3&&args[0]=="--controls")return ControlsDiagnostics.Run(args[1],args[2]);
        if(args.Length==4&&args[0]=="--music")return MusicDiagnostics.Run(args[1],args[2],args[3]);
        if(args.Length==4&&args[0]=="--music-functional")return MusicDiagnostics.Run(args[1],args[2],args[3],false);
        if(args.Length==6&&args[0]=="--import-msu")return MusicDiagnostics.ImportPersonal(args[1],args[2],args[3],args[4],args[5]);
        if(args.Length==3&&args[0]=="--prepare-msu"){new SoundtrackLibrary(args[1]).PrepareForLaunch(args[2],Console.WriteLine);return 0;}
        bool internetTest=args.Length>4&&args[4]=="--internet";
        Exception? failure=null;var oldClipboard=Clipboard.GetDataObject();
        using var form=new MainForm("http://127.0.0.1:24816");
        void Page(string id)=>typeof(MainForm).GetMethod("ShowPage",Fields)!.Invoke(form,[id]);
        async Task Capture(string title) {
            if(args.Length<3)return;await Task.Delay(120);
            var info=new ProcessStartInfo("python"){UseShellExecute=false,CreateNoWindow=true};
            info.ArgumentList.Add(args[3]);info.ArgumentList.Add(form.Handle.ToInt64().ToString());info.ArgumentList.Add(Path.Combine(args[2],title+".png"));
            using var capture=Process.Start(info)!;await capture.WaitForExitAsync();Check(capture.ExitCode==0,"captured actual window: "+title);
        }
        form.Shown+=async(_,_)=>{
            try {
                if(args.Length>0&&args[0]=="--preview-controls"){
                    Page("settings");await Capture("settings-controls");
                    Page("guide");Get<ComboBox>(form,"guideTopics").SelectedIndex=3;await Capture("controls-guide");
                    form.ClientSize=new System.Drawing.Size(924,681);await Capture("controls-guide-small");
                    return;
                }
                if(args.Length>0&&args[0]=="--preview"){
                    Page("online");await Capture("online-host");
                    Page("guide");Get<ComboBox>(form,"guideTopics").SelectedIndex=1;await Capture("internet-guide");
                    return;
                }
                Check(!Install.Ready,"fresh folder has no compiled Nintendo game data");
                Check(!Get<Button>(form,"solo").Enabled&&Get<Button>(form,"import").Enabled,"first launch points to ROM setup and disables launch");
                await Capture("first-run");
                Page("guide");Check(Get<ComboBox>(form,"guideTopics").Items.Count==8,"guide offers setup, online, limits, controls, troubleshooting, sharing, music and randomizer");
                await Capture("guide");Page("play");
                string invalid=Path.Combine(Install.Root,"invalid-test.sfc");File.WriteAllBytes(invalid,new byte[1024]);
                bool rejected=false;try{await Install.Extract(invalid,_=>{});}catch(InvalidDataException){rejected=true;}File.Delete(invalid);
                Check(rejected&&!Install.Ready,"incorrect ROM rejected before extraction");
                var timer=Stopwatch.StartNew();await Install.Extract(args[0],Console.WriteLine);
                Check(Install.Ready,"valid user ROM extracted in clean bundled Python runtime");
                string expected=Protocol.HashFile(args[1]);Check(Protocol.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat"))==expected,"fresh asset file matches installed game data exactly");
                Console.WriteLine("Extraction seconds: "+timer.Elapsed.TotalSeconds);
                Install.SetIni(Path.Combine(Install.Game,"zelda3.ini"),"Sound","EnableAudio","0");
                typeof(MainForm).GetMethod("Enable",Fields)!.Invoke(form,null);
                typeof(MainForm).GetMethod("SetStatus",Fields)!.Invoke(form,["Ready to play","Choose your look and launch solo. Open Online to try two players.",false]);
                await Capture("play-ready");
                form.ClientSize=new System.Drawing.Size(924,681);await Capture("play-small");form.ClientSize=new System.Drawing.Size(1000,760);
                Page("settings");await Capture("settings");Page("activity");await Capture("activity");Page("play");
                Get<Button>(form,"solo").PerformClick();
                await Until(()=>Get<Process?>(form,"game") is {HasExited:false});var solo=Get<Process>(form,"game");
                await Task.Delay(5000);Check(!solo.HasExited,"solo launched through actual button and stayed running");
                solo.CloseMainWindow();await Until(()=>Get<Process?>(form,"game")==null);
                Page("settings");Get<Button>(form,"host").PerformClick();await Until(()=>Get<Relay?>(form,"relay")!=null&&!Get<bool>(form,"busy"));
                using var client=new HttpClient();Check((await client.GetStringAsync("http://127.0.0.1:24816/health")).Contains("presence"),"actual host button starts responding relay");
                Get<Button>(form,"host").PerformClick();await Until(()=>Get<Relay?>(form,"relay")==null&&!Get<bool>(form,"busy"));
                if(!internetTest)Get<ComboBox>(form,"connection").SelectedIndex=1;
                Page("online");await Capture("online-host");
                Get<Button>(form,"create").PerformClick();await Until(()=>Get<Bridge?>(form,"bridge")!=null&&Get<Process?>(form,"game") is {HasExited:false},internetTest?120000:10000);
                Check(Get<Relay?>(form,"relay")!=null,"create automatically starts relay");
                var bridge=Get<Bridge>(form,"bridge");var online=Get<Process>(form,"game");
                string sessionUrl=Get<TextBox>(form,"url").Text;
                await Task.Delay(5000);
                Check(bridge.Welcome.Slot==1&&Get<TextBox>(form,"code").Text.Length==16,"create button connects and displays room code");
                await Capture("online-connected");
                Get<Button>(form,"copyInvite").PerformClick();await Until(()=>!Get<bool>(form,"busy"));
                string invite=Clipboard.GetText();Check(invite.Contains(internetTest?"Relay: wss://":"Relay: ws://")&&invite.Contains(bridge.Welcome.Code)&&invite.Contains("Share tools: Yes")&&!invite.Contains("Relay: ws://127.0.0.1"),"copy invitation contains correct public/LAN address, code and matching settings");
                if(internetTest){
                    Check(Get<InternetTunnel>(form,"internet").Running&&sessionUrl.StartsWith("wss://")&&sessionUrl.Contains(".trycloudflare.com/room"),"Internet host starts a verified public WSS link through actual UI");
                    await using var friend=new Bridge(_=>{});await friend.Connect(sessionUrl,new Hello("join",bridge.Welcome.Code,"Remote test",Protocol.Build,Protocol.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat")),Install.GameplayHash(),true));
                    using var udpFriend=new System.Net.Sockets.UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,0));
                    byte[] registration=new byte[Protocol.Size];"Z3N1"u8.CopyTo(registration);registration[13]=2;System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(registration.AsSpan(4),friend.Token);
                    await udpFriend.SendAsync(registration,new System.Net.IPEndPoint(System.Net.IPAddress.Loopback,friend.Port));
                    using var timerPublic=new CancellationTokenSource(10000);var frame=await udpFriend.ReceiveAsync(timerPublic.Token);
                    Check(frame.Buffer[13]==1&&System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(frame.Buffer.AsSpan(4))==friend.Token,"actual native game frames reach a second client through public Internet WSS");
                }
                Check(bridge.Sent>30,"actual online game sends frames through companion");
                Check(online.StartInfo.WorkingDirectory.StartsWith(Path.Combine(Install.Data,"Online")),"online game launched in private campaign folder");
                var privateIni=Path.Combine(online.StartInfo.WorkingDirectory,"zelda3.ini");
                Check(File.ReadAllText(privateIni).Contains("Fullscreen = 1"),"selected fullscreen display is copied to online profile");
                var saves=Path.Combine(online.StartInfo.WorkingDirectory,"saves");Directory.CreateDirectory(saves);
                var fixture=Path.Combine(saves,"backup-test.txt");File.WriteAllText(fixture,"save isolation fixture");
                Get<Button>(form,"disconnect").PerformClick();await Until(()=>Get<Bridge?>(form,"bridge")==null&&!Get<bool>(form,"busy"));
                Check(!online.HasExited,"disconnect leaves own game running safely");
                if(internetTest)Check(Get<InternetTunnel?>(form,"internet")==null&&Get<Relay?>(form,"relay")==null,"disconnect stops owned Internet helper and loopback server");
                online.CloseMainWindow();await Until(()=>Get<Process?>(form,"game")==null);
                string again=Install.OnlineProfile(sessionUrl,bridge.Welcome.Code);
                Check(Directory.EnumerateFiles(Path.Combine(again,"Backups"),"backup-test.txt",SearchOption.AllDirectories).Any(p=>File.ReadAllText(p)=="save isolation fixture"),"campaign save backup round-trip");
                Check(!File.Exists(Path.Combine(Install.Game,"saves","backup-test.txt")),"online save stayed outside solo save directory");
                File.Delete(fixture);
                if(Get<Relay?>(form,"relay")==null){Page("settings");Get<Button>(form,"host").PerformClick();await Until(()=>Get<Relay?>(form,"relay")!=null&&!Get<bool>(form,"busy"));Page("online");}
                Get<Button>(form,"joinMode").PerformClick();Get<TextBox>(form,"url").Text="ws://127.0.0.1:24816/room";
                await Capture("online-join");
                Get<TextBox>(form,"code").Text="0000000000000000";Get<Button>(form,"join").PerformClick();
                await Until(()=>!Get<bool>(form,"busy"));
                Check(Get<Button>(form,"create").Enabled&&Get<Button>(form,"join").Enabled,"failed join restores UI controls");
                Check(Get<Label>(form,"statusTitle").Text=="Something needs attention","failed join displays visible recovery feedback");await Capture("join-error");
                Page("settings");Get<CheckBox>(form,"audio").Checked=true;Get<CheckBox>(form,"dimFlashes").Checked=true;Get<Button>(form,"saveSettings").PerformClick();await Until(()=>!Get<bool>(form,"busy"));
                Check(Install.GetIni("Sound","EnableAudio")=="1"&&Install.GetIni("Graphics","DimFlashes")=="1","settings save sound and reduced flashes");
                Get<ComboBox>(form,"display").SelectedIndex=1;string preferences=File.ReadAllText(Path.Combine(Install.Data,"launcher-preferences.json"));Check(preferences.Contains("\"Look\":1")&&!preferences.Contains(bridge.Welcome.Code),"look preference saved locally without a room code");
                Page("guide");Get<ComboBox>(form,"guideTopics").SelectedIndex=3;await Capture("controls-guide");
                Install.SetIni(Path.Combine(Install.Game,"zelda3.ini"),"Sound","EnableAudio","1");
                Check(File.ReadAllText(Path.Combine(Install.Game,"zelda3.ini")).Contains("EnableAudio = 1"),"shipping profile retains enabled audio");
                File.WriteAllText(Path.Combine(Install.Root,"ui-smoke-results.json"),JsonSerializer.Serialize(new{checks,assetHash=expected},new JsonSerializerOptions{WriteIndented=true}));
                Console.WriteLine(checks.Count+" UI/setup checks passed.");
            }catch(Exception e){failure=e;Console.Error.WriteLine(e);}
            finally {
                if(oldClipboard!=null)Clipboard.SetDataObject(oldClipboard,true);
                var game=Get<Process?>(form,"game");if(game is {HasExited:false}){game.CloseMainWindow();await Task.Delay(600);}
                Install.SetIni(Path.Combine(Install.Game,"zelda3.ini"),"Sound","EnableAudio","1");
                form.Close();
            }
        };
        Application.Run(form);return failure==null?0:1;
    }
}
