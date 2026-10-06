using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text.Json;
using ZeldaCompanion;

if(args.Length==3&&args[0]=="--shuffle"){await ShuffleTests.Run(args[1],args[2]);return;}
string url="ws://127.0.0.1:24819/room";
await using var relay=new Relay("http://127.0.0.1:24819",_=>{});await relay.Start();
await using var publicTunnel=args.Length==3&&args[0]=="--internet"?new InternetTunnel(Path.GetFullPath(args[1]),Path.GetFullPath(args[2]),Console.WriteLine):null;
if(publicTunnel!=null){url=await publicTunnel.Start("http://127.0.0.1:24819");Console.WriteLine("Eight-client tests use the public Cloudflare WSS address.");}
string hash=new('A',64);int passed=0;
void Check(bool ok,string test){if(!ok)throw new Exception("FAILED: "+test);Console.WriteLine("PASS "+test);passed++;}
Hello Hello(string action,string code="",string? data=null,bool share=true,string name="Tester",bool combat=false)=>new(action,code,name,Protocol.Build,data??hash,hash,share,combat);
async Task<(ClientWebSocket Socket,Welcome Welcome)> Client(Hello h){var s=new ClientWebSocket();await s.ConnectAsync(new Uri(url),CancellationToken.None);await Protocol.SendJson(s,h,CancellationToken.None);using var timeout=new CancellationTokenSource(5000);var r=await Protocol.ReadAsync(s,timeout.Token);if(r==null){s.Dispose();throw new IOException("Rejected");}return(s,JsonSerializer.Deserialize<Welcome>(r.Value.Data)!);}
async Task Reject(Hello h,string test){using var s=new ClientWebSocket();await s.ConnectAsync(new Uri(url),CancellationToken.None);await Protocol.SendJson(s,h,CancellationToken.None);using var t=new CancellationTokenSource(5000);var r=await Protocol.ReadAsync(s,t.Token);Check(r==null,test);}
byte[] Frame(int slot,uint token=0,uint seq=1){var b=new byte[Protocol.Size];"Z3N2"u8.CopyTo(b);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),token);BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(8),seq);b[13]=(byte)slot;b[12]=1;return b;}
async Task<byte[]> ReadFrame(ClientWebSocket socket,int slot=0){using var t=new CancellationTokenSource(10000);while(true){var r=await Protocol.ReadAsync(socket,t.Token);if(r==null)throw new IOException("Unexpected disconnect");if(r.Value.Type!=WebSocketMessageType.Binary)continue;var f=Protocol.Unpack(r.Value.Data);if(slot==0||f[13]==slot)return f;}}
Task Send(ClientWebSocket socket,byte[] frame)=>socket.SendAsync(Protocol.Pack(frame).AsMemory(),WebSocketMessageType.Binary,true,CancellationToken.None).AsTask();
async Task<byte[]> GameFrame(UdpClient udp,int slot=0){using var t=new CancellationTokenSource(10000);while(true){var r=await udp.ReceiveAsync(t.Token);if(r.Buffer[13]!=0&&(slot==0||r.Buffer[13]==slot))return r.Buffer;}}

