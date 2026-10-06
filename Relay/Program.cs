using ZeldaCompanion;
string bind = args.Length == 2 && args[0] == "--bind" ? args[1] : "http://127.0.0.1:24816";
await using var relay=new Relay(bind);
await relay.Start();
var done=new TaskCompletionSource();
Console.CancelKeyPress += (_,e) => { e.Cancel=true; done.TrySetResult(); };
Console.WriteLine("Press Ctrl+C to stop. /health and /room; no game assets on server.");
await done.Task;
