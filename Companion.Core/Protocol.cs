using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.IO.Compression;

namespace ZeldaCompanion;

public sealed record Hello(string Action, string Code, string Name, string Build, string Assets, string Settings, bool ShareTools, bool SharedCombat = false);
public sealed record RoomPlayer(int Slot, string Name);
public sealed record Roster(string Type, RoomPlayer[] Players);
public sealed record Welcome(string Code, int Slot, string Mode = "coop-alpha", RoomPlayer[]? Players = null, byte[]? State = null, byte[]? World = null);

public static class Protocol
{
    public const int MaxPlayers = 8, Header = 256, WorldOffset = Header + 32 * 48 * 2, WorldSize = 736;
    public const int FightOffset = WorldOffset + WorldSize, FightSize = 256, Size = FightOffset + FightSize;
    public const int NameOffset = 32, NameBytes = 97, StateOffset = 160, StateSize = 64;
    public const string Build = "coop-alpha-2";
    // SRAM F340..F37F: permanent acquisitions only. Ammunition, money, damage,
    // bottles, selected item and small keys never enter a shared snapshot.
    public static readonly (int Offset, byte Max, bool Bits)[] Fields = [
        (0,3,false),(1,2,false),(2,1,false),(4,2,false),(5,1,false),(6,1,false),
        (7,1,false),(8,1,false),(9,1,false),(10,1,false),(11,1,false),(12,3,false),
        (13,1,false),(14,1,false),(16,1,false),(17,1,false),(18,1,false),(19,2,false),
        (20,2,false),(21,1,false),(22,1,false),(23,1,false),(25,4,false),(26,3,false),
        (27,2,false),(28,2,false),(29,2,false),(30,2,false),(31,2,false),
        (36,255,true),(37,255,true),(38,255,true),(39,255,true),
        (40,255,true),(41,255,true),(44,163,false),(48,50,false),(49,70,false),
        (52,7,true),(57,7,true),(58,127,true),(59,2,false)];
    public static bool ValidState(ReadOnlySpan<byte> state) {
        if(state.Length!=StateSize)return false;
        for(int i=0;i<StateSize;i++) {
            var field=Fields.FirstOrDefault(f=>f.Offset==i);
            if(state[i]>field.Max || (field.Bits && (state[i]&~field.Max)!=0))return false;
        }
        return state[0] is not 2 && (state[44]==0 || state[44]>=24 && state[44]%8<=3) && state.Slice(28,4).IndexOfAnyExcept((byte)0,(byte)2)<0;
    }
    public static bool MergeState(Span<byte> target, ReadOnlySpan<byte> source) {
        bool changed=false;
        foreach(var f in Fields) {byte v=f.Bits?(byte)(target[f.Offset]|source[f.Offset]):Math.Max(target[f.Offset],source[f.Offset]);if(v!=target[f.Offset]){target[f.Offset]=v;changed=true;}}
        return changed;
    }
    public static bool ValidName(string? name)=>name is {Length:>0 and <=24} && name.Trim()==name && !name.Any(char.IsControl) && Encoding.UTF8.GetByteCount(name)<NameBytes;
    public static bool ValidWorld(ReadOnlySpan<byte> w)=>w.Length==WorldSize && w[720]<=3 && w[722]<=8 && w[723]==0 && w[725..].IndexOfAnyExcept((byte)0)<0;
    public static byte WorldMask(int i)=>i<592 ? ((i&1)==0 ? (byte)0x0f : (byte)0xfc) : i is 592+0x3b or 592+0x7b ? (byte)0xdf : i==721 ? (byte)0xbf : (byte)0xff;
    public static void MergeWorld(Span<byte> target,ReadOnlySpan<byte> source){for(int i=0;i<725;i++){if(i==720||i==722)target[i]=Math.Max(target[i],source[i]);else target[i]|=(byte)(source[i]&WorldMask(i));}}
    public static readonly byte[] CombatTypes=[0x08,0x0a,0x0e,0x11,0x12,0x18,0x19,0x20,0x22,0x41,0x42,0x43,0x45,0x46,0x47,0x48,0x49,0x4a,0x4b,0x4c,0x4e,0x4f,0x51,0x55,0x56,0x58,0x6a,0x6b,0x6d,0x6e,0x6f,0x71];
    public static bool ValidFight(ReadOnlySpan<byte> f){if(f.Length!=FightSize||f[192]>1||f[193]>MaxPlayers||f[198..].IndexOfAnyExcept((byte)0)>=0)return false;for(int k=0;k<16;k++){var s=f.Slice(k*8,8);if(s.IndexOfAnyExcept((byte)0)<0)continue;if(!CombatTypes.Contains(s[0])||s[1]>11||s[7]>1)return false;}return true;}
    public static string ColorName(int slot)=>slot switch {1=>"green",2=>"blue",3=>"red",4=>"purple",5=>"gold",6=>"cyan",7=>"rose",8=>"silver",_=>"unknown"};
    public static void StampName(byte[] b,string name){b.AsSpan(NameOffset,NameBytes).Clear();Encoding.UTF8.GetBytes(name,b.AsSpan(NameOffset,NameBytes-1));}
    public static string FrameName(byte[] b){int n=Array.IndexOf(b,(byte)0,NameOffset,NameBytes);return Encoding.UTF8.GetString(b,NameOffset,n-NameOffset);}
    public static bool Valid(byte[] b, bool local = false) => b.Length == Size &&
        b.AsSpan(0,4).SequenceEqual("Z3N2"u8) && (local || BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(4)) == 0) &&
        b[12] <= 1 && b[13] <= MaxPlayers && (!local || b[13]>=1) && b[14] <= 1 && b[18] <= 1 && b[19] == 0 &&
        b.AsSpan(30,2).IndexOfAnyExcept((byte)0)<0 && b[NameOffset+NameBytes-1]==0 &&
        b.AsSpan(129,31).IndexOfAnyExcept((byte)0)<0 && ValidState(b.AsSpan(StateOffset,StateSize)) &&
        b.AsSpan(224,32).IndexOfAnyExcept((byte)0)<0 && ValidWorld(b.AsSpan(WorldOffset,WorldSize)) && ValidFight(b.AsSpan(FightOffset,FightSize));
    // Compress only binary game frames; handshakes and private room codes are
    // never mixed into this compression stream. Limit output before allocation.
    public static byte[] Pack(byte[] b){using var ms=new MemoryStream();ms.Write("Z3C2"u8);using(var zip=new DeflateStream(ms,CompressionLevel.Fastest,true))zip.Write(b);return ms.Length<b.Length?ms.ToArray():b;}
    public static byte[] Unpack(byte[] b){if(b.Length<4 || !b.AsSpan(0,4).SequenceEqual("Z3C2"u8))return b;using var zip=new DeflateStream(new MemoryStream(b,4,b.Length-4),CompressionMode.Decompress);var decoded=new byte[Size];zip.ReadExactly(decoded);if(zip.ReadByte()!=-1)throw new InvalidDataException("Oversized compressed frame.");return decoded;}
    public static string HashFile(string path) { using var s = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(s)); }
    public static async Task<(byte[] Data, WebSocketMessageType Type)?> ReadAsync(WebSocket ws, CancellationToken ct)
    {
        byte[] buffer = new byte[Size + 1]; int n = 0;
        while (true)
        {
            var r = await ws.ReceiveAsync(buffer.AsMemory(n), ct);
            if (r.MessageType == WebSocketMessageType.Close) return null;
            n += r.Count;
            if (n > Size) throw new InvalidDataException("Message exceeds protocol limit.");
            if (r.EndOfMessage) return (buffer[..n], r.MessageType);
        }
    }
    public static Task SendJson(WebSocket ws, object value, CancellationToken ct) =>
        ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(value).AsMemory(), WebSocketMessageType.Text, true, ct).AsTask();
    public static string NewCode() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8));
}
