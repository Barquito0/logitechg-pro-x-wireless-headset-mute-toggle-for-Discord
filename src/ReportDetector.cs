using System;
using System.Collections.Generic;
using System.Linq;

namespace HeadsetButtonBridge
{
    // A timestamp-driven state machine. The host schedules a one-shot timer only
    // when a possible press arrives; this class never polls the device.
    public sealed class ReportDetector
    {
        readonly byte[] pattern;
        readonly int guard, confirm, debounce;
        readonly long armedAt;
        long lastOther = -100000, lastFire = -100000;
        public long? DueAt { get; private set; }
        public ReportDetector(BridgeSettings settings, long now, int warmupMs)
        {
            pattern = settings.PatternBytes();
            guard = settings.OtherControlGuardMs; confirm = settings.ConfirmDelayMs; debounce = settings.DebounceMs;
            armedAt = now + warmupMs;
        }
        public void Feed(byte[] data, long now)
        {
            if (data.Length != pattern.Length || data[0] != pattern[0]) return;
            if (!data.SequenceEqual(pattern)) { lastOther = now; DueAt = null; return; }
            if (now < armedAt || now - lastOther < guard || now - lastFire < debounce) return;
            if (!DueAt.HasValue) DueAt = now + confirm;
        }
        public bool Advance(long now)
        {
            if (!DueAt.HasValue || now < DueAt.Value) return false;
            lastFire = DueAt.Value;
            DueAt = null;
            return true;
        }
        public void Cancel() { DueAt = null; }
    }

    public static class Calibration
    {
        public static string ChoosePattern(IList<List<string>> captures)
        {
            if (captures.Count != 3 || captures.Any(x => x.Count == 0 || x.Count > 128))
                throw new InvalidOperationException("Capture three separate button presses first.");
            List<string> common = captures[0].Distinct().Where(x => captures.All(y => y.Contains(x))).ToList();
            // Ambiguous candidates must be resolved by recapturing, never guessed.
            if (common.Count != 1) throw new InvalidOperationException("The presses did not produce one consistent event. Keep other controls still and try again.");
            return common[0];
        }
    }
}
