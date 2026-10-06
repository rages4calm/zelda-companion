using System.Diagnostics;

namespace Zelda_3_Launcher;

// Drain both output streams before reading the exit code. Build commands cannot
// wait for keyboard input, and only processes owned by this launcher are killed.
internal static class ProcessRunner
{
    private static readonly object LogLock = new();
    private static readonly Dictionary<Form, Process> Active = new();
    public static string LastError { get; private set; } = "";

    public static bool Run(Form owner, string filename, string arguments,
        bool isGame = false, int timeoutMilliseconds = 900000,
        string? workingDirectory = null, string? logPath = null)
    {
        string log = logPath ?? Path.Combine(Program.currentDirectory, "zelda3.log");
        void Write(string text)
        {
            lock (LogLock) File.AppendAllText(log, text + Environment.NewLine);
        }
        LastError = "";
        using var process = new Process();
        try
        {
            lock (LogLock)
            {
                if (File.Exists(log) && new FileInfo(log).Length > 2 * 1024 * 1024)
                    File.Move(log, log + ".previous", true);
            }
            Write($"\n{DateTime.Now:O}\nExecuting {filename} {arguments}");
            process.StartInfo = new ProcessStartInfo(filename, arguments)
            {
                WorkingDirectory = workingDirectory ?? Program.repoDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Write(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Write("[stderr] " + e.Data); };
            process.Start();
            Active.Add(owner, process);
            process.StandardInput.Close();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var timer = Stopwatch.StartNew();
            while (!process.WaitForExit(50))
            {
                Application.DoEvents();
                if (owner.IsDisposed)
                    throw new OperationCanceledException("Launcher closed; operation cancelled.");
                if (!isGame && timer.ElapsedMilliseconds >= timeoutMilliseconds)
                    throw new TimeoutException("Build step timed out. The child process was stopped.");
            }
            process.WaitForExit(); // Wait for asynchronous log handlers too.
            Write($"Exit code: {process.ExitCode}; elapsed: {timer.Elapsed.TotalSeconds:F2}s");
            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Process exited with code {process.ExitCode}.");
            return false;
        }
        catch (Exception ex)
        {
            try
            {
                if (!process.HasExited) { process.Kill(true); process.WaitForExit(); }
            }
            catch { /* Process may not have started. */ }
            LastError = ex.Message;
            Write("ERROR: " + LastError);
            return true;
        }
        finally { Active.Remove(owner); }
    }

    public static void Cancel(Form owner)
    {
        if (Active.TryGetValue(owner, out var process))
            try { if (!process.HasExited) process.Kill(true); } catch { }
    }

    public static string ErrorDetails()
    {
        string log = Path.Combine(Program.currentDirectory, "zelda3.log");
        lock (LogLock)
        {
            var tail = File.Exists(log) ? File.ReadLines(log).TakeLast(14) : Array.Empty<string>();
            return LastError + "\n\n" + string.Join("\n", tail) + "\n\nFull log: " + log;
        }
    }
}
