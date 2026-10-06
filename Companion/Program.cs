namespace ZeldaCompanion;
static class Program
{
    [STAThread] static void Main() {
        ApplicationConfiguration.Initialize();
        try { Application.Run(new MainForm()); }
        catch(Exception e) { MessageBox.Show("The launcher could not start. Extract the whole ZIP to a writable folder and try again.\n\n"+e.Message,"Zelda Companion — startup problem",MessageBoxButtons.OK,MessageBoxIcon.Error); }
    }
}
