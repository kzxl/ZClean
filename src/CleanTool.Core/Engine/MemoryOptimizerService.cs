using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CleanTool.Core.Engine;

public record MemoryOptimizationResult
{
    public int ProcessesOptimized { get; init; }
    public long InitialWorkingSetBytes { get; init; }
    public long FinalWorkingSetBytes { get; init; }
    public long ReclaimedBytes => Math.Max(0, InitialWorkingSetBytes - FinalWorkingSetBytes);
}

public class MemoryOptimizerService
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    /// <summary>
    /// Trims memory working set of target processes or idle user processes.
    /// </summary>
    public Task<MemoryOptimizationResult> OptimizeWorkingSetsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
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

            return new MemoryOptimizationResult
            {
                ProcessesOptimized = count,
                InitialWorkingSetBytes = initialTotal,
                FinalWorkingSetBytes = finalTotal
            };
        }, cancellationToken);
    }
}
