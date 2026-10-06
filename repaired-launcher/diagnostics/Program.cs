using System.Diagnostics;
using System.Reflection;
using Zelda_3_Launcher;

internal static class DiagnosticProgram
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        string data = Path.GetFullPath(args[0]);
        var assembly = typeof(MainForm).Assembly;
        var program = assembly.GetType("Zelda_3_Launcher.Program")!;
        foreach (var (field, value) in new[] {
            ("currentDirectory", data), ("repoDir", Path.Combine(data, "zelda3")),
            ("third_partyDir", Path.Combine(data, "zelda3", "third_party")) })
            program.GetField(field)!.SetValue(null, value);
        string log = Path.Combine(data, "process-tests.log");
        var runner = assembly.GetType("Zelda_3_Launcher.ProcessRunner")!;
        var run = runner.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
        using var owner = new Form();
        bool Failed(string command, int timeout = 5000) => (bool)run.Invoke(null,
            new object?[] { owner, "cmd.exe", command, false, timeout, data, log })!;
        void Require(bool ok, string description)
        {
            if (!ok) throw new Exception(description);
            Console.WriteLine("PASS " + description);
        }
        try
        {
            Require(!Failed("/d /c echo stdout-probe & echo stderr-probe 1>&2 & exit /b 0"), "successful process");
            string output = File.ReadAllText(log);
            Require(output.Contains("stdout-probe") && output.Contains("[stderr] stderr-probe"), "both streams logged before return");
            Require(Failed("/d /c exit /b 7"), "positive exit code fails");
            Require(Failed("/d /c exit /b -1"), "negative exit code fails");
            var timer = Stopwatch.StartNew();
            Require(Failed("/d /c pause & exit /b 9"), "hidden PAUSE receives closed stdin and fails");
            Require(timer.Elapsed.TotalSeconds < 2, "PAUSE never stalls");
            Require(Failed("/d /c ping -n 20 127.0.0.1 > nul", 250), "timeout kills owned child tree");
            Require(File.ReadAllText(log).Contains("timed out"), "timeout explanation logged");
            if (args.Contains("--build"))
            {
                using var form = new MainForm();
                form.Show();
                var build = typeof(MainForm).GetMethod("build_Click", BindingFlags.NonPublic | BindingFlags.Instance)!;
                for (int i = 1; i <= 2; i++)
                {
                    timer.Restart();
                    build.Invoke(form, new object[] { form, EventArgs.Empty });
                    string script = File.ReadAllText(Path.Combine(data, "zelda3", "radzprower.bat"));
                    Require(!script.Split('\n').Any(l => l.Trim().Equals("pause", StringComparison.OrdinalIgnoreCase)), "all PAUSE lines stripped");
                    Require(script.Split("Building with TCC...").Length == 2, "one build block after retry " + i);
                    Require(File.Exists(Path.Combine(data, "zelda3", "zelda3.exe")), "game executable exists after rebuild " + i);
                    var launch = (Button)typeof(MainForm).GetField("launch", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
                    Require(launch.Enabled, "launch button enabled after rebuild " + i);
                    Console.WriteLine($"Rebuild {i}: {timer.Elapsed.TotalSeconds:F2}s");
                }
                form.Close();
            }
            using (var settings = new settingsForm())
            {
                Require(!settings.IsDisposed, "settings dialog initializes");
            }
            return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}
