using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using IntelVibrance.Core;
using IntelVibrance.UI;

namespace IntelVibrance
{
    /// <summary>
    /// Application entry point.
    /// Handles: single-instance check, crash safety, recovery on start.
    /// </summary>
    static class Program
    {
        // Mutex for single-instance enforcement
        private static System.Threading.Mutex _mutex;

        [STAThread]
        static void Main(string[] args)
        {
            // ── Single-instance check ──────────────────────────────
            bool createdNew;
            _mutex = new System.Threading.Mutex(true, "IntelVibrance_SingleInstance", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    "Intel Vibrance is already running.\nLook for the icon in the system tray.",
                    "Already Running",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // ── Crash safety: emergency restore ───────────────────
            // If the app crashed previously without restoring, attempt recovery
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // ── Check for --startup flag ───────────────────────────
            bool startMinimized = Array.IndexOf(args, "--startup") >= 0;

            // ── Initialize ────────────────────────────────────────
            Logger.Initialize();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Note: Application.SetColorMode is .NET 7+ only, not available in .NET Framework 4.8

            Logger.Log("[INFO] Starting main window");

            var mainWindow = new MainWindow();
            if (startMinimized)
                mainWindow.WindowState = FormWindowState.Minimized;

            Application.Run(mainWindow);

            _mutex.ReleaseMutex();
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Logger.Log($"[CRASH] Unhandled exception: {e.ExceptionObject}");
            Logger.Log("[CRASH] Attempting emergency display restore...");

            // Emergency restore: reset gamma ramp on all displays
            VibranceController.EmergencyRestore();

            Logger.Log("[CRASH] Emergency restore attempted. Application terminating.");
        }
    }
}
