using System;
using System.Collections.Generic;
using System.Linq;
using HeadsetButtonBridge;

public static class BridgeTests
{
    static int passed;
    static byte[] Empty = { 1, 0, 0, 0, 0 }, Up = { 1, 1, 0, 0, 0 }, Down = { 1, 2, 0, 0, 0 };
    static BridgeSettings Settings() { return new BridgeSettings { Pattern = "01-00-00-00-00", PassedVolumeCheck = true }; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); passed++; Console.WriteLine("PASS: " + name); }
    static bool Throws(Action action) { try { action(); return false; } catch { return true; } }
    public static int Main()
    {
        try
        {
            ReportDetector d = new ReportDetector(Settings(), 0, 2000);
            d.Feed(Empty, 100); Check(!d.Advance(1000), "startup messages cannot trigger shortcuts");
            d.Feed(Empty, 2500); Check(!d.Advance(2649) && d.Advance(2650), "standalone button fires after confirmation");
            Check(!d.Advance(3000), "candidate fires once");
            d.Feed(Empty, 2700); Check(!d.Advance(3000), "duplicate report is debounced");
            d.Feed(Empty, 4000); d.Feed(Up, 4050); Check(!d.Advance(5000), "volume activity cancels a pending empty report");
            d.Feed(Empty, 4100); Check(!d.Advance(5000), "volume release does not trigger");
            d.Feed(Down, 6000); d.Feed(Empty, 6100); Check(!d.Advance(7000), "volume-down release does not trigger");
            d.Feed(Empty, 8000); Check(d.Advance(8150), "button works after volume guard expires");
            d.Feed(new byte[] { 2, 0, 0, 0, 0 }, 9000); d.Feed(new byte[] { 1, 0 }, 9001); Check(!d.Advance(9500), "other report IDs and lengths ignored");
            d.Feed(Empty, 10000); d.Cancel(); Check(!d.Advance(11000), "pause cancels pending shortcut");
            // Transcribed from the actual hardware test: two mute presses,
            // followed by volume-up/down bursts and their release reports.
            long[] times = { 0, 3530, 8733, 8796, 8796, 8858, 8920, 8984, 9047, 9109, 9172, 9235, 9297, 9360, 11435, 11500, 11600, 12063 };
            byte[][] reports = { Empty, Empty, Up, Empty, Up, Up, Up, Up, Up, Up, Up, Up, Up, Empty, Down, Down, Down, Empty };
            d = new ReportDetector(Settings(), -2000, 2000);
            int count = 0;
            for (int i = 0; i < times.Length; i++) { if (d.Advance(times[i])) count++; d.Feed(reports[i], times[i]); }
            if (d.Advance(14000)) count++;
            Check(count == 2, "recorded hardware sequence: 2 mute triggers, 0 volume triggers");
            Check(!d.DueAt.HasValue, "no timer scheduled after idle");
            d = new ReportDetector(Settings(), 0, 0);
            d.OtherControlChanged(1000); d.Feed(Empty, 1015);
            Check(!d.Advance(2000), "keyboard volume notification suppresses empty headset report");
            d.Feed(Empty, 3000); d.OtherControlChanged(3040);
            Check(!d.Advance(4000), "volume callback arriving after HID report cancels candidate");
            d.OtherControlChanged(5000); d.OtherControlChanged(5010); d.Feed(Empty, 5020);
            Check(!d.Advance(6000), "duplicate playback callbacks cannot create a shortcut");
            d.Feed(Empty, 7000);
            Check(d.Advance(7150), "real mute press still works after keyboard volume guard expires");
            d.OtherControlChanged(8000); d.OtherControlChanged(7900); d.Feed(Empty, 8700);
            Check(!d.Advance(9000), "late queued callback cannot shorten the suppression window");
            // Windows-volume regression captured from the actual headset:
            // Core Audio at 173276 ms, empty HID at 173341 ms; another volume
            // change at 174285 ms, empty HID at 174339 ms.
            d = new ReportDetector(Settings(), 0, 0);
            d.OtherControlChanged(0); d.Feed(Empty, 65);
            bool keyboardFalseTrigger = d.Advance(1009);
            d.OtherControlChanged(1009); d.Feed(Empty, 1063);
            keyboardFalseTrigger |= d.Advance(2000);
            d.Feed(Empty, 3000);
            Check(!keyboardFalseTrigger && d.Advance(3150), "captured Windows-volume sequence is filtered while next mute is accepted");
            List<List<string>> captures = new List<List<string>> { new List<string> { "01-00" }, new List<string> { "01-00" }, new List<string> { "01-00" } };
            Check(Calibration.ChoosePattern(captures) == "01-00", "three matching calibration samples accepted");
            captures[2][0] = "01-01";
            Check(Throws(delegate { Calibration.ChoosePattern(captures); }), "inconsistent calibration rejected");
            captures.ForEach(x => { x.Clear(); x.Add("01-00"); x.Add("01-01"); });
            Check(Throws(delegate { Calibration.ChoosePattern(captures); }), "ambiguous calibration rejected");
            Check(Throws(delegate { new BridgeSettings().Validate(); }), "uncalibrated settings rejected");
            BridgeSettings s = Settings(); s.Validate(); Check(s.ShortcutLabel == "F24", "default shortcut is F24");
            s.Control = true; s.Alt = true; s.VirtualKey = 0x7B;
            Check(s.ShortcutLabel == "Ctrl + Alt + F12", "custom shortcut labels match key codes");
            s.VirtualKey = 0x5B; Check(Throws(s.Validate), "Windows key rejected");
            s = Settings(); s.ProductId = 0; Check(Throws(s.Validate), "unrelated HID device rejected");
            s = Settings(); s.PassedVolumeCheck = false; Check(Throws(s.Validate), "volume check required before activation");
            s = Settings(); s.Pattern = "01-XX-00-00-00"; Check(Throws(s.Validate), "malformed calibration rejected");
            Console.WriteLine("All " + passed + " checks passed."); return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }
}
