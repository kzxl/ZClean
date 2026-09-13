using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ZeroClean.Core.Engine;

public record SystemMemorySnapshot
{
    public ulong TotalPhysicalBytes { get; init; }
    public ulong AvailablePhysicalBytes { get; init; }
    public ulong UsedPhysicalBytes => TotalPhysicalBytes > AvailablePhysicalBytes ? TotalPhysicalBytes - AvailablePhysicalBytes : 0;
    public double MemoryLoadPercent { get; init; }
}

public record MemoryOptimizationResult
{
    public int ProcessesOptimized { get; init; }
    public long InitialWorkingSetBytes { get; init; }
    public long FinalWorkingSetBytes { get; init; }
    public long ReclaimedBytes => Math.Max(0, InitialWorkingSetBytes - FinalWorkingSetBytes);
    public bool StandbyPurged { get; init; }
    public SystemMemorySnapshot? InitialMemory { get; init; }
    public SystemMemorySnapshot? FinalMemory { get; init; }
}

public class MemoryOptimizerService
{
    [DllImport("psapi.dll", SetLastError = true)]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [DllImport("ntdll.dll")]
    private static extern uint NtSetSystemInformation(
        int SystemInformationClass,
        IntPtr SystemInformation,
        int SystemInformationLength);

    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(
        IntPtr TokenHandle,
        bool DisableAllPrivileges,
        ref TOKEN_PRIVILEGES NewState,
        uint BufferLength,
        IntPtr PreviousState,
        IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    private const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
    private const string SE_PROFILE_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";

    /// <summary>
    /// Gets current physical memory snapshot (Total, Available, Load%).
    /// </summary>
    public SystemMemorySnapshot GetSystemMemorySnapshot()
    {
        var stat = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(stat))
        {
            return new SystemMemorySnapshot
            {
                TotalPhysicalBytes = stat.ullTotalPhys,
                AvailablePhysicalBytes = stat.ullAvailPhys,
                MemoryLoadPercent = stat.dwMemoryLoad
            };
        }

        return new SystemMemorySnapshot();
    }

    /// <summary>
    /// Purges Windows Standby Memory list via native NtSetSystemInformation.
    /// </summary>
    public bool PurgeStandbyList()
    {
        try
        {
            EnablePrivilege(SE_INCREASE_QUOTA_NAME);
            EnablePrivilege(SE_PROFILE_SINGLE_PROCESS_NAME);

            GCHandle handle = GCHandle.Alloc(MemoryPurgeStandbyList, GCHandleType.Pinned);
            try
            {
                uint status = NtSetSystemInformation(SystemMemoryListInformation, handle.AddrOfPinnedObject(), Marshal.SizeOf<int>());
                return status == 0; // STATUS_SUCCESS = 0
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Trims memory working set of target processes and optionally purges Windows standby cache.
    /// </summary>
    public Task<MemoryOptimizationResult> OptimizeWorkingSetsAsync(bool purgeStandby = true, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var initialMem = GetSystemMemorySnapshot();
            bool standbyPurged = false;

            if (purgeStandby)
            {
                standbyPurged = PurgeStandbyList();
            }

            var processes = Process.GetProcesses();
            long initialTotal = 0;
            long finalTotal = 0;
            int count = 0;

            foreach (var proc in processes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Skip system process (Id 0, 4)
                    if (proc.Id <= 4) continue;

                    initialTotal += proc.WorkingSet64;

                    // Trim working set
                    int ret = EmptyWorkingSet(proc.Handle);
                    if (ret != 0)
                    {
                        count++;
                        proc.Refresh();
                        finalTotal += proc.WorkingSet64;
                    }
                    else
                    {
                        finalTotal += proc.WorkingSet64;
                    }
                }
                catch
                {
                    // Access denied on protected/system processes is normal
                }
                finally
                {
                    proc.Dispose();
                }
            }

            var finalMem = GetSystemMemorySnapshot();

            return new MemoryOptimizationResult
            {
                ProcessesOptimized = count,
                InitialWorkingSetBytes = initialTotal,
                FinalWorkingSetBytes = finalTotal,
                StandbyPurged = standbyPurged,
                InitialMemory = initialMem,
                FinalMemory = finalMem
            };
        }, cancellationToken);
    }

    private static bool EnablePrivilege(string privilegeName)
    {
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out IntPtr hToken))
                return false;

            try
            {
                if (!LookupPrivilegeValue(null, privilegeName, out LUID luid))
                    return false;

                var tp = new TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Luid = luid,
                    Attributes = SE_PRIVILEGE_ENABLED
                };

                return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally
            {
                CloseHandle(hToken);
            }
        }
        catch
        {
            return false;
        }
    }
}
