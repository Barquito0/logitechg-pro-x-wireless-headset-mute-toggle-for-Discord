using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace HeadsetButtonBridge
{
    public sealed class SetupWizard : Form
    {
        readonly BridgeSettings settings;
        readonly bool hadCalibration;
        readonly Stopwatch clock = Stopwatch.StartNew();
        readonly List<List<string>> captures = new List<List<string>>();
        readonly Panel content = new Panel();
        readonly Label stepLabel = new Label(), heading = new Label(), intro = new Label(), feedback = new Label();
        readonly Button next = new Button(), back = new Button(), captureButton = new Button(), volumeButton = new Button();
        readonly TextBox trace = new TextBox();
        readonly System.Windows.Forms.Timer captureTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer volumeTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer candidateTimer = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer sendTimer = new System.Windows.Forms.Timer();
        ComboBox devices, keys;
        CheckBox control, alt, shift, startup;
        HidDevice device;
        DeviceInfo selected;
        List<string> capturing;
        ReportDetector testDetector;
        int page, volumeFalseTriggers, reportsQueued;
        bool firstCaptureReport, volumeRunning, sawVolumeActivity, volumePassed, buttonVerified, closing;
        public BridgeSettings Result { get; private set; }

        public SetupWizard(BridgeSettings previous)
        {
            settings = previous == null ? new BridgeSettings() : previous.Copy();
            hadCalibration = previous != null;
            Text = "Headset Button Bridge - Setup";
            Font = new Font("Segoe UI", 10F);
            BackColor = Color.FromArgb(248, 249, 252);
            ForeColor = Color.FromArgb(25, 32, 45);
            ClientSize = new Size(760, 650);
            MinimumSize = new Size(720, 660);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            AccessibleName = "Headset Button Bridge setup";
            Icon = SystemIcons.Information;
            TableLayoutPanel layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(30, 22, 30, 18) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            stepLabel.Dock = DockStyle.Fill; stepLabel.ForeColor = Color.FromArgb(85, 95, 115);
            heading.Dock = DockStyle.Fill; heading.Font = new Font("Segoe UI", 19F, FontStyle.Bold);
            intro.Dock = DockStyle.Fill;
            content.Dock = DockStyle.Fill; content.AutoScroll = true;
            FlowLayoutPanel actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            next.Text = "Next"; next.Size = new Size(150, 36); next.BackColor = Color.FromArgb(61, 78, 200); next.ForeColor = Color.White; next.FlatStyle = FlatStyle.Flat;
            next.Click += delegate { if (page == 3) Finish(); else ShowPage(page + 1); };
            back.Text = "Back"; back.Size = new Size(90, 36); back.Click += delegate { ShowPage(page - 1); };
            Button cancel = new Button { Text = "Cancel", Size = new Size(90, 36) }; cancel.Click += delegate { Close(); };
            actions.Controls.Add(next); actions.Controls.Add(back); actions.Controls.Add(cancel);
            layout.Controls.Add(stepLabel, 0, 0); layout.Controls.Add(heading, 0, 1); layout.Controls.Add(intro, 0, 2); layout.Controls.Add(content, 0, 3); layout.Controls.Add(actions, 0, 4);
            Controls.Add(layout);
            AcceptButton = next; CancelButton = cancel;
            feedback.AutoSize = false; feedback.Size = new Size(650, 90); feedback.ForeColor = Color.FromArgb(45, 67, 128);
            trace.Multiline = true; trace.ReadOnly = true; trace.ScrollBars = ScrollBars.Vertical; trace.Font = new Font("Consolas", 9F); trace.Size = new Size(650, 112); trace.TabStop = false;
            captureButton.Size = new Size(270, 38); captureButton.Click += delegate { BeginCapture(); };
            volumeButton.Size = new Size(270, 38); volumeButton.Click += delegate { BeginVolumeCheck(); };
            captureTimer.Tick += delegate { captureTimer.Stop(); EndCapture(); };
            volumeTimer.Tick += delegate { volumeTimer.Stop(); EndVolumeCheck(); };
            candidateTimer.Tick += delegate { candidateTimer.Stop(); if (testDetector != null && testDetector.Advance(clock.ElapsedMilliseconds)) TestTrigger(); ScheduleTestCandidate(); };
            sendTimer.Interval = 3000;
            sendTimer.Tick += delegate { sendTimer.Stop(); try { ReadShortcut(); KeySender.Send(settings); SetFeedback("Shortcut sent. If the target app was recording, stop recording there.", false); } catch (Exception ex) { SetFeedback(ex.Message, true); } };
            ShowPage(0);
        }
        FlowLayoutPanel Stack()
        {
            FlowLayoutPanel stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            content.Controls.Add(stack);
            return stack;
        }
        Label Note(string text, int height) { return new Label { Text = text, Size = new Size(650, height), Margin = new Padding(0, 6, 0, 8) }; }
        void ShowPage(int value)
        {
            if (page == 0 && devices != null && value == 1) selected = devices.SelectedItem as DeviceInfo;
            captureTimer.Stop(); volumeTimer.Stop(); candidateTimer.Stop(); sendTimer.Stop();
            capturing = null; volumeRunning = false; testDetector = null;
            foreach (Control shared in new Control[] { feedback, trace, captureButton, volumeButton })
                if (shared.Parent != null) shared.Parent.Controls.Remove(shared);
            foreach (Control old in content.Controls.Cast<Control>().ToArray()) old.Dispose();
            content.Controls.Clear(); page = value;
            stepLabel.Text = "STEP " + (page + 1) + " OF 4     /     " + new string[] { "CONNECT", "LEARN THE BUTTON", "CHECK THE CONTROLS", "CHOOSE A SHORTCUT" }[page];
            back.Enabled = page > 0; next.Text = page == 3 ? "Save and run" : "Next"; next.Enabled = true;
            FlowLayoutPanel stack = Stack();
            if (page == 0)
            {
                CloseDevice();
                heading.Text = "Connect your headset";
                intro.Text = "Plug in the USB receiver and turn on your Logitech PRO X Wireless. This release supports the original model (046D:0ABA).";
                devices = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 650, AccessibleName = "Headset receiver" };
                Button refresh = new Button { Text = "Refresh devices", Size = new Size(180, 36) };
                refresh.Click += delegate { RefreshDevices(); };
                stack.Controls.Add(devices); stack.Controls.Add(refresh);
                stack.Controls.Add(Note("Setup listens only to the headset's control messages. It does not record audio, install a driver, or change your microphone settings.", 65));
                stack.Controls.Add(feedback);
                devices.SelectedIndexChanged += delegate { next.Enabled = devices.SelectedItem != null; };
                RefreshDevices();
                if (hadCalibration)
                {
                    Button keep = new Button { Text = "Keep calibration and change shortcut", Size = new Size(330, 36) };
                    keep.Click += delegate { ShowPage(3); };
                    stack.Controls.Add(keep);
                }
            }
            else if (page == 1)
            {
                heading.Text = "Learn your mute button";
                intro.Text = "Click Capture, then press the headset mute button once. Repeat for three samples. Leave the volume wheel still while recording.";
                if (device == null && !OpenSelected()) { ShowPage(0); return; }
                captures.Clear(); settings.PassedVolumeCheck = false; volumePassed = false; buttonVerified = false;
                captureButton.Text = "Capture press 1 of 3"; captureButton.Enabled = true;
                next.Enabled = false; trace.Clear();
                stack.Controls.Add(captureButton); stack.Controls.Add(feedback);
                stack.Controls.Add(Note("Live device events appear below. Seeing a report confirms Windows can read a message from your receiver.", 48));
                stack.Controls.Add(trace);
                SetFeedback("Ready. Each capture waits up to 8 seconds for your press.", false);
            }
            else if (page == 2)
            {
                heading.Text = "Make sure volume stays volume";
                intro.Text = "Start the 8-second check, then turn the headset volume wheel up and down. Do not press mute during this check.";
                volumeButton.Text = "Start volume check"; volumeButton.Enabled = true;
                next.Enabled = false; trace.Clear(); volumePassed = false; buttonVerified = false;
                stack.Controls.Add(volumeButton); stack.Controls.Add(feedback); stack.Controls.Add(trace);
                stack.Controls.Add(Note("After the volume check passes, press mute once more to verify the learned button. No shortcut is sent during calibration.", 65));
                SetFeedback("We will reject a calibration that also triggers from the volume wheel.", false);
            }
            else
            {
                CloseDevice();
                heading.Text = "Give the button a shortcut";
                intro.Text = "F24 is recommended: Windows supports it, and most keyboards and games leave it unused. Add modifiers only if the target app needs them.";
                FlowLayoutPanel row = new FlowLayoutPanel { Size = new Size(650, 42), WrapContents = false };
                keys = new ComboBox { Width = 150, DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "Shortcut key" };
                for (int key = 0x7C; key <= 0x87; key++) keys.Items.Add(new KeyChoice(key));
                for (int key = 0x70; key <= 0x7B; key++) keys.Items.Add(new KeyChoice(key));
                keys.Items.Add(new KeyChoice(0x13)); keys.Items.Add(new KeyChoice(0x91));
                for (int i = 0; i < keys.Items.Count; i++) if (((KeyChoice)keys.Items[i]).Key == settings.VirtualKey) keys.SelectedIndex = i;
                control = new CheckBox { Text = "Ctrl", AutoSize = true, Checked = settings.Control };
                alt = new CheckBox { Text = "Alt", AutoSize = true, Checked = settings.Alt };
                shift = new CheckBox { Text = "Shift", AutoSize = true, Checked = settings.Shift };
                row.Controls.Add(keys); row.Controls.Add(control); row.Controls.Add(alt); row.Controls.Add(shift); stack.Controls.Add(row);
                startup = new CheckBox { Text = "Start quietly when I sign in to Windows", AutoSize = true, Checked = settings.StartWithWindows, Margin = new Padding(0, 10, 0, 8) };
                stack.Controls.Add(startup);
                stack.Controls.Add(Note("Discord: Settings > System > Custom Keybinds > Add a Keybind > Toggle Mute. After saving here, record the shortcut there and press the headset button.", 67));
                Button send = new Button { Text = "Send shortcut in 3 seconds", Size = new Size(270, 36) };
                send.Click += delegate { ReadShortcut(); sendTimer.Stop(); sendTimer.Start(); SetFeedback("Switch to the target app's shortcut recorder now. Sending in 3 seconds...", false); };
                stack.Controls.Add(send);
                stack.Controls.Add(Note("Each press toggles the app; it does not synchronize the headset's physical mute state. Leave about one second after adjusting volume before pressing mute.", 65));
                stack.Controls.Add(feedback);
                SetFeedback("Saving keeps the bridge in the system tray. Right-click its icon to pause, recalibrate, change startup, or exit.", false);
            }
        }
        sealed class KeyChoice
        {
            public readonly int Key;
            public KeyChoice(int key) { Key = key; }
            public override string ToString() { return Shortcut.Label(Key, false, false, false); }
        }
        void RefreshDevices()
        {
            devices.Items.Clear();
            try
            {
                foreach (DeviceInfo info in HidDevice.Discover()) devices.Items.Add(info);
                if (devices.Items.Count > 0) { devices.SelectedIndex = 0; SetFeedback("Receiver detected. Continue to test its button.", false); }
                else SetFeedback("No supported receiver found. Plug in your PRO X Wireless USB receiver, then refresh.", true);
                next.Enabled = devices.Items.Count > 0;
            }
            catch (Exception ex) { next.Enabled = false; SetFeedback(ex.Message, true); }
        }
        bool OpenSelected()
        {
            if (selected == null) return false;
            try
            {
                device = new HidDevice(selected);
                device.Report += delegate(byte[] report)
                {
                    long time = clock.ElapsedMilliseconds;
                    if (System.Threading.Interlocked.Increment(ref reportsQueued) > 128) { System.Threading.Interlocked.Decrement(ref reportsQueued); return; }
                    if (closing || IsDisposed || !IsHandleCreated) { System.Threading.Interlocked.Decrement(ref reportsQueued); return; }
                    try { BeginInvoke(new Action(delegate { System.Threading.Interlocked.Decrement(ref reportsQueued); if (!closing) OnReport(report, time); })); }
                    catch (InvalidOperationException) { System.Threading.Interlocked.Decrement(ref reportsQueued); }
                };
                device.Disconnected += delegate(string reason)
                {
                    if (closing || IsDisposed || !IsHandleCreated) return;
                    try { BeginInvoke(new Action(delegate { if (closing) return; next.Enabled = false; captureTimer.Stop(); volumeTimer.Stop(); candidateTimer.Stop(); capturing = null; volumeRunning = false; SetFeedback(reason + " Go Back and reconnect before continuing.", true); })); }
                    catch (InvalidOperationException) { }
                };
                device.Start();
                settings.ReportLength = selected.ReportLength;
                return true;
            }
            catch (Exception ex) { SetFeedback(ex.Message, true); CloseDevice(); return false; }
        }
        void BeginCapture()
        {
            if (captures.Count >= 3) return;
            capturing = new List<string>(); firstCaptureReport = true;
            captureButton.Enabled = false;
            captureTimer.Interval = 8000; captureTimer.Start();
            SetFeedback("Listening for press " + (captures.Count + 1) + ". Press the headset mute button once now.", false);
        }
        void OnReport(byte[] report, long timestamp)
        {
            string hex = BitConverter.ToString(report);
            if (page == 1 || page == 2)
            {
                if (trace.Lines.Length >= 20) trace.Lines = trace.Lines.Skip(1).ToArray();
                trace.AppendText(DateTime.Now.ToString("HH:mm:ss.fff") + "   " + hex + Environment.NewLine);
            }
            if (page == 1 && capturing != null)
            {
                if (capturing.Count < 128) capturing.Add(hex);
                else { captureTimer.Stop(); capturing = null; captureButton.Enabled = true; SetFeedback("Too much activity. Keep other controls still and capture again.", true); return; }
                if (firstCaptureReport) { firstCaptureReport = false; captureTimer.Stop(); captureTimer.Interval = 350; captureTimer.Start(); }
            }
            if (page == 2 && testDetector != null)
            {
                if (hex != settings.Pattern) sawVolumeActivity = true;
                if (testDetector.Advance(timestamp)) TestTrigger();
                testDetector.Feed(report, timestamp);
                ScheduleTestCandidate();
            }
        }
        void EndCapture()
        {
            captureButton.Enabled = true;
            if (capturing == null || capturing.Count == 0) { capturing = null; SetFeedback("No event arrived. Make sure the headset is on and try again. Some devices do not expose the button.", true); return; }
            captures.Add(capturing); capturing = null;
            if (captures.Count < 3) { captureButton.Text = "Capture press " + (captures.Count + 1) + " of 3"; SetFeedback("Sample " + captures.Count + " recorded. Wait one second, then capture the next press.", false); return; }
            try
            {
                settings.Pattern = Calibration.ChoosePattern(captures);
                captureButton.Text = "3 matching presses recorded"; captureButton.Enabled = false;
                SetFeedback("Windows recognized the same event in all three captures: " + settings.Pattern + ". Continue to check the volume wheel.", false);
                next.Enabled = true;
            }
            catch (Exception ex) { captures.Clear(); captureButton.Text = "Capture press 1 of 3"; SetFeedback(ex.Message, true); }
        }
        void BeginVolumeCheck()
        {
            volumeRunning = true; volumePassed = false; buttonVerified = false; volumeFalseTriggers = 0; sawVolumeActivity = false;
            testDetector = new ReportDetector(settings, clock.ElapsedMilliseconds, 0);
            volumeButton.Enabled = false; next.Enabled = false;
            volumeTimer.Interval = 8000; volumeTimer.Start();
            SetFeedback("Checking for 8 seconds: turn volume up and down now. Leave the mute button alone.", false);
        }
        void ScheduleTestCandidate()
        {
            candidateTimer.Stop();
            if (testDetector == null || !testDetector.DueAt.HasValue) return;
            candidateTimer.Interval = (int)Math.Max(1, testDetector.DueAt.Value - clock.ElapsedMilliseconds);
            candidateTimer.Start();
        }
        void TestTrigger()
        {
            if (volumeRunning) { volumeFalseTriggers++; return; }
            if (volumePassed)
            {
                buttonVerified = true; settings.PassedVolumeCheck = true; next.Enabled = true;
                SetFeedback("Verified: mute recognized and volume controls filtered. Continue to choose your shortcut.", false);
            }
        }
        void EndVolumeCheck()
        {
            if (testDetector != null && testDetector.Advance(clock.ElapsedMilliseconds)) TestTrigger();
            volumeRunning = false; volumeButton.Enabled = true;
            if (!sawVolumeActivity || volumeFalseTriggers != 0)
            {
                testDetector = null; candidateTimer.Stop();
                SetFeedback(!sawVolumeActivity ? "No separate volume activity was detected. Run the check again and turn the wheel both ways." : "The learned event also triggered during volume activity. Go Back and calibrate again; this mapping is not safe to enable.", true);
                return;
            }
            volumePassed = true;
            testDetector = new ReportDetector(settings, clock.ElapsedMilliseconds, 1000);
            SetFeedback("Volume check passed. Wait one second, then press the mute button once to verify it still registers.", false);
        }
        void ReadShortcut()
        {
            settings.VirtualKey = ((KeyChoice)keys.SelectedItem).Key;
            settings.Control = control.Checked; settings.Alt = alt.Checked; settings.Shift = shift.Checked;
        }
        void Finish()
        {
            try
            {
                if (!hadCalibration && !buttonVerified) throw new InvalidOperationException("Verify the calibrated button before saving.");
                ReadShortcut(); settings.StartWithWindows = startup.Checked;
                settings.Validate(); SettingsStore.Save(settings); Startup.Set(settings.StartWithWindows);
                Result = settings; Close();
            }
            catch (Exception ex) { SetFeedback("Could not save setup: " + ex.Message, true); }
        }
        void SetFeedback(string message, bool error)
        {
            feedback.Text = message;
            feedback.ForeColor = error ? Color.FromArgb(163, 45, 42) : Color.FromArgb(45, 67, 128);
            feedback.AccessibleName = message;
        }
        void CloseDevice() { if (device != null) { device.Dispose(); device = null; } }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            closing = true;
            captureTimer.Stop(); volumeTimer.Stop(); candidateTimer.Stop(); sendTimer.Stop();
            CloseDevice();
            captureTimer.Dispose(); volumeTimer.Dispose(); candidateTimer.Dispose(); sendTimer.Dispose();
            base.OnFormClosed(e);
        }
    }
}
