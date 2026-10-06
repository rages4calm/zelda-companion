using System.Diagnostics;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.Text.Json;
using ZeldaCompanion;

internal static class CoopDiagnostics {
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static T Field<T>(MainForm f,string n)=>(T)typeof(MainForm).GetField(n,Flags)!.GetValue(f)!;
    static object? Call(MainForm f,string method,params object[] values)=>typeof(MainForm).GetMethod(method,Flags)!.Invoke(f,values);
    static async Task Until(Func<bool> done,int ms=15000){var watch=Stopwatch.StartNew();while(!done()){if(watch.ElapsedMilliseconds>ms)throw new TimeoutException("UI check timed out.");await Task.Delay(50);}}
    public static int Run(string output,string captureScript){
        Directory.CreateDirectory(output);var checks=new List<string>();Exception? error=null;var peers=new List<Bridge>();var games=new List<UdpClient>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
        string id=new('A',24),saved=Path.Combine(Install.Data,"Online",id,"saves");Directory.CreateDirectory(saved);string marker=Path.Combine(saved,"private-fixture.txt");File.WriteAllText(marker,"Private campaign preservation fixture.");
        using var form=new MainForm();
        async Task Capture(string label){form.Activate();await Task.Delay(180);var info=new ProcessStartInfo("python"){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add(captureScript);info.ArgumentList.Add(form.Handle.ToInt64().ToString());info.ArgumentList.Add(Path.Combine(output,label+".png"));using var p=Process.Start(info)!;await p.WaitForExitAsync();Check(p.ExitCode==0,"captured actual owned window: "+label);}
        form.Shown+=async(_,_)=>{
            try{
                Check(Install.Ready,"private UI test uses initialized game data");
                Check(Field<CheckBox>(form,"showNames").Checked&&!Field<CheckBox>(form,"sharedCombat").Checked,"names on; combat beta off by default");
                Check(Field<ComboBox>(form,"campaign").Items.Count>=2,"saved online campaign appears in selector");
                string profile=Install.OnlineProfile("ws://127.0.0.1:1/room",new('B',16),id);
                Check(profile==Path.GetDirectoryName(saved)&&File.ReadAllText(marker)=="Private campaign preservation fixture.","continuing campaign preserves its original files");
                Check(Directory.EnumerateFiles(Path.Combine(profile,"Backups"),"private-fixture.txt",SearchOption.AllDirectories).Any(),"continued campaign backed up before launch");
                bool refused=false;try{Install.OnlineProfile("x","y","../escape");}catch(IOException){refused=true;}Check(refused,"campaign selector rejects paths outside its save root");
                Call(form,"ShowPage","online");Call(form,"SetStatus","UI test · synthetic players","Separate test folder; personal installation stays untouched.",false);await Capture("online-host");
                form.ClientSize=new(940,720);Call(form,"SetRole",false);await Capture("online-join-small");
                var join=Field<Button>(form,"join");var area=Field<Panel>(form,"content").RectangleToScreen(Field<Panel>(form,"content").ClientRectangle);
                Check(area.Contains(join.RectangleToScreen(join.ClientRectangle)),"small-window join action stays fully inside the visible content");
                form.ClientSize=new(1000,760);Call(form,"SetRole",true);
                Field<TextBox>(form,"name").Text="Demo host";Field<Button>(form,"create").PerformClick();
                await Until(()=>Field<Bridge?>(form,"bridge")!=null&&Field<Process?>(form,"game") is {HasExited:false}&&!Field<bool>(form,"busy"),120000);
                var host=Field<Bridge>(form,"bridge");var game=Field<Process>(form,"game");var relay=Field<Relay>(form,"relay");
                Check(new Uri(relay.ListenUrl).Port!=24816,"actual Internet create button allocates a free local port");
                Check(Field<InternetTunnel>(form,"internet").Running&&Field<TextBox>(form,"url").Text.StartsWith("wss://"),"actual create button starts public Internet tunnel");
                Check(host.PlayerName=="Demo host"&&!host.SharedCombat,"launcher passes identity and selected combat mode");
                for(int i=2;i<=8;i++){
                    var p=new Bridge(_=>{});peers.Add(p);await p.Connect(Field<TextBox>(form,"url").Text,new Hello("join",host.Welcome.Code,$"Bot {i}",Protocol.Build,Protocol.HashFile(Path.Combine(Install.Game,"zelda3_assets.dat")),Install.GameplayHash(),true));
                    var udp=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));games.Add(udp);var frame=new byte[Protocol.Size];"Z3N2"u8.CopyTo(frame);frame[13]=(byte)i;BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(4),p.Token);BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(8),1);await udp.SendAsync(frame,new IPEndPoint(IPAddress.Loopback,p.Port));
                }
                await Until(()=>host.Players.Length==8&&Field<Label>(form,"sessionStatus").Text.Contains("8 / 8"));await Until(()=>host.Sent>30);
                Check(Field<Label>(form,"roster").Text.Contains("Demo host")&&Field<Label>(form,"roster").Text.Contains("Bot 8"),"live eight-player roster lists authenticated names");
                string invite=(string)Call(form,"InvitationText")!;Check(invite.Contains("0.3.0")&&invite.Contains("Shared progress: Yes")&&invite.Contains("Combat beta: No"),"invitation states correct release and both shared choices");
                Check(!Field<CheckBox>(form,"sharedCombat").Enabled&&!Field<ComboBox>(form,"campaign").Enabled,"active room locks compatibility choices and campaign");
                Call(form,"SetStatus","UI test · synthetic room","One actual game and seven protocol clients; this is a test session.",false);await Capture("online-eight-connected");form.ClientSize=new(940,720);await Capture("online-eight-small");
                game.CloseMainWindow();await Until(()=>Field<Process?>(form,"game")==null);
                Check(File.ReadAllText(marker)=="Private campaign preservation fixture.","normal game exit preserves continued campaign fixture");
                Check(Field<Button>(form,"create").Enabled,"launcher becomes ready again after game closes");
                Call(form,"ShowPage","guide");Field<ComboBox>(form,"guideTopics").SelectedIndex=2;await Capture("coop-guide-small");
                Check(Field<ComboBox>(form,"guideTopics").Items.Count==8,"all eight guide topics retained");
            }catch(Exception e){error=e;Console.Error.WriteLine(e);}
            finally{foreach(var udp in games)udp.Dispose();foreach(var p in peers)await p.DisposeAsync();var game=Field<Process?>(form,"game");if(game is {HasExited:false}){game.CloseMainWindow();await Until(()=>Field<Process?>(form,"game")==null);}form.Close();}
        };
        Application.Run(form);File.WriteAllText(Path.Combine(output,"ui-results.json"),JsonSerializer.Serialize(new{passed=checks.Count,checks,error=error?.Message},new JsonSerializerOptions{WriteIndented=true}));return error==null?0:1;
    }
}
