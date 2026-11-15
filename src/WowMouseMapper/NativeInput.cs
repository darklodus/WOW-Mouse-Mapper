using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace WowMouseMapper;

internal static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Device { public ushort Page, Usage; public uint Flags; public IntPtr Target; }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices([In] Device[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetKeyNameText(int parameter, StringBuilder text, int size);

    public static void RegisterMouse(IntPtr window)
    {
        // Foreground only: no INPUTSINK, hooks, injection, or background logging.
        Device[] devices = [new() { Page = 1, Usage = 2, Target = window }];
        if (!RegisterRawInputDevices(devices, 1, (uint)Marshal.SizeOf<Device>()))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static (ushort Flags, short Wheel)? ReadMouse(IntPtr input)
    {
        uint header = (uint)(8 + IntPtr.Size * 2), size = 0;
        if (GetRawInputData(input, 0x10000003, IntPtr.Zero, ref size, header) == uint.MaxValue || size < header + 24 || size > 65536)
            return null;
        var data = Marshal.AllocHGlobal((int)size);
        try
        {
            if (GetRawInputData(input, 0x10000003, data, ref size, header) == uint.MaxValue || Marshal.ReadInt32(data) != 0)
                return null;
            // RAWMOUSE's button union starts at offset 4 after its USHORT + alignment padding.
            return ((ushort)Marshal.ReadInt16(data, (int)header + 4), Marshal.ReadInt16(data, (int)header + 6));
        }
        finally { Marshal.FreeHGlobal(data); }
    }

    public static string KeyName(int key, IntPtr parameter)
    {
        var buffer = new StringBuilder(128);
        GetKeyNameText(unchecked((int)parameter.ToInt64()), buffer, buffer.Capacity);
        return InputDecoder.KeyName(key, (parameter.ToInt64() & (1L << 24)) != 0,
            buffer.Length > 0 ? buffer.ToString() : $"VK 0x{key:X2}");
    }
}
