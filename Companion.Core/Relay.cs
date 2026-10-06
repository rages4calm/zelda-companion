using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ZeldaCompanion;

public sealed class Relay : IAsyncDisposable
{
    sealed class Peer(WebSocket socket, int slot, string name)
    {
        public readonly WebSocket Socket = socket;
        public readonly int Slot = slot;
        public readonly string Name = name;
        public readonly uint Epoch = BitConverter.ToUInt32(System.Security.Cryptography.RandomNumberGenerator.GetBytes(4)) | 1;
        readonly object gate=new();
        readonly Dictionary<int,(byte[] Data,WebSocketMessageType Type)> latest=[];
        public readonly Channel<bool> Pulse=Channel.CreateBounded<bool>(1);
        public void Put(int key,byte[] data,WebSocketMessageType type=WebSocketMessageType.Binary){lock(gate)latest[key]=(data,type);Pulse.Writer.TryWrite(true);}
        public (byte[] Data,WebSocketMessageType Type)[] Drain(){lock(gate){var batch=latest.OrderBy(x=>x.Key).Select(x=>x.Value).ToArray();latest.Clear();return batch;}}
    }
    sealed class Room(Hello hello)
    {
        public readonly Hello Hello = hello;
        public readonly List<Peer> Peers = [];
        public readonly byte[] State = new byte[Protocol.StateSize];
        public readonly byte[] World = new byte[Protocol.WorldSize];
    }
    readonly object gate = new();
    readonly Dictionary<string, Room> rooms = [];
    readonly Dictionary<string,int> connections = [];
    int connectionCount;
    readonly WebApplication app;
    readonly Action<string> log;
    public string ListenUrl=>app.Urls.First();
    public Relay(string bindUrl, Action<string>? logger = null)
    {
        log = logger ?? Console.WriteLine;
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders(); builder.WebHost.UseUrls(bindUrl);
        app = builder.Build();
        app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10), KeepAliveTimeout = TimeSpan.FromSeconds(10) });
        app.MapGet("/health", () => Results.Text("Zelda companion coop alpha 2"));
        app.Map("/room", Handle);
    }
    public async Task Start(CancellationToken ct = default) { await app.StartAsync(ct); log("Relay listening. Up to eight players per room."); }
    static RoomPlayer[] Players(Room room)=>room.Peers.OrderBy(p=>p.Slot).Select(p=>new RoomPlayer(p.Slot,p.Name)).ToArray();
    static void BroadcastRoster(Room room){var json=JsonSerializer.SerializeToUtf8Bytes(new Roster("roster",Players(room)));foreach(var p in room.Peers)p.Put(0,json,WebSocketMessageType.Text);}
    async Task Handle(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest || ctx.Request.Headers.Origin.Count > 0) { ctx.Response.StatusCode = 400; return; }
        string ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        lock(gate) {
            int count=connections.GetValueOrDefault(ip);
            if(count>=16 || connectionCount>=128) {ctx.Response.StatusCode=429;return;}
            connections[ip]=count+1;connectionCount++;
        }
        try { await HandleSocket(ctx); }
        finally {lock(gate){connectionCount--;int count=connections[ip]-1;if(count==0)connections.Remove(ip);else connections[ip]=count;}}
    }
    async Task HandleSocket(HttpContext ctx)
    {
        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        using var life = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
        Room? room = null; Peer? peer = null; string code = ""; Task? pump = null;
        try
        {
            using var helloTimeout = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
            helloTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var first = await Protocol.ReadAsync(ws, helloTimeout.Token);
            if (first is null || first.Value.Type != WebSocketMessageType.Text || first.Value.Data.Length > 1024) throw new InvalidDataException("Invalid handshake.");
            var h = JsonSerializer.Deserialize<Hello>(first.Value.Data) ?? throw new InvalidDataException("Missing handshake.");
            if (h.Action is not ("create" or "join") || !Protocol.ValidName(h.Name) ||
                h.Build != Protocol.Build || !Hash(h.Assets) || !Hash(h.Settings)) throw new InvalidDataException("Unsupported build or handshake.");
            lock (gate)
            {
                if (h.Action == "create")
                {
                    if (rooms.Count >= 64) throw new InvalidDataException("Relay is full.");
                    do code = Protocol.NewCode(); while (rooms.ContainsKey(code));
                    room = new Room(h); rooms.Add(code,room);
                }
                else
                {
                    code = h.Code?.ToUpperInvariant() ?? "";
                    if (!rooms.TryGetValue(code, out room)) throw new InvalidDataException("Room not found.");
                    if (room.Hello.Assets != h.Assets || room.Hello.Settings != h.Settings || room.Hello.ShareTools != h.ShareTools || room.Hello.SharedCombat != h.SharedCombat)
                        throw new InvalidDataException("Game data, gameplay settings or tool sharing differ.");
                    if (room.Peers.Count == Protocol.MaxPlayers) throw new InvalidDataException("Room is full (eight players).");
                }
                int slot = Enumerable.Range(1,Protocol.MaxPlayers).First(s=>room.Peers.All(p=>p.Slot!=s));
                peer = new Peer(ws,slot,h.Name!); room.Peers.Add(peer);
            }
            Welcome welcome;lock(gate)welcome=new Welcome(code,peer.Slot,Players:Players(room),State:room.State.ToArray(),World:room.World.ToArray());
            await Protocol.SendJson(ws, welcome, life.Token);
            pump = Task.Run(async () => {
                try {
                    await foreach (var pulse in peer.Pulse.Reader.ReadAllAsync(life.Token)) {
                      foreach(var message in peer.Drain()) {
                        using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(life.Token);sendTimeout.CancelAfter(TimeSpan.FromSeconds(5));
                        await ws.SendAsync(message.Data.AsMemory(), message.Type, true, sendTimeout.Token);
                      }
                    }
                } catch { life.Cancel(); ws.Abort(); }
            });
            lock(gate)BroadcastRoster(room);
            log("Player connected.");
            uint lastSeq = 0; bool seen = false; long window = Environment.TickCount64; int rate = 0;
            while (!life.IsCancellationRequested)
            {
                using var idle = CancellationTokenSource.CreateLinkedTokenSource(life.Token);
                idle.CancelAfter(TimeSpan.FromSeconds(15));
                var msg = await Protocol.ReadAsync(ws, idle.Token);
                if (msg is null) break;
                if (msg.Value.Type == WebSocketMessageType.Text && msg.Value.Data.AsSpan().SequenceEqual("ping"u8)) continue;
                byte[] b = msg.Value.Type==WebSocketMessageType.Binary?Protocol.Unpack(msg.Value.Data):msg.Value.Data;
                if (msg.Value.Type != WebSocketMessageType.Binary || !Protocol.Valid(b) || b[13] != peer.Slot)
                    throw new InvalidDataException("Invalid player frame.");
                long now = Environment.TickCount64;
                if (now-window >= 1000) { window=now; rate=0; }
                if (++rate > 40) throw new InvalidDataException("Player frame rate exceeded.");
                uint seq = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(8));
                if (seen && unchecked((int)(seq-lastSeq)) <= 0) continue;
                seen=true; lastSeq=seq;
                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(26),peer.Epoch);
                Protocol.StampName(b,peer.Name);
                lock (gate) {
                    if (!room.Hello.ShareTools) {b.AsSpan(Protocol.StateOffset,Protocol.StateSize).Clear();b.AsSpan(Protocol.WorldOffset,Protocol.WorldSize).Clear();}
                    else {Protocol.MergeState(room.State,b.AsSpan(Protocol.StateOffset,Protocol.StateSize));room.State.CopyTo(b,Protocol.StateOffset);Protocol.MergeWorld(room.World,b.AsSpan(Protocol.WorldOffset,Protocol.WorldSize));room.World.CopyTo(b,Protocol.WorldOffset);}
                    if(!room.Hello.SharedCombat)b.AsSpan(Protocol.FightOffset,Protocol.FightSize).Clear();
                    var packed=Protocol.Pack(b);
                    foreach (var other in room.Peers) if (other != peer) other.Put(peer.Slot,packed);
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or JsonException or OperationCanceledException or WebSocketException or IOException)
        {
            if (ws.State == WebSocketState.Open) try {
                using var timeout = new CancellationTokenSource(2000);
                // No room codes, names or private data in server logs.
                await ws.CloseOutputAsync(WebSocketCloseStatus.PolicyViolation, e is InvalidDataException ? e.Message : "Connection ended.", timeout.Token);
            } catch { }
            log("Connection ended: " + e.GetType().Name);
        }
        finally
        {
            life.Cancel(); peer?.Pulse.Writer.TryComplete();
            if (pump != null) try { await pump; } catch { }
            lock (gate) if (peer != null && room != null) {
                room.Peers.Remove(peer); if (room.Peers.Count == 0) rooms.Remove(code);else BroadcastRoster(room);
            }
        }
    }
    static bool Hash(string? s) => s?.Length == 64 && s.All(Uri.IsHexDigit);
    public async ValueTask DisposeAsync() { await app.StopAsync(TimeSpan.FromSeconds(3)); await app.DisposeAsync(); }
}
