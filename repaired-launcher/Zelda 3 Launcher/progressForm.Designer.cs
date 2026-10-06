using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.IO.Compression;
using LibGit2Sharp;

namespace Zelda_3_Launcher
{
    partial class progressForm : Form
    {
        public progressForm(string title, string message)
        {
            InitializeComponent();
            Text = title;
            updateLabel.Text = message;
            Shown += async (_, _) =>
            {
                try
                {
                    switch (title)
                    {
                        case "Repository Download":
                            await CloneRepository();
                            break;
                        case "Copying ROM File":
                            copyROM(this, EventArgs.Empty);
                            if (!File.Exists(Path.Combine(Program.repoDir, "zelda3.sfc"))) return;
                            break;
                        case "Downloading TCC":
                            await DownloadZip("TCC.zip", new Uri("https://github.com/FitzRoyX/tinycc/releases/download/tcc_20230519/tcc_20230519.zip"));
                            break;
                        case "Downloading SDL2":
                            // Use the version requested by the checked-out game, not a stale launcher constant.
                            string bat = File.ReadAllText(Path.Combine(Program.repoDir, "run_with_tcc.bat"));
                            var version = Regex.Match(bat, @"(?im)^set SDL2=third_party\\SDL2-([0-9]+\.[0-9]+\.[0-9]+)\s*$");
                            if (!version.Success) throw new IOException("Cannot identify the SDL version required by run_with_tcc.bat.");
                            string v = version.Groups[1].Value;
                            await DownloadZip("SDL2.zip", new Uri($"https://github.com/libsdl-org/SDL/releases/download/release-{v}/SDL2-devel-{v}-VC.zip"));
                            break;
                        case "Downloading Python":
                            await DownloadZip("Python.zip", new Uri("https://www.python.org/ftp/python/3.11.1/python-3.11.1-embed-amd64.zip"));
                            break;
                        case "Downloading pip":
                            await DownloadFile(new Uri("https://bootstrap.pypa.io/get-pip.py"), Path.Combine(Program.repoDir, "assets", "get-pip.py"));
                            break;
                    }
                    DialogResult = DialogResult.OK;
                }
                catch (Exception ex)
                {
                    File.AppendAllText(Path.Combine(Program.currentDirectory, "zelda3.log"), $"\n{DateTime.Now:O} {title}: {ex}\n");
                    MessageBox.Show(this, ex.Message + "\n\nFull log: " + Path.Combine(Program.currentDirectory, "zelda3.log"),
                        title + " failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    DialogResult = DialogResult.Abort;
                }
                finally { Close(); }
            };
        }

        private async Task CloneRepository()
        {
            progBar.Style = ProgressBarStyle.Marquee;
            await Task.Run(() =>
            {
                if (!Repository.IsValid(Program.repoDir))
                {
                    if (Directory.Exists(Program.repoDir) && Directory.EnumerateFileSystemEntries(Program.repoDir).Any())
                        throw new IOException("The zelda3 folder is not a valid Git checkout. Your files have been preserved.");
                    Repository.Clone("https://github.com/snesrev/zelda3.git", Program.repoDir);
                }
                else
                {
                    using var repo = new Repository(Program.repoDir);
                    var remote = repo.Network.Remotes["origin"];
                    Commands.Fetch(repo, remote.Name, remote.FetchRefSpecs.Select(r => r.Specification), new FetchOptions(), null);
                    var tracked = repo.Head.TrackedBranch ?? throw new IOException("Game source has no tracked branch.");
                    // Preserve local changes; a conflicting update fails visibly instead of resetting them.
                    repo.Merge(tracked, new Signature("Local Zelda Launcher", "local@localhost", DateTimeOffset.Now),
                        new MergeOptions { FastForwardStrategy = FastForwardStrategy.FastForwardOnly });
                }
                Directory.CreateDirectory(Path.Combine(Program.repoDir, "saves"));
            });
        }

        private async Task DownloadFile(Uri uri, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            string temporary = destination + ".download";
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var client = new HttpClient();
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            response.EnsureSuccessStatusCode();
            long? total = response.Content.Headers.ContentLength;
            progBar.Style = total.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            progBar.Maximum = 100;
            progBar.Value = 0;
            try
            {
                await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
                await using (var output = File.Create(temporary))
                {
                    byte[] buffer = new byte[65536];
                    long received = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, count), timeout.Token);
                        received += count;
                        if (total > 0) progBar.Value = (int)Math.Min(100, received * 100 / total.Value);
                    }
                }
                File.Move(temporary, destination, true);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private async Task DownloadZip(string filename, Uri uri)
        {
            string zip = Path.Combine(Program.third_partyDir, filename);
            await DownloadFile(uri, zip);
            updateLabel.Text = "Extracting " + filename + "...";
            await Task.Run(() => ZipFile.ExtractToDirectory(zip,
                filename == "Python.zip" ? Path.Combine(Program.repoDir, "assets") : Program.third_partyDir, true));
            File.Delete(zip);
        }

