namespace Zelda_3_Launcher
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Directory.SetCurrentDirectory(currentDirectory);
            using var singleInstance = new Mutex(true, "Zelda3Launcher_" +
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(currentDirectory.ToUpperInvariant()))), out bool firstInstance);
            if (!firstInstance)
            {
                MessageBox.Show("The launcher is already running for this folder.", "Zelda 3 Launcher");
                return;
            }
            Application.Run(new MainForm());
        }

        public static string currentDirectory = AppContext.BaseDirectory;
        public static string repoDir = Path.Combine(currentDirectory, "zelda3");
        public static string third_partyDir = Path.Combine(repoDir, "third_party");
    }
}