using System.Diagnostics;
using System.Runtime.InteropServices;
using CleanTool.Core.Contracts;
using CleanTool.Core.Models;

namespace CleanTool.Rules.System;

/// <summary>
/// Scans and purges deleted items residing in the Windows Recycle Bin.
/// </summary>
public class RecycleBinRule : ICleanerRule
{
    public string Id => "sys.recyclebin";
    public string Name => "Recycle Bin";
    public string Description => "Purges items currently held inside the Windows Recycle Bin.";
    public CleanCategory Category => CleanCategory.System;
    public CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public bool RequiresElevation => false;
    public bool IsDefaultEnabled => false; // User-controlled

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public Task<RuleScanResult> ScanAsync(CleanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            long totalSize = 0;
            long totalItems = 0;

            try
            {
                var rbInfo = new SHQUERYRBINFO();
                rbInfo.cbSize = Marshal.SizeOf(typeof(SHQUERYRBINFO));

                int hr = SHQueryRecycleBin(null, ref rbInfo);
                if (hr == 0)
                {
                    totalSize = rbInfo.i64Size;
                    totalItems = rbInfo.i64NumItems;
                }
            }
            catch { }

            sw.Stop();

            var items = new List<ScanItemResult>();
            if (totalItems > 0)
            {
                items.Add(new ScanItemResult
                {
                    FilePath = "$Recycle.Bin",
                    SizeBytes = totalSize,
                    LastModified = DateTime.Now,
                    IsLocked = false
                });
            }

            return new RuleScanResult
            {
                RuleId = Id,
                RuleName = Name,
                Category = Category,
                RiskLevel = RiskLevel,
                Items = items,
                TotalSizeBytes = totalSize,
                ScanDuration = sw.Elapsed
            };
        }, cancellationToken);
    }

    public Task<RuleCleanResult> CleanAsync(CleanOptions options, IProgress<CleanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var scan = ScanAsync(options, null, cancellationToken).GetAwaiter().GetResult();

            if (options.DryRun || scan.TotalCount == 0)
            {
                sw.Stop();
                return new RuleCleanResult
                {
                    RuleId = Id,
                    RuleName = Name,
                    DeletedCount = scan.TotalCount,
                    BytesFreed = scan.TotalSizeBytes,
                    SkippedCount = 0,
                    CleanDuration = sw.Elapsed
                };
            }

            int deleted = 0;
            long freed = 0;
            var errors = new List<string>();

            try
            {
                int hr = SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                if (hr == 0)
                {
                    deleted = scan.TotalCount;
                    freed = scan.TotalSizeBytes;
                    progress?.Report(new CleanProgress("$Recycle.Bin", deleted, freed));
                }
                else
                {
                    errors.Add($"SHEmptyRecycleBin failed with HRESULT: 0x{hr:X8}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to empty recycle bin: {ex.Message}");
            }

            sw.Stop();
            return new RuleCleanResult
            {
                RuleId = Id,
                RuleName = Name,
                DeletedCount = deleted,
                BytesFreed = freed,
                SkippedCount = scan.TotalCount - deleted,
                Errors = errors,
                CleanDuration = sw.Elapsed
            };
        }, cancellationToken);
    }
}