        private void copyROM(object sender, EventArgs e)
        {
            this.Refresh();

            if (File.Exists(Path.Combine(Program.repoDir, "zelda3.sfc")))
            {
                this.Close();
                return;
            }

            if (File.Exists(Path.Combine(Program.currentDirectory, "zelda3.sfc")))
            {
                File.Copy(Path.Combine(Program.currentDirectory, "zelda3.sfc"), Path.Combine(Program.repoDir, "zelda3.sfc"));
                this.Close();
                return;
            }

            Boolean exit = false;
            var result = new OpenFileDialog();
            result.Filter = "Zelda 3 ROM (*.sfc)|*.sfc";
            while (!exit)
            {
                if (result.ShowDialog() == DialogResult.OK)
                {
                    var hashCheck = checkHash(result.FileName);
                    if (hashCheck.success)
                    {
                        File.Copy(result.FileName, Path.Combine(Program.repoDir, "zelda3.sfc"));
                        exit = true;
                    }
                    else
                    {
                        var answer = MessageBox.Show("ROM hash is not valid for the English (US) version.\n\n" +
                            "The hash of the file selected is " + hashCheck.yourHash + ".\n\n" + 
                            "The correct hash is " + hashCheck.hash + ".\n\n" +
                            "Would you like to select another?", "Invalid ROM Hash", MessageBoxButtons.YesNo, MessageBoxIcon.Error); ;
                        if (answer == DialogResult.No) exit = true;
                    }
                }
                else exit = true;
            }

            this.Close();
        }

        public (Boolean success, string hash, string yourHash) checkHash(string file, string version="us")
        {
            using (var sha1Hasher = SHA1.Create())
            using (FileStream stream = new FileStream(file, FileMode.Open))
            using (BufferedStream buffer = new BufferedStream(stream))
            {
                byte[] hash = sha1Hasher.ComputeHash(buffer);
                StringBuilder hashString = new StringBuilder(2 * hash.Length);
                foreach (byte b in hash)
                {
                    hashString.AppendFormat("{0:x2}", b);
                }

                string versionHash;
                var yourHash = hashString.ToString().ToUpper();

                switch (version)
                {
                    case "us":
                        versionHash = "6D4F10A8B10E10DBE624CB23CF03B88BB8252973";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "de":
                        versionHash = "2E62494967FB0AFDF5DA1635607F9641DF7C6559";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "fr":
                        versionHash = "229364A1B92A05167CD38609B1AA98F7041987CC";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "fr-c":
                        versionHash = "C1C6C7F76FFF936C534FF11F87A54162FC0AA100";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "en":
                        versionHash = "7C073A222569B9B8E8CA5FCB5DFEC3B5E31DA895";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "es":
                        versionHash = "461FCBD700D1332009C0E85A7A136E2A8E4B111E";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "pl":
                        versionHash = "3C4D605EEFDA1D76F101965138F238476655B11D";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "pt":
                        versionHash = "D0D09ED41F9C373FE6AFDCCAFBF0DA8C88D3D90D";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "redux":
                        versionHash = "B2A07A59E64C498BC1B2F28728F9BF4014C8D582";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        versionHash = "9325C22EB0A2A1F0017157C8B620BC3A605CEDE1";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "nl":
                        versionHash = "FA8ADFDBA2697C9A54D583A1284A22AC764C7637";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                    case "sv":
                        versionHash = "43CD3438469B2C3FE879EA2F410B3EF3CB3F1CA4";
                        if (yourHash == versionHash) return (true, versionHash, yourHash);
                        return (false, versionHash, yourHash);
                }
            }
            return (false, "NULL", "NULL");
        }

        private ProgressBar progBar;
        private Label updateLabel;
    }
}