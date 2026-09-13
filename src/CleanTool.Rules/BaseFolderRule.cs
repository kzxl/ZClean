using System.Diagnostics;
using CleanTool.Core.Contracts;
using CleanTool.Core.Engine;
using CleanTool.Core.Models;

namespace CleanTool.Rules;

/// <summary>
/// Reusable base implementation for folder-based cleanup targets.
/// </summary>
public abstract class BaseFolderRule : ICleanerRule
{
    private readonly ISafetyGuard _safetyGuard;
    private readonly IFileLockDetector _fileLockDetector;

    protected BaseFolderRule(
        ISafetyGuard? safetyGuard = null, 
        IFileLockDetector? fileLockDetector = null)
    {
        _safetyGuard = safetyGuard ?? new SafetyGuard();
        _fileLockDetector = fileLockDetector ?? new FileLockDetector();
    }

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract CleanCategory Category { get; }
    public virtual CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public virtual bool RequiresElevation => false;
    public virtual bool IsDefaultEnabled => true;

    /// <summary>Returns the collection of directory paths targeted by this rule.</summary>
    protected abstract IEnumerable<string> GetTargetDirectories();

    /// <summary>Search pattern for target files (default "*").</summary>
    protected virtual string SearchPattern => "*";

    /// <summary>Whether to recurse into child subdirectories.</summary>
    protected virtual bool Recursive => true;

    /// <summary>Whether to purge empty subdirectories after cleaning files.</summary>
    protected virtual bool RemoveEmptyDirectories => true;

    public virtual async Task<RuleScanResult> ScanAsync(
        CleanOptions options, 
        IProgress<ScanProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            var items = new List<ScanItemResult>();
            long totalSize = 0;

            foreach (var dir in GetTargetDirectories())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(dir))
                    continue;

                if (!_safetyGuard.IsSafeTargetDirectory(dir))
                    continue;

                ScanDirectory(dir, items, ref totalSize, options, progress, cancellationToken);
            }

            sw.Stop();
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

    public virtual async Task<RuleCleanResult> CleanAsync(
        CleanOptions options, 
        IProgress<CleanProgress>? progress = null, 
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            int deleted = 0;
            long freed = 0;
            int skipped = 0;
            var errors = new List<string>();

            // First perform a scan to determine candidates
            var scan = ScanAsync(options, null, cancellationToken).GetAwaiter().GetResult();

            foreach (var item in scan.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (options.DryRun)
                {
                    deleted++;
                    freed += item.SizeBytes;
                    progress?.Report(new CleanProgress(item.FilePath, deleted, freed));
                    continue;
                }

                if (item.IsLocked && options.SkipLockedFiles)
                {
                    skipped++;
                    continue;
                }

                try
                {
                    if (File.Exists(item.FilePath))
                    {
                        // Clear read-only attribute if set
                        var attributes = File.GetAttributes(item.FilePath);
                        if (attributes.HasFlag(FileAttributes.ReadOnly))
                        {
                            File.SetAttributes(item.FilePath, attributes & ~FileAttributes.ReadOnly);
                        }

                        File.Delete(item.FilePath);
                        deleted++;
                        freed += item.SizeBytes;
                        progress?.Report(new CleanProgress(item.FilePath, deleted, freed));
                    }
                }
                catch (Exception ex)
                {
                    skipped++;
                    errors.Add($"Failed to delete {item.FilePath}: {ex.Message}");
                }
            }

            // Remove empty subdirectories if allowed and not in dry-run
            if (RemoveEmptyDirectories && !options.DryRun)
            {
                foreach (var dir in GetTargetDirectories())
                {
                    try
                    {
                        CleanupEmptySubdirectories(dir);
                    }
                    catch { }
                }
            }

            sw.Stop();
            return new RuleCleanResult
            {
                RuleId = Id,
                RuleName = Name,
                DeletedCount = deleted,
                BytesFreed = freed,
                SkippedCount = skipped,
                Errors = errors,
                CleanDuration = sw.Elapsed
            };
        }, cancellationToken);
    }

    private void ScanDirectory(
        string dirPath, 
        List<ScanItemResult> items, 
        ref long totalSize, 
        CleanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken ct)
    {
        try
        {
            var searchOption = Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(dirPath, SearchPattern, searchOption);

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();

                if (!_safetyGuard.IsEligibleForDeletion(file, options))
                    continue;

                long size = 0;
                DateTime lastModified = DateTime.MinValue;

                try
                {
                    var fi = new FileInfo(file);
                    size = fi.Length;
                    lastModified = fi.LastWriteTime;
                }
                catch
                {
                    continue;
                }

                bool isLocked = _fileLockDetector.IsFileLocked(file);
                if (isLocked && options.SkipLockedFiles && !options.DryRun)
                    continue;

                items.Add(new ScanItemResult
                {
                    FilePath = file,
                    SizeBytes = size,
                    LastModified = lastModified,
                    IsLocked = isLocked
                });

                totalSize += size;
                progress?.Report(new ScanProgress(file, items.Count, totalSize));
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
        catch (Exception) { }
    }

    private static void CleanupEmptySubdirectories(string startLocation)
    {
        if (!Directory.Exists(startLocation))
            return;

        foreach (var directory in Directory.GetDirectories(startLocation))
        {
            CleanupEmptySubdirectories(directory);
            try
            {
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    Directory.Delete(directory, false);
                }
            }
            catch { }
        }
    }
}
