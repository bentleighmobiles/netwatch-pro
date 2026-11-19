using System.Runtime.InteropServices;

namespace NetwatchPro.Services;

/// <summary>
/// Raw Windows API surfaces used by the Milestone-2 live collector. Struct layouts
/// match the documented iphlpapi/kernel32 definitions; keep field order exact.
/// </summary>
internal static class WindowsNative
{
    // ---------------- CPU (GetSystemTimes) ----------------
    [StructLayout(LayoutKind.Sequential)]
    internal struct FileTime
    {
        public uint Low;
        public uint High;
    }

    internal static ulong ToU64(FileTime t) => ((ulong)t.High << 32) | t.Low;

    [DllImport("kernel32.dll")]
    internal static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    // ---------------- RAM (GlobalMemoryStatusEx) ----------------
    [StructLayout(LayoutKind.Sequential)]
    internal struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;       // % of physical memory in use
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    // ---------------- Disk (GetDiskFreeSpaceEx) ----------------
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDiskFreeSpaceExW(
        string directory, out ulong freeAvailable, out ulong totalBytes, out ulong totalFreeBytes);

    // ---------------- TCP (GetExtendedTcpTable) ----------------
    internal const int AfInet = 2;
    internal const int AfInet6 = 23;
    internal const int TcpTableOwnerPidAll = 5;
    internal const uint TcpStateListen = 2;
    internal const uint TcpStateEstablished = 5;

    [StructLayout(LayoutKind.Sequential)]
    internal struct TcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;      // network byte order
        public uint LocalPort;      // port in low 16 bits, network byte order
        public uint RemoteAddr;
        public uint RemotePort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    internal static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable, ref int size, bool order, int af, int tableClass, int reserved);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Tcp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddr;
        public uint LocalScopeId;
        public uint LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] RemoteAddr;
        public uint RemoteScopeId;
        public uint RemotePort;
        public uint State;
        public uint OwningPid;
    }

    // ---------------- UDP (GetExtendedUdpTable) ----------------
    internal const int UdpTableOwnerPid = 1;

    [StructLayout(LayoutKind.Sequential)]
    internal struct UdpRowOwnerPid
    {
        public uint LocalAddr;
        public uint LocalPort;
        public uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    internal static extern uint GetExtendedUdpTable(
        IntPtr pUdpTable, ref int size, bool order, int af, int tableClass, int reserved);
}