if(args.Length==3&&args[0] is "--pair" or "--bots"){
    int count=args[0]=="--bots"?8:2;string root=Path.GetFullPath(args[1]),outdir=Path.GetFullPath(args[2]);Directory.CreateDirectory(outdir);
    var clients=new List<Bridge>();var processes=new List<Process>();var latest=new byte[count][];var history=new List<string>[count];
    try{
        string assets=Protocol.HashFile(Path.Combine(root,"A","zelda3_assets.dat")),settings=Protocol.HashFile(Path.Combine(root,"A","zelda3.ini"));
        for(int i=0;i<count;i++){
            string label=((char)('A'+i)).ToString();var b=new Bridge(_=>{});clients.Add(b);history[i]=[];
            await b.Connect(url,new Hello(i==0?"create":"join",i==0?"":clients[0].Welcome.Code,$"Bot {i+1}",Protocol.Build,assets,settings,true,true));
            int index=i;b.LocalFrame+=f=>{latest[index]=f.ToArray();string row=$"{f[12]},{f[14]},{BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(20))},{BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(22))},{BinaryPrimitives.ReadUInt16LittleEndian(f.AsSpan(24))}";lock(history[index])if(history[index].Count==0||history[index][^1]!=row)history[index].Add(row);};
            var si=new ProcessStartInfo(Path.Combine(root,label,"online.exe")){WorkingDirectory=Path.Combine(root,label),UseShellExecute=false,RedirectStandardError=true,RedirectStandardOutput=true};
            si.Environment["Z3ONLINE_PORT"]=b.Port.ToString();si.Environment["Z3ONLINE_TOKEN"]=b.Token.ToString("x8");si.Environment["Z3ONLINE_SLOT"]=b.Welcome.Slot.ToString();si.Environment["Z3ONLINE_NAME"]=$"Bot {i+1}";si.Environment["Z3ONLINE_COMBAT"]="1";
            var p=Process.Start(si)!;processes.Add(p);_=Task.Run(async()=>File.WriteAllText(Path.Combine(outdir,label+".stderr"),await p.StandardError.ReadToEndAsync()));_=Task.Run(async()=>File.WriteAllText(Path.Combine(outdir,label+".stdout"),await p.StandardOutput.ReadToEndAsync()));
        }
        File.WriteAllText(Path.Combine(outdir,"pids.json"),JsonSerializer.Serialize(processes.Select((p,i)=>(p,i)).ToDictionary(x=>((char)('A'+x.i)).ToString(),x=>x.p.Id)));
        for(int tick=0;tick<90&&processes.Any(p=>!p.HasExited);tick++){await Task.Delay(1000);for(int i=0;i<count;i++)if(latest[i]!=null)File.WriteAllBytes(Path.Combine(outdir,((char)('A'+i))+".frame"),latest[i]);}
        foreach(var p in processes)if(!p.HasExited)p.CloseMainWindow();await Task.WhenAll(processes.Select(p=>p.WaitForExitAsync()));
        File.WriteAllText(Path.Combine(outdir,"native-group.json"),JsonSerializer.Serialize(clients.Select((b,i)=>new{slot=b.Welcome.Slot,b.Sent,b.Received,roster=b.Players.Length,exit=processes[i].ExitCode,movement=history[i].Count}),new JsonSerializerOptions{WriteIndented=true}));
    }finally{foreach(var p in processes){if(!p.HasExited)p.CloseMainWindow();p.Dispose();}foreach(var b in clients)await b.DisposeAsync();}
    return;
}

Check(!Protocol.Valid(new byte[1]),"wrong packet length rejected");
Check(Protocol.Valid(Frame(8)),"eighth player frame accepted");
var corrupt=Frame(1);corrupt[Protocol.StateOffset+10]=2;Check(!Protocol.Valid(corrupt),"invalid lantern value rejected");
corrupt=Frame(9);Check(!Protocol.Valid(corrupt),"ninth player slot rejected");
corrupt=Frame(1);corrupt[19]=1;Check(!Protocol.Valid(corrupt),"reserved fields rejected");
corrupt=Frame(1);corrupt[Protocol.StateOffset+3]=1;Check(!Protocol.Valid(corrupt),"ammunition excluded from shared snapshot");
corrupt=Frame(1);corrupt[Protocol.StateOffset+45]=1;Check(!Protocol.Valid(corrupt),"current health excluded from shared snapshot");
corrupt=Frame(1);corrupt[Protocol.StateOffset+44]=28;Check(!Protocol.Valid(corrupt),"invalid heart-piece encoding rejected");
corrupt=Frame(1);corrupt[Protocol.NameOffset+Protocol.NameBytes-1]=1;Check(!Protocol.Valid(corrupt),"unterminated name rejected");
corrupt=Frame(1);corrupt[Protocol.FightOffset]=0x53;Check(!Protocol.Valid(corrupt),"unsupported boss state rejected");
Check(Protocol.ValidName("Zoë 林")&&!Protocol.ValidName("bad\nname")&&!Protocol.ValidName(new string('X',25)),"Unicode names supported and control/oversized names rejected");
var sample=Frame(1);sample[Protocol.Header+15]=0xff;var packed=Protocol.Pack(sample);Check(packed.Length<sample.Length/10&&Protocol.Unpack(packed).SequenceEqual(sample),"bounded binary compression round trip and traffic reduction");
bool oversized=false;try{Protocol.Unpack(Protocol.Pack(new byte[Protocol.Size+1]));}catch(InvalidDataException){oversized=true;}Check(oversized,"oversized decompressed frame rejected");

