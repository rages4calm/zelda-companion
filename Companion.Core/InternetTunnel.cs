using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace ZeldaCompanion;

// Owns only its child cloudflared process. Never installs a service or changes a router.
public sealed class InternetTunnel(string executable,string workDirectory,Action<string> log) : IAsyncDisposable
{
    const string ExpectedSha="F096265EC2FCBE9BB6E2D64268DB167CED3FCBB83D894BDB9E2FCDB26F2EA7E2";
    readonly CancellationTokenSource life=new();
    readonly TaskCompletionSource<string> address=new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly List<Task> pumps=[];
    Process? process;
    public string? RelayUrl {get;private set;}
    public bool Running=>process is {HasExited:false};
    public async Task<string> Start(string localHttp,CancellationToken cancel=default){
        if(!File.Exists(executable))throw new IOException("The bundled Internet helper is missing. Extract the complete release ZIP again.");
        if(Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(executable,cancel)))!=ExpectedSha)throw new IOException("Internet helper verification failed. Use the verified helper from the clean release.");
        if(!Uri.TryCreate(localHttp,UriKind.Absolute,out var local)||!local.IsLoopback||local.Scheme!="http")throw new ArgumentException("The Internet helper can expose only this launcher's loopback relay.");
        Directory.CreateDirectory(workDirectory);string config=Path.Combine(workDirectory,"empty-config.yml");await File.WriteAllTextAsync(config,"{}\n",cancel);
        var info=new ProcessStartInfo(executable){WorkingDirectory=workDirectory,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true};
        foreach(var arg in new[]{"tunnel","--config",config,"--no-autoupdate","--protocol","http2","--metrics","127.0.0.1:0","--url",localHttp})info.ArgumentList.Add(arg);
        // An existing user cloudflared environment must not redirect our origin or load credentials.
        foreach(var key in info.Environment.Keys.Where(k=>k.StartsWith("TUNNEL_",StringComparison.OrdinalIgnoreCase)||k is "NO_AUTOUPDATE" or "NO_TLS_VERIFY").ToArray())info.Environment.Remove(key);
        process=Process.Start(info)??throw new IOException("Could not start the Internet helper.");process.StandardInput.Close();
        pumps.Add(Pump(process.StandardOutput));pumps.Add(Pump(process.StandardError));
        _=WatchExit(process);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancel,life.Token);timeout.CancelAfter(TimeSpan.FromSeconds(90));
        try{
            string https=await address.Task.WaitAsync(timeout.Token);
            using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(3)};
            while(true){timeout.Token.ThrowIfCancellationRequested();if(!Running)throw new IOException("Internet helper stopped. Check Activity, then create a new room.");
                try{var health=await http.GetStringAsync(https+"/health",timeout.Token);if(health.Trim()=="Zelda companion coop alpha 2")break;}
                catch(Exception e) when(e is HttpRequestException or TaskCanceledException){if(timeout.IsCancellationRequested)throw;}
                await Task.Delay(700,timeout.Token);
            }
            RelayUrl="wss://"+new Uri(https).Host+"/room";log("Internet relay verified through Cloudflare. No port forwarding required.");return RelayUrl;
        }catch(OperationCanceledException){throw new IOException("Internet hosting did not become reachable within 90 seconds. Check your connection, then try a new room. Activity contains helper errors.");}
    }
    async Task Pump(StreamReader reader){try{while(await reader.ReadLineAsync(life.Token) is {} line){
        var match=Regex.Match(line,@"https://[a-z0-9-]+\.trycloudflare\.com\b");if(match.Success)address.TrySetResult(match.Value);
        if(line.Contains(" ERR ")||line.Contains(" WRN ")){string safe=Regex.Replace(line,@"https?://\S+","[address]");log("Internet helper: "+safe[..Math.Min(safe.Length,400)]);}
    }}catch(OperationCanceledException){}catch(IOException){}}
    async Task WatchExit(Process child){try{await child.WaitForExitAsync(life.Token);if(!life.IsCancellationRequested){address.TrySetException(new IOException("The Internet helper exited. Check Activity and retry hosting."));log("Internet helper stopped; create a new room to reconnect.");}}catch(OperationCanceledException){}}
    public async ValueTask DisposeAsync(){life.Cancel();if(process is {HasExited:false}){process.Kill(true);await process.WaitForExitAsync();}try{await Task.WhenAll(pumps);}catch{}process?.Dispose();process=null;life.Dispose();}
}
