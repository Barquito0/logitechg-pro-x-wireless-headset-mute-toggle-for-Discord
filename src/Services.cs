using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HeadsetButtonBridge
{
    public static class Startup
    {
        const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "HeadsetButtonBridge";
        public static void Set(bool enabled)
        {
            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath))
            {
                if (enabled) key.SetValue(ValueName, "\"" + Application.ExecutablePath + "\" --background", RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }
        public static bool IsEnabled()
        {
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(KeyPath))
                return key != null && key.GetValue(ValueName) != null;
        }
    }
    public static class KeySender
    {
        public static void Send(BridgeSettings settings)
        {
            List<ushort> ownedModifiers = new List<ushort>();
            List<INPUT> inputs = new List<INPUT>();
            ushort[] modifiers = new ushort[] { 0x11, 0x12, 0x10 };
            bool[] wanted = new bool[] { settings.Control, settings.Alt, settings.Shift };
            // Wait for held modifiers to be released instead of silently sending
            // a different chord or releasing keys that belong to the user.
            for (int i = 0; i < modifiers.Length; i++)
            {
                if ((GetAsyncKeyState(modifiers[i]) & 0x8000) != 0)
                    throw new InvalidOperationException("Release Ctrl, Alt and Shift, then press the headset button again.");
                if (wanted[i]) { ownedModifiers.Add(modifiers[i]); inputs.Add(Key(modifiers[i], false)); }
            }
            inputs.Add(Key((ushort)settings.VirtualKey, false));
            inputs.Add(Key((ushort)settings.VirtualKey, true));
            for (int i = ownedModifiers.Count - 1; i >= 0; i--) inputs.Add(Key(ownedModifiers[i], true));
            uint sent = SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Count)
            {
                int error = Marshal.GetLastWin32Error();
                // Best effort release for only the keydowns actually inserted.
                List<INPUT> releases = new List<INPUT>();
                for (int i = (int)sent - 1; i >= 0; i--)
                    if (inputs[i].Union.Keyboard.Flags == 0) releases.Add(Key(inputs[i].Union.Keyboard.VirtualKey, true));
                if (releases.Count > 0) SendInput((uint)releases.Count, releases.ToArray(), Marshal.SizeOf(typeof(INPUT)));
                throw new Win32Exception(error, "Windows blocked the shortcut. Run Discord and this app at the same privilege level.");
            }
        }
        static INPUT Key(ushort key, bool up)
        {
            INPUT value = new INPUT { Type = 1 };
            value.Union.Keyboard.VirtualKey = key;
            value.Union.Keyboard.Flags = up ? 2U : 0U;
            return value;
        }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint Type; public InputUnion Union; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion
        {
            [FieldOffset(0)] public KeyboardInput Keyboard;
            [FieldOffset(0)] public MouseInput Mouse;
        }
        [StructLayout(LayoutKind.Sequential)] struct KeyboardInput { public ushort VirtualKey, Scan; public uint Flags, Time; public UIntPtr Extra; }
        [StructLayout(LayoutKind.Sequential)] struct MouseInput { public int X, Y; public uint Data, Flags, Time; public UIntPtr Extra; }
        [DllImport("user32.dll", SetLastError=true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    }

    public static class AppLog
    {
        static readonly object Gate = new object();
        public static void Write(string message)
        {
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(SettingsStore.DirectoryPath);
                    string path = Path.Combine(SettingsStore.DirectoryPath, "app.log");
                    if (File.Exists(path) && new FileInfo(path).Length > 65536)
                    {
                        if (File.Exists(path + ".old")) File.Delete(path + ".old");
                        File.Move(path, path + ".old");
                    }
                    File.AppendAllText(path, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    public sealed class DeviceWindow : Form
    {
        IntPtr registration;
        public event Action DevicesChanged;
        public DeviceWindow()
        {
            Text = "Headset Button Bridge listener";
            ShowInTaskbar = false;
            CreateHandle();
            Filter filter = new Filter { Size = Marshal.SizeOf(typeof(Filter)), Type = 5, ClassGuid = HidDevice.Native.HidGuid };
            registration = RegisterDeviceNotification(Handle, ref filter, 0);
            if (registration == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        protected override void WndProc(ref Message message)
        {
            if ((message.Msg == 0x219 && (message.WParam.ToInt64() == 0x8000 || message.WParam.ToInt64() == 0x8004)) ||
                (message.Msg == 0x218 && (message.WParam.ToInt64() == 7 || message.WParam.ToInt64() == 18)))
            {
                Action changed = DevicesChanged;
                if (changed != null) changed();
            }
            base.WndProc(ref message);
        }
        protected override void Dispose(bool disposing)
        {
            if (registration != IntPtr.Zero) { UnregisterDeviceNotification(registration); registration = IntPtr.Zero; }
            base.Dispose(disposing);
        }
        [StructLayout(LayoutKind.Sequential)] struct Filter { public int Size, Type, Reserved; public Guid ClassGuid; }
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr RegisterDeviceNotification(IntPtr window, ref Filter filter, uint flags);
        [DllImport("user32.dll")] static extern bool UnregisterDeviceNotification(IntPtr registration);
    }
}
