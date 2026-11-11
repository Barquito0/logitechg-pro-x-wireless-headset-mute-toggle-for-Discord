using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Headset Button Bridge")]
[assembly: AssemblyProduct("Headset Button Bridge")]
[assembly: AssemblyDescription("Calibrate a PRO X Wireless headset button and turn its events into a shortcut.")]
[assembly: AssemblyCompany("Barquito0")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace HeadsetButtonBridge
{
    public static class Program
    {
        internal const string MutexName = "Local\\HeadsetButtonBridge.v1";
        internal const string SetupEventName = "Local\\HeadsetButtonBridge.Setup.v1";
        internal const string StopEventName = "Local\\HeadsetButtonBridge.Stop.v1";
        [STAThread] public static int Main(string[] args)
        {
            bool stop = Array.IndexOf(args, "--stop") >= 0 || Array.IndexOf(args, "--uninstall") >= 0;
            if (Array.IndexOf(args, "--uninstall") >= 0) Startup.Set(false);
            if (stop) { Signal(StopEventName); return 0; }
            bool created;
            using (Mutex single = new Mutex(true, MutexName, out created))
            {
                if (!created) { if (Array.IndexOf(args, "--background") < 0) Signal(SetupEventName); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    AppLog.Write("UI error: " + e.Exception.GetType().Name + ": " + e.Exception.Message);
                    MessageBox.Show(e.Exception.Message, "Headset Button Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                try { using (BridgeContext context = new BridgeContext(Array.IndexOf(args, "--setup") >= 0, Array.IndexOf(args, "--background") >= 0)) Application.Run(context); }
                catch (Exception error) { AppLog.Write("Startup error: " + error.Message); MessageBox.Show(error.Message, "Headset Button Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            }
            return 0;
        }
        static void Signal(string name)
        {
            try { using (EventWaitHandle handle = EventWaitHandle.OpenExisting(name)) handle.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
        }
    }
}
