using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace HeadsetButtonBridge
{
    public sealed class BridgeContext : ApplicationContext
    {
        readonly DeviceWindow window = new DeviceWindow();
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly NotifyIcon tray;
        readonly ToolStripMenuItem status, startupItem;
        readonly System.Windows.Forms.Timer confirmTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer reconnectTimer = new System.Windows.Forms.Timer();
        readonly EventWaitHandle setupEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.SetupEventName);
        readonly EventWaitHandle stopEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Program.StopEventName);
        readonly RegisteredWaitHandle setupWait, stopWait;
        BridgeSettings settings;
        HidDevice device;
        ReportDetector detector;
        SetupWizard wizard;
        bool paused, exiting;
        int generation, queued;
        long pressCount;
        public BridgeContext(bool showSetup, bool background)
        {
            AppLog.Write("Application started");
            status = new ToolStripMenuItem("Starting...") { Enabled = false };
            startupItem = new ToolStripMenuItem("Start with Windows") { CheckOnClick = true };
            startupItem.Checked = Startup.IsEnabled();
            startupItem.CheckedChanged += delegate
            {
                try { Startup.Set(startupItem.Checked); if (settings != null) { settings.StartWithWindows = startupItem.Checked; SettingsStore.Save(settings); } }
                catch (Exception ex) { AppLog.Write("Startup option: " + ex.Message); MessageBox.Show(ex.Message, "Startup setting"); }
            };
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(status);
            menu.Items.Add("Calibrate / change shortcut...", null, delegate { OpenSetup(); });
            ToolStripMenuItem pause = new ToolStripMenuItem("Pause") { CheckOnClick = true };
            pause.CheckedChanged += delegate { paused = pause.Checked; if (detector != null) detector.Cancel(); confirmTimer.Stop(); UpdateStatus(); };
            menu.Items.Add(pause); menu.Items.Add(startupItem); menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            tray = new NotifyIcon { Icon = SystemIcons.Information, Text = "Headset Button Bridge", ContextMenuStrip = menu, Visible = true };
            tray.DoubleClick += delegate { OpenSetup(); };
            confirmTimer.Tick += delegate { confirmTimer.Stop(); if (detector != null && detector.Advance(clock.ElapsedMilliseconds)) Fire(); ScheduleCandidate(); };
            reconnectTimer.Interval = 750;
            reconnectTimer.Tick += delegate { reconnectTimer.Stop(); Connect(); };
            window.DevicesChanged += delegate { reconnectTimer.Stop(); reconnectTimer.Start(); };
            setupWait = ThreadPool.RegisterWaitForSingleObject(setupEvent, delegate { Dispatch(OpenSetup); }, null, Timeout.Infinite, false);
            stopWait = ThreadPool.RegisterWaitForSingleObject(stopEvent, delegate { Dispatch(ExitThread); }, null, Timeout.Infinite, false);
            try { settings = SettingsStore.Load(); }
            catch (Exception ex) { AppLog.Write("Settings require calibration: " + ex.Message); }
            Connect();
            if (settings == null || showSetup || !background) window.BeginInvoke(new Action(OpenSetup));
        }
        void Dispatch(Action action)
        {
            if (exiting || window.IsDisposed) return;
            try { window.BeginInvoke(action); } catch (InvalidOperationException) { }
        }
        public void OpenSetup()
        {
            AppLog.Write("Opening setup");
            if (exiting) return;
            if (wizard != null && !wizard.IsDisposed) { wizard.Hide(); wizard.Show(); wizard.Activate(); return; }
            Disconnect();
            wizard = new SetupWizard(settings);
            AppLog.Write("Setup ready");
            wizard.FormClosed += delegate
            {
                SetupWizard closed = wizard;
                wizard = null;
                if (closed.Result != null)
                {
                    settings = closed.Result;
                    startupItem.Checked = settings.StartWithWindows;
                }
                Connect();
            };
            wizard.Show();
        }
        void Disconnect()
        {
            generation++;
            confirmTimer.Stop(); detector = null;
            if (device != null) { device.Dispose(); device = null; }
        }
        void Connect()
        {
            if (exiting || wizard != null) return;
            Disconnect();
            if (settings == null) { status.Text = "Calibration required"; return; }
            try
            {
                DeviceInfo info = HidDevice.Discover().FirstOrDefault(x => x.Matches(settings));
                if (info == null) { status.Text = "Waiting for PRO X Wireless receiver"; return; }
                int current = generation;
                detector = new ReportDetector(settings, clock.ElapsedMilliseconds, 2000);
                device = new HidDevice(info);
                device.Report += delegate(byte[] data)
                {
                    long timestamp = clock.ElapsedMilliseconds;
                    if (Interlocked.Increment(ref queued) > 128) { Interlocked.Decrement(ref queued); return; }
                    Dispatch(delegate
                    {
                        Interlocked.Decrement(ref queued);
                        if (current != generation || detector == null || paused) return;
                        if (detector.Advance(timestamp)) Fire();
                        detector.Feed(data, timestamp);
                        ScheduleCandidate();
                    });
                };
                device.Disconnected += delegate(string reason) { Dispatch(delegate { if (current != generation) return; Disconnect(); status.Text = "Receiver disconnected"; AppLog.Write(reason); }); };
                device.Start();
                UpdateStatus();
            }
            catch (Exception ex) { status.Text = "Could not connect; reconnect receiver"; AppLog.Write("Connect: " + ex.Message); }
        }
        void ScheduleCandidate()
        {
            confirmTimer.Stop();
            if (detector == null || !detector.DueAt.HasValue) return;
            confirmTimer.Interval = (int)Math.Max(1, detector.DueAt.Value - clock.ElapsedMilliseconds);
            confirmTimer.Start();
        }
        void Fire()
        {
            if (paused || wizard != null || settings == null) return;
            try { KeySender.Send(settings); pressCount++; UpdateStatus(); }
            catch (Exception ex) { status.Text = ex.Message; AppLog.Write("Shortcut: " + ex.Message); }
        }
        void UpdateStatus()
        {
            status.Text = paused ? "Paused" : settings == null ? "Calibration required" : "Listening: " + settings.ShortcutLabel + "  (" + pressCount + " presses)";
            tray.Text = paused ? "Headset Button Bridge - paused" : "Headset button to " + (settings == null ? "shortcut" : settings.ShortcutLabel);
        }
        protected override void ExitThreadCore()
        {
            if (exiting) return;
            exiting = true;
            setupWait.Unregister(null); stopWait.Unregister(null);
            if (wizard != null) wizard.Close();
            Disconnect();
            reconnectTimer.Stop(); reconnectTimer.Dispose(); confirmTimer.Dispose();
            tray.Visible = false; tray.Dispose();
            setupEvent.Dispose(); stopEvent.Dispose(); window.Dispose();
            base.ExitThreadCore();
        }
    }
}
