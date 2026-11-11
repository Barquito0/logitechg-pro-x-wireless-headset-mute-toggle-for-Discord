using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace HeadsetButtonBridge
{
    public sealed class DeviceInfo
    {
        public string Path;
        public int VendorId, ProductId, UsagePage, Usage, ReportLength;
        public override string ToString() { return "Logitech PRO X Wireless  |  " + VendorId.ToString("X4") + ":" + ProductId.ToString("X4"); }
        public bool Matches(BridgeSettings s) { return VendorId == s.VendorId && ProductId == s.ProductId && UsagePage == s.UsagePage && Usage == s.Usage && ReportLength == s.ReportLength; }
    }
    public sealed class HidDevice : IDisposable
    {
        readonly SafeFileHandle handle;
        readonly Thread worker;
        volatile bool stopping;
        public event Action<byte[]> Report;
        public event Action<string> Disconnected;
        public readonly DeviceInfo Info;
        public HidDevice(DeviceInfo info)
        {
            Info = info;
            handle = Native.CreateFile(info.Path, 0x80000000, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
            if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error, "Could not listen to the headset. Close other diagnostic tools and reconnect its USB receiver."); }
            worker = new Thread(ReadLoop) { IsBackground = true, Name = "Headset HID listener" };
        }
        public void Start() { worker.Start(); }
        void ReadLoop()
        {
            SetNativeThread();
            byte[] buffer = new byte[Info.ReportLength];
            try
            {
                while (!stopping)
                {
                    uint count;
                    // Blocking kernel read: no timer, spin loop, microphone stream,
                    // or USB write while the headset is idle.
                    if (!Native.ReadFile(handle, buffer, (uint)buffer.Length, out count, IntPtr.Zero))
                    {
                        if (!stopping && Disconnected != null) Disconnected("Headset disconnected (Windows error " + Marshal.GetLastWin32Error() + ").");
                        return;
                    }
                    if (count != buffer.Length || stopping) continue;
                    Action<byte[]> callback = Report;
                    if (callback != null) callback((byte[])buffer.Clone());
                }
            }
            catch (ObjectDisposedException) { }
        }
        public void Dispose()
        {
            if (stopping) return;
            stopping = true;
            // Repeat cancellation during shutdown to cover the tiny interval
            // between the worker's stopping check and its next blocking read.
            Native.CancelIoEx(handle, IntPtr.Zero);
            if (worker.IsAlive && worker != Thread.CurrentThread)
            {
                for (int attempt = 0; attempt < 50 && worker.IsAlive; attempt++)
                {
                    IntPtr threadHandle = Native.OpenThread(1, false, (uint)workerNativeId);
                    if (threadHandle != IntPtr.Zero) { Native.CancelSynchronousIo(threadHandle); Native.CloseHandle(threadHandle); }
                    Native.CancelIoEx(handle, IntPtr.Zero);
                    worker.Join(10);
                }
            }
            handle.Dispose();
        }
        volatile int workerNativeId;
        void SetNativeThread() { workerNativeId = (int)Native.GetCurrentThreadId(); }

        public static List<DeviceInfo> Discover()
        {
            List<DeviceInfo> result = new List<DeviceInfo>();
            Guid hid = Native.HidGuid;
            IntPtr set = Native.SetupDiGetClassDevs(ref hid, null, IntPtr.Zero, 0x12);
            if (set == new IntPtr(-1)) throw new Win32Exception();
            try
            {
                for (uint index = 0; ; index++)
                {
                    Native.InterfaceData data = new Native.InterfaceData { Size = Marshal.SizeOf(typeof(Native.InterfaceData)) };
                    if (!Native.SetupDiEnumDeviceInterfaces(set, IntPtr.Zero, ref hid, index, ref data))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break;
                        throw new Win32Exception(error);
                    }
                    uint length;
                    Native.SetupDiGetDeviceInterfaceDetail(set, ref data, IntPtr.Zero, 0, out length, IntPtr.Zero);
                    IntPtr detail = Marshal.AllocHGlobal((int)length);
                    string path;
                    try
                    {
                        Marshal.WriteInt32(detail, IntPtr.Size == 8 ? 8 : 6);
                        if (!Native.SetupDiGetDeviceInterfaceDetail(set, ref data, detail, length, out length, IntPtr.Zero)) throw new Win32Exception();
                        path = Marshal.PtrToStringUni(IntPtr.Add(detail, 4));
                    }
                    finally { Marshal.FreeHGlobal(detail); }
                    // v1 deliberately supports this tested receiver, never opens
                    // keyboards, mice, or unrelated HID devices for reading.
                    if (path.IndexOf("vid_046d&pid_0aba", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using (SafeFileHandle h = Native.CreateFile(path, 0, 3, IntPtr.Zero, 3, 0, IntPtr.Zero))
                    {
                        if (h.IsInvalid) continue;
                        IntPtr parsed;
                        if (!Native.HidD_GetPreparsedData(h, out parsed)) continue;
                        Native.Caps caps;
                        int status;
                        try { status = Native.HidP_GetCaps(parsed, out caps); }
                        finally { Native.HidD_FreePreparsedData(parsed); }
                        if (status < 0 || caps.UsagePage != 12 || caps.Usage != 1 || caps.InputLength < 2 || caps.InputLength > 128) continue;
                        result.Add(new DeviceInfo { Path = path, VendorId = 0x046D, ProductId = 0x0ABA, UsagePage = caps.UsagePage, Usage = caps.Usage, ReportLength = caps.InputLength });
                    }
                }
            }
            finally { Native.SetupDiDestroyDeviceInfoList(set); }
            return result;
        }

        internal static class Native
        {
            public static readonly Guid HidGuid = new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
            [StructLayout(LayoutKind.Sequential)] public struct InterfaceData { public int Size; public Guid Guid; public uint Flags; public IntPtr Reserved; }
            [StructLayout(LayoutKind.Sequential)] public struct Caps
            {
                public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] Reserved;
                public ushort Links, InButtons, InValues, InData, OutButtons, OutValues, OutData, FeatureButtons, FeatureValues, FeatureData;
            }
            [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr data);
            [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr data);
            [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr data, out Caps caps);
            [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid guid, string enumerator, IntPtr parent, uint flags);
            [DllImport("setupapi.dll", SetLastError=true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr set, IntPtr device, ref Guid guid, uint index, ref InterfaceData data);
            [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr set, ref InterfaceData data, IntPtr detail, uint size, out uint required, IntPtr device);
            [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr set);
            [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
            [DllImport("kernel32.dll", SetLastError=true)] public static extern bool ReadFile(SafeFileHandle h, byte[] data, uint length, out uint count, IntPtr overlapped);
            [DllImport("kernel32.dll", SetLastError=true)] public static extern bool CancelIoEx(SafeFileHandle h, IntPtr overlapped);
            [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
            [DllImport("kernel32.dll", SetLastError=true)] public static extern IntPtr OpenThread(uint access, bool inherit, uint id);
            [DllImport("kernel32.dll", SetLastError=true)] public static extern bool CancelSynchronousIo(IntPtr thread);
            [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr handle);
        }
    }
}
