using CleanTool.Core.Contracts;
using CleanTool.Core.Engine;
using CleanTool.Core.Models;

namespace CleanTool.Rules.System;

/// <summary>
/// Scans Desktop and Start Menu for orphaned .lnk shortcuts pointing to non-existent applications.
/// </summary>
[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
public class BrokenShortcutRule : ICleanerRule
{
    private readonly ISafetyGuard _safetyGuard;

    public BrokenShortcutRule(ISafetyGuard? safetyGuard = null)
    {
        _safetyGuard = safetyGuard ?? new SafetyGuard();
    }

    public string Id => "sys.shortcuts.broken";
    public string Name => "Broken Desktop & Start Menu Shortcuts";
    public string Description => "Finds and removes orphaned .lnk shortcuts pointing to deleted applications.";
    public CleanCategory Category => CleanCategory.System;
    public CleanRiskLevel RiskLevel => CleanRiskLevel.Safe;
    public bool RequiresElevation => false;
    public bool IsDefaultEnabled => false; // Opt-in

    public async Task<RuleScanResult> ScanAsync(CleanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<ScanItemResult>();
            long totalSize = 0;

            var targetFolders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
            };

            dynamic? shell = null;
            try
            {
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                    shell = Activator.CreateInstance(shellType);
            }
            catch { }

            foreach (var folder in targetFolders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Directory.Exists(folder)) continue;

                try
                {
                    foreach (var lnk in Directory.EnumerateFiles(folder, "*.lnk", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!_safetyGuard.IsEligibleForDeletion(lnk, options))
                            continue;

                        bool isBroken = false;
                        if (shell != null)
                        {
                            try
                            {
                                var shortcut = shell.CreateShortcut(lnk);
                                string targetPath = shortcut.TargetPath;
                                if (!string.IsNullOrWhiteSpace(targetPath) && !File.Exists(targetPath) && !Directory.Exists(targetPath))
                                {
                                    isBroken = true;
                                }
                            }
                            catch { }
                        }

                        if (isBroken)
                        {
                            long size = 0;
                            try { size = new FileInfo(lnk).Length; } catch { }

                            items.Add(new ScanItemResult
                            {
                                FilePath = lnk,
                                SizeBytes = size,
                                LastModified = File.GetLastWriteTime(lnk),
                                IsLocked = false
                            });

                            totalSize += size;
                            progress?.Report(new ScanProgress(lnk, items.Count, totalSize));
                        }
                    }
                }
                catch { }
            }

            return new RuleScanResult
            {
                RuleId = Id,
                RuleName = Name,
                Category = Category,
                RiskLevel = RiskLevel,
                Items = items,
                TotalSizeBytes = totalSize
            };
        }, cancellationToken);
    }

    public async Task<RuleCleanResult> CleanAsync(CleanOptions options, IProgress<CleanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var scan = await ScanAsync(options, null, cancellationToken);
        int deleted = 0;
        long freed = 0;
        int skipped = 0;
        var errors = new List<string>();

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

            try
            {
                if (File.Exists(item.FilePath))
                {
                    File.Delete(item.FilePath);
                    deleted++;
                    freed += item.SizeBytes;
                    progress?.Report(new CleanProgress(item.FilePath, deleted, freed));
                }
            }
            catch (Exception ex)
            {
                skipped++;
                errors.Add($"Failed to delete broken shortcut {item.FilePath}: {ex.Message}");
            }
        }

        return new RuleCleanResult
        {
            RuleId = Id,
            RuleName = Name,
            DeletedCount = deleted,
            BytesFreed = freed,
            SkippedCount = skipped,
            Errors = errors
        };
    }
}
