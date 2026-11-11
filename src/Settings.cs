using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace HeadsetButtonBridge
{
    public sealed class BridgeSettings
    {
        public int SchemaVersion = 1;
        public int VendorId = 0x046D;
        public int ProductId = 0x0ABA;
        public int UsagePage = 12;
        public int Usage = 1;
        public int ReportLength = 5;
        public string Pattern = "";
        public int VirtualKey = 0x87;
        public bool Control, Alt, Shift;
        public bool StartWithWindows = true;
        public bool PassedVolumeCheck;
        public int OtherControlGuardMs = 750;
        public int ConfirmDelayMs = 150;
        public int DebounceMs = 500;
        public BridgeSettings Copy() { return (BridgeSettings)MemberwiseClone(); }

        public byte[] PatternBytes()
        {
            return Pattern.Split('-').Select(x => Convert.ToByte(x, 16)).ToArray();
        }
        public void Validate()
        {
            if (SchemaVersion != 1) throw new InvalidDataException("This settings version is not supported.");
            if (VendorId != 0x046D || ProductId != 0x0ABA || UsagePage != 12 || Usage != 1)
                throw new InvalidDataException("This release supports the original Logitech PRO X Wireless receiver (046D:0ABA).");
            if (ReportLength < 2 || ReportLength > 128 || Pattern.Length != ReportLength * 3 - 1 || PatternBytes().Length != ReportLength)
                throw new InvalidDataException("The calibrated report is invalid. Run calibration again.");
            if (!Shortcut.IsAllowed(VirtualKey)) throw new InvalidDataException("Choose a supported shortcut key.");
            if (!PassedVolumeCheck) throw new InvalidDataException("The volume control check has not passed.");
            if (OtherControlGuardMs != 750 || ConfirmDelayMs != 150 || DebounceMs != 500)
                throw new InvalidDataException("The event filter settings are invalid.");
        }
        public string ShortcutLabel { get { return Shortcut.Label(VirtualKey, Control, Alt, Shift); } }
    }

    public static class SettingsStore
    {
        public static readonly string DirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HeadsetButtonBridge");
        public static readonly string FilePath = Path.Combine(DirectoryPath, "settings.xml");
        public static BridgeSettings Load()
        {
            if (!File.Exists(FilePath)) return null;
            using (XmlReader reader = XmlReader.Create(FilePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 32768 }))
            {
                BridgeSettings value = (BridgeSettings)new XmlSerializer(typeof(BridgeSettings)).Deserialize(reader);
                value.Validate();
                return value;
            }
        }
        public static void Save(BridgeSettings value)
        {
            value.Validate();
            Directory.CreateDirectory(DirectoryPath);
            string temporary = FilePath + ".tmp";
            using (FileStream stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                new XmlSerializer(typeof(BridgeSettings)).Serialize(stream, value);
            if (File.Exists(FilePath)) File.Replace(temporary, FilePath, null);
            else File.Move(temporary, FilePath);
        }
    }

    public static class Shortcut
    {
        public static bool IsAllowed(int key) { return (key >= 0x70 && key <= 0x87) || key == 0x13 || key == 0x91; }
        public static string Label(int key, bool control, bool alt, bool shift)
        {
            string name = key >= 0x70 && key <= 0x87 ? "F" + (key - 0x6F) : key == 0x13 ? "Pause" : "Scroll Lock";
            return (control ? "Ctrl + " : "") + (alt ? "Alt + " : "") + (shift ? "Shift + " : "") + name;
        }
    }
}
