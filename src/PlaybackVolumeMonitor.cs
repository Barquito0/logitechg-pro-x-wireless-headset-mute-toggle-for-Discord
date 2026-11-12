using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HeadsetButtonBridge
{
    // Read-only Core Audio callbacks. No audio streams or periodic queries.
    public sealed class PlaybackVolumeMonitor : IDisposable
    {
        sealed class Subscription
        {
            public IAudioEndpointVolume Volume;
            public VolumeCallback Callback;
        }
        readonly List<Subscription> subscriptions = new List<Subscription>();
        IMMDeviceEnumerator enumerator;
        EndpointCallback endpointCallback;
        volatile bool disposed;
        public event Action<float, bool> Changed;
        public event Action EndpointsChanged;
        public int EndpointCount { get { return subscriptions.Count; } }
        public PlaybackVolumeMonitor()
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            try
            {
                endpointCallback = new EndpointCallback(this);
                Marshal.ThrowExceptionForHR(enumerator.RegisterEndpointNotificationCallback(endpointCallback));
                Refresh();
            }
            catch { Dispose(); throw; }
        }
        public void Refresh()
        {
            ClearSubscriptions();
            IMMDeviceCollection devices;
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 1, out devices));
            try
            {
                uint count;
                Marshal.ThrowExceptionForHR(devices.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device;
                    Marshal.ThrowExceptionForHR(devices.Item(i, out device));
                    IAudioEndpointVolume volume = null;
                    try
                    {
                        Guid iid = typeof(IAudioEndpointVolume).GUID;
                        object obj;
                        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 23, IntPtr.Zero, out obj));
                        volume = (IAudioEndpointVolume)obj;
                        VolumeCallback callback = new VolumeCallback(this);
                        Marshal.ThrowExceptionForHR(volume.RegisterControlChangeNotify(callback));
                        subscriptions.Add(new Subscription { Volume = volume, Callback = callback });
                        volume = null;
                    }
                    finally { if (volume != null) Marshal.ReleaseComObject(volume); Marshal.ReleaseComObject(device); }
                }
            }
            finally { Marshal.ReleaseComObject(devices); }
        }
        void ClearSubscriptions()
        {
            foreach (Subscription item in subscriptions)
            {
                item.Callback.Stop();
                try { item.Volume.UnregisterControlChangeNotify(item.Callback); } catch (COMException) { }
                Marshal.ReleaseComObject(item.Volume);
            }
            subscriptions.Clear();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            ClearSubscriptions();
            if (endpointCallback != null && enumerator != null)
            {
                try { enumerator.UnregisterEndpointNotificationCallback(endpointCallback); } catch (COMException) { }
                endpointCallback = null;
            }
            if (enumerator != null) { Marshal.ReleaseComObject(enumerator); enumerator = null; }
        }
        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class EndpointCallback : IMMNotificationClient
        {
            readonly PlaybackVolumeMonitor owner;
            internal EndpointCallback(PlaybackVolumeMonitor owner) { this.owner = owner; }
            void Notify()
            {
                if (owner.disposed) return;
                try { Action action = owner.EndpointsChanged; if (action != null) action(); } catch { }
            }
            public int OnDeviceStateChanged(string id, uint state) { Notify(); return 0; }
            public int OnDeviceAdded(string id) { Notify(); return 0; }
            public int OnDeviceRemoved(string id) { Notify(); return 0; }
            public int OnDefaultDeviceChanged(int flow, int role, string id) { if (flow == 0) Notify(); return 0; }
            public int OnPropertyValueChanged(string id, PropertyKey key) { return 0; }
        }
        [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
        public sealed class VolumeCallback : IAudioEndpointVolumeCallback
        {
            readonly PlaybackVolumeMonitor owner;
            volatile bool stopped;
            internal VolumeCallback(PlaybackVolumeMonitor owner) { this.owner = owner; }
            public void Stop() { stopped = true; }
            public int OnNotify(IntPtr data)
            {
                if (stopped || owner.disposed || data == IntPtr.Zero) return 0;
                try
                {
                    VolumeNotification value = (VolumeNotification)Marshal.PtrToStructure(data, typeof(VolumeNotification));
                    Action<float, bool> handler = owner.Changed;
                    if (handler != null) handler(value.MasterVolume, value.Muted != 0);
                }
                catch { /* Exceptions must never cross Windows' COM callback boundary. */ }
                return 0;
            }
        }
        [StructLayout(LayoutKind.Sequential)] struct VolumeNotification
        {
            public Guid Context;
            public int Muted;
            public float MasterVolume;
            public uint Channels;
            public float FirstChannelVolume;
        }
        [ComVisible(true), Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IAudioEndpointVolumeCallback { [PreserveSig] int OnNotify(IntPtr data); }
        [StructLayout(LayoutKind.Sequential)] public struct PropertyKey { public Guid Format; public uint Id; }
        [ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IMMNotificationClient
        {
            [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint state);
            [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, PropertyKey key);
        }
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumerator { }
        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IMMNotificationClient callback);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IMMNotificationClient callback);
        }
        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDeviceCollection
        {
            [PreserveSig] int GetCount(out uint count);
            [PreserveSig] int Item(uint index, out IMMDevice device);
        }
        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object obj);
        }
        [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
            [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback callback);
        }
    }
}