var sockets=new List<ClientWebSocket>();string room="";uint previousEpoch=0;
try{
    var (one,w)=await Client(Hello("create",name:"Host"));sockets.Add(one);room=w.Code;
    Check(w.Slot==1&&w.Code.Length==16&&w.Players!.Length==1,"room code, host slot and initial roster");
    await Reject(Hello("join",room,new string('B',64)),"asset mismatch rejected");
    await Reject(Hello("join",room,share:false),"progress-sharing mismatch rejected");
    await Reject(Hello("join",room,combat:true),"combat mismatch rejected");
    await Reject(Hello("join",room) with{Build="presence-alpha-1"},"old two-player release rejected");
    await Reject(Hello("join",room,name:"bad\nname"),"control characters rejected in handshake");
    for(int i=2;i<=8;i++){var (s,wi)=await Client(Hello("join",room,name:$"Player {i}"));sockets.Add(s);Check(wi.Slot==i&&wi.Players!.Length==i,$"player {i} receives an accurate roster");}
    await Reject(Hello("join",room),"full eight-player room rejects ninth player");
    var f=Frame(1);f[Protocol.StateOffset+10]=1;f[Protocol.WorldOffset+1]=0x80;Protocol.StampName(f,"Spoofed name");await Send(one,f);
    for(int i=1;i<8;i++){var got=await ReadFrame(sockets[i],1);Check(got[Protocol.StateOffset+10]==1&&got[Protocol.WorldOffset+1]==0x80&&Protocol.FrameName(got)=="Host",$"player {i+1} receives host lantern, door state and authenticated name");}
    var third=Frame(3,seq:1);third[Protocol.StateOffset+25]=3;third[Protocol.StateOffset+52]=1;await Send(sockets[2],third);var thirdGot=await ReadFrame(one,3);previousEpoch=BinaryPrimitives.ReadUInt32LittleEndian(thirdGot.AsSpan(26));Check(thirdGot[Protocol.StateOffset+10]==1&&thirdGot[Protocol.StateOffset+25]==3,"room snapshot merges acquisitions from different players");
    var stale=Frame(3,seq:1);stale[22]=99;await Send(sockets[2],stale);var fresh=Frame(3,seq:2);fresh[22]=12;await Send(sockets[2],fresh);Check((await ReadFrame(one,3))[22]==12,"stale per-player sequence discarded");
    sockets[2].Abort();sockets[2].Dispose();await Task.Delay(150);
    var (replacement,wr)=await Client(Hello("join",room,name:"Replacement"));sockets[2]=replacement;
    Check(wr.Slot==3&&wr.State![10]==1&&wr.State[25]==3&&wr.World![1]==0x80,"vacant slot reused; late join receives retained gear and world state");
    await Send(replacement,Frame(3,seq:1));var newGot=await ReadFrame(one,3);Check(BinaryPrimitives.ReadUInt32LittleEndian(newGot.AsSpan(26))!=previousEpoch&&Protocol.FrameName(newGot)=="Replacement","replacement player gets new epoch and name; sequence resets safely");
    for(int i=0;i<7;i++)await Send(sockets[i],Frame(i+1,seq:100));
    var sources=new HashSet<int>();while(sources.Count<7)sources.Add((await ReadFrame(sockets[7]))[13]);Check(sources.SetEquals(Enumerable.Range(1,7)),"fair delivery retains frames from all seven peers");
    await Send(one,Frame(2,seq:101));using var closeTimeout=new CancellationTokenSource(5000);bool closed=false;while(!closed){var r=await Protocol.ReadAsync(one,closeTimeout.Token);closed=r==null;}Check(closed,"player slot spoof rejected");
}finally{foreach(var s in sockets){s.Abort();s.Dispose();}}
await Task.Delay(150);await Reject(Hello("join",room),"empty room expires");

var bridges=new List<Bridge>();var games=new List<UdpClient>();
try{
    for(int i=0;i<8;i++){var b=new Bridge(_=>{});bridges.Add(b);await b.Connect(url,Hello(i==0?"create":"join",i==0?"":bridges[0].Welcome.Code,name:$"Bridge {i+1}"));var g=new UdpClient(new IPEndPoint(IPAddress.Loopback,0));games.Add(g);await g.SendAsync(Frame(i+1,b.Token),new IPEndPoint(IPAddress.Loopback,b.Port));}
    await Task.Delay(250);Check(bridges.All(b=>b.Players.Length==8),"eight real WebSocket bridges receive live rosters");
    for(int i=0;i<7;i++)await games[i].SendAsync(Frame(i+1,bridges[i].Token,2),new IPEndPoint(IPAddress.Loopback,bridges[i].Port));
    var slots=new HashSet<int>();while(slots.Count<7){var got=await GameFrame(games[7]);Check(BinaryPrimitives.ReadUInt32LittleEndian(got.AsSpan(4))==bridges[7].Token,"incoming frame receives recipient's local authentication token");slots.Add(got[13]);}
    Check(slots.SetEquals(Enumerable.Range(1,7)),"UDP-WebSocket-UDP fanout reaches all seven peers");
    await bridges[3].DisposeAsync();bridges.RemoveAt(3);await Task.Delay(200);Check(bridges.All(b=>b.Players.Length==7&&b.Players.All(p=>p.Slot!=4)),"departed player removed from every room roster");
}finally{foreach(var g in games)g.Dispose();foreach(var b in bridges)await b.DisposeAsync();}

Console.WriteLine($"{passed} checks passed.");
if(publicTunnel!=null)File.WriteAllText(Path.Combine(args[2],"public-coop-results.json"),JsonSerializer.Serialize(new{passed,publicWss=true,clients=8,utc=DateTime.UtcNow},new JsonSerializerOptions{WriteIndented=true}));
