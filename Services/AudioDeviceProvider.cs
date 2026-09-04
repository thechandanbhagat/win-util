using System.Runtime.InteropServices;
using WinUtil.Models;

namespace WinUtil.Services;

internal sealed class AudioDeviceProvider : IAudioDeviceProvider
{
    private static readonly Guid FriendlyNamePropertyFormatId = new("a45c254e-df1c-4efd-8020-67d146a850e0");
    private const uint FriendlyNamePropertyId = 14;
    private const uint StorageAccessModeRead = 0;

    public AudioDeviceSnapshot GetSnapshot()
    {
        try
        {
            var enumerator = (IMultimediaDeviceEnumerator)new MultimediaDeviceEnumerator();

            try
            {
                var outputName = TryGetDefaultDeviceName(enumerator, DataFlow.Render);
                var inputName = TryGetDefaultDeviceName(enumerator, DataFlow.Capture);
                return new AudioDeviceSnapshot(outputName, inputName, null);
            }
            finally
            {
                Marshal.ReleaseComObject(enumerator);
            }
        }
        catch (COMException)
        {
            return new AudioDeviceSnapshot(null, null, "Audio device discovery could not complete.");
        }
    }

    private static string? TryGetDefaultDeviceName(IMultimediaDeviceEnumerator enumerator, DataFlow dataFlow)
    {
        if (enumerator.GetDefaultAudioEndpoint(dataFlow, DeviceRole.Multimedia, out var device) != 0
            || device is null)
        {
            return null;
        }

        try
        {
            if (device.OpenPropertyStore(StorageAccessModeRead, out var propertyStore) != 0
                || propertyStore is null)
            {
                return null;
            }

            try
            {
                var key = new PropertyKey(FriendlyNamePropertyFormatId, FriendlyNamePropertyId);
                if (propertyStore.GetValue(ref key, out var value) != 0)
                {
                    return null;
                }

                try
                {
                    return value.ReadStringValue();
                }
                finally
                {
                    NativeMethods.PropVariantClear(ref value);
                }
            }
            finally
            {
                Marshal.ReleaseComObject(propertyStore);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(device);
        }
    }

    private enum DataFlow
    {
        Render = 0,
        Capture = 1
    }

    private enum DeviceRole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        internal Guid FormatId = formatId;
        internal uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        private const ushort VariantTypeLpwstr = 31;

        [FieldOffset(0)]
        internal ushort VariantType;

        [FieldOffset(8)]
        internal IntPtr PointerValue;

        internal readonly string? ReadStringValue() => VariantType == VariantTypeLpwstr
            ? Marshal.PtrToStringUni(PointerValue)
            : null;
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MultimediaDeviceEnumerator;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMultimediaDeviceEnumerator
    {
        int EnumAudioEndpoints(DataFlow dataFlow, uint stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(DataFlow dataFlow, DeviceRole role, out IMultimediaDevice? endpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMultimediaDevice
    {
        int Activate(ref Guid interfaceId, uint classContext, IntPtr activationParameters, [MarshalAs(UnmanagedType.IUnknown)] out object? activatedInterface);

        int OpenPropertyStore(uint storageAccessMode, out IPropertyStore? propertyStore);
    }

    [ComImport]
    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        int GetCount(out uint propertyCount);

        int GetAt(uint propertyIndex, out PropertyKey key);

        int GetValue(ref PropertyKey key, out PropVariant value);
    }

    private static class NativeMethods
    {
        [DllImport("ole32.dll")]
        internal static extern int PropVariantClear(ref PropVariant propertyVariant);
    }
}
