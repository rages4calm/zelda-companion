using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Channels;
using System.Collections.Concurrent;

namespace ZeldaCompanion;

public sealed class Bridge : IAsyncDisposable
{
    readonly ClientWebSocket ws = new();
    readonly UdpClient udp = new(new IPEndPoint(IPAddress.Loopback,0));
    readonly CancellationTokenSource life = new();
    readonly Channel<byte[]> queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1) { FullMode=BoundedChannelFullMode.DropOldest });
    readonly Action<string> log;
    IPEndPoint? game;
    Task[] tasks = [];
    public Welcome Welcome { get; private set; } = new("",0);
    public uint Token { get; } = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4)) | 1;
    public int Port => ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
    public long Sent { get; private set; }
    public long Received { get; private set; }
    public bool IsConnected => !life.IsCancellationRequested && ws.State == WebSocketState.Open;
    public long LastPeerFrame { get; private set; }
    public bool SharedCombat {get;private set;}
    public string PlayerName {get;private set;}="Player";
    RoomPlayer[] players=[];
    public RoomPlayer[] Players=>Volatile.Read(ref players);
    readonly byte[]?[] waiting=new byte[Protocol.MaxPlayers+1][];
    readonly ConcurrentDictionary<int,byte[]> locations=new();
    byte[]? localLocation;
    int disposed;
    public string Activity(RoomPlayer player){if(player.Slot==Welcome.Slot)return "You";if(!locations.TryGetValue(player.Slot,out var f))return "Entering game";if(f[12]==0)return "Menu / transition";var mine=Volatile.Read(ref localLocation);return mine!=null&&f[14]==mine[14]&&f[17]==mine[17]&&f[18]==mine[18]&&f[20]==mine[20]&&f[21]==mine[21]?"Same area":$"{(f[17]==0?"Light":"Dark")} world · {(f[14]==0?"outdoors":"indoors")}";}
    public event Action<byte[]>? LocalFrame;
    public Bridge(Action<string>? logger = null) { log=logger ?? Console.WriteLine; }
    public async Task Connect(string relayUrl, Hello hello, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(relayUrl,UriKind.Absolute,out var uri) || uri.Scheme is not ("ws" or "wss") ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)) throw new ArgumentException("Use ws://host:24816/room or wss://host/room.");
        ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(5); ws.Options.KeepAliveTimeout=TimeSpan.FromSeconds(10);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await ws.ConnectAsync(uri,timeout.Token);
        await Protocol.SendJson(ws,hello,timeout.Token);
        var first = await Protocol.ReadAsync(ws,timeout.Token);
        if (first is null) throw new IOException(ws.CloseStatusDescription ?? "Relay refused the room.");
        if (first.Value.Type != WebSocketMessageType.Text) throw new InvalidDataException("Invalid relay reply.");
        Welcome=JsonSerializer.Deserialize<Welcome>(first.Value.Data) ?? throw new InvalidDataException("Missing welcome.");
        if (Welcome.Slot is < 1 or > Protocol.MaxPlayers || Welcome.Code.Length != 16 || !Welcome.Code.All(Uri.IsHexDigit) || Welcome.Mode != "coop-alpha" || !Protocol.ValidState(Welcome.State??[]) || !Protocol.ValidWorld(Welcome.World??[]))
            throw new InvalidDataException("Unsupported relay reply.");
        players=Welcome.Players??[];
        SharedCombat=hello.SharedCombat;
        PlayerName=hello.Name;
        var state=new byte[Protocol.Size];"Z3N2"u8.CopyTo(state);Welcome.State!.CopyTo(state,Protocol.StateOffset);
        BinaryPrimitives.WriteUInt32LittleEndian(state.AsSpan(8),1);waiting[0]=state;
        Welcome.World!.CopyTo(state,Protocol.WorldOffset);
        tasks = [Run(UdpLoop),Run(SendLoop),Run(ReceiveLoop)];
        log($"Connected as player {Welcome.Slot} ({Protocol.ColorName(Welcome.Slot)}). Room supports eight players.");
    }
    async Task Run(Func<Task> action)
    {
        try { await action(); }
        catch (Exception e) when (e is OperationCanceledException or SocketException or WebSocketException or IOException or ObjectDisposedException or InvalidDataException) {
            if (!life.IsCancellationRequested) log("Online connection ended: " + e.Message);
        }
        finally { life.Cancel(); }
    }
    async Task UdpLoop()
    {
        long window=Environment.TickCount64; int rate=0;
        while (!life.IsCancellationRequested) {
            var r=await udp.ReceiveAsync(life.Token); byte[] b=r.Buffer;
            if (!IPAddress.IsLoopback(r.RemoteEndPoint.Address) || !Protocol.Valid(b,true) ||
                BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4)) != Token || b[13] != Welcome.Slot ||
                (game != null && !r.RemoteEndPoint.Equals(game))) continue;
            long now=Environment.TickCount64; if (now-window>=1000) { window=now; rate=0; }
            if (++rate>40) continue;
            if(game==null){game=r.RemoteEndPoint;for(int i=0;i<waiting.Length;i++){var cached=Interlocked.Exchange(ref waiting[i],null);if(cached!=null)await SendToGame(cached);}}
            b.AsSpan(4,4).Clear();Volatile.Write(ref localLocation,b[..26]);LocalFrame?.Invoke(b); queue.Writer.TryWrite(b);
        }
    }
    async Task SendLoop()
    {
        while (!life.IsCancellationRequested) {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(life.Token);
            timeout.CancelAfter(2000);
            try { await queue.Reader.WaitToReadAsync(timeout.Token); } catch(OperationCanceledException) when (!life.IsCancellationRequested) { }
            if (queue.Reader.TryRead(out var b)) { await ws.SendAsync(Protocol.Pack(b).AsMemory(),WebSocketMessageType.Binary,true,life.Token); Sent++; }
            else await ws.SendAsync("ping"u8.ToArray().AsMemory(),WebSocketMessageType.Text,true,life.Token);
        }
    }
    async Task ReceiveLoop()
    {
        while (!life.IsCancellationRequested) {
            var msg=await Protocol.ReadAsync(ws,life.Token);
            if (msg is null) { log("Peer relay disconnected. Game can continue solo in its private profile."); return; }
            if(msg.Value.Type==WebSocketMessageType.Text){var roster=JsonSerializer.Deserialize<Roster>(msg.Value.Data);if(roster?.Type!="roster" || roster.Players.Length>Protocol.MaxPlayers || roster.Players.Any(p=>p.Slot<1||p.Slot>Protocol.MaxPlayers||!Protocol.ValidName(p.Name)) || roster.Players.Select(p=>p.Slot).Distinct().Count()!=roster.Players.Length)throw new InvalidDataException("Invalid room roster.");Volatile.Write(ref players,roster.Players);continue;}
            var b=Protocol.Unpack(msg.Value.Data);
            if (msg.Value.Type != WebSocketMessageType.Binary || !Protocol.Valid(b) || b[13]==Welcome.Slot) throw new IOException("Invalid relay player frame.");
            if(Received==0)log($"Other player is sending game frames (player {b[13]}).");
            Received++;
            locations[b[13]]=b[..26];
            LastPeerFrame=Environment.TickCount64;
            if (game != null) await SendToGame(b);
            else Interlocked.Exchange(ref waiting[b[13]],b);
        }
    }
    async Task SendToGame(byte[] b){BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4),Token);await udp.SendAsync(b,game!,life.Token);}
    public async ValueTask DisposeAsync()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        life.Cancel(); queue.Writer.TryComplete(); udp.Dispose(); ws.Abort();
        try { await Task.WhenAll(tasks); } catch { }
        ws.Dispose(); life.Dispose();
    }
}
