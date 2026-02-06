namespace KeyedColors;

using System;
using System.Windows.Forms;
using KeyedColors.Services.Logging;
using KeyedColors.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            Logger.Initialize("keyedcolors.log");
            Logger.Log("Application starting...");
            Logger.Log($"Base directory: {AppDomain.CurrentDomain.BaseDirectory}");
            Logger.Log($"Executable path: {Application.ExecutablePath}");

            ApplicationConfiguration.Initialize();
            Logger.Log("Configuration initialized");

            Application.Run(new MainForm());

            Logger.Log("Application closed normally");
        }
        catch (Exception ex)
        {
            Logger.LogError("Fatal application error", ex);

            MessageBox.Show(
                $"The application encountered an error and needs to close.\r\nError details have been saved to:\r\n{Logger.LogPath}",
                "KeyedColors Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
