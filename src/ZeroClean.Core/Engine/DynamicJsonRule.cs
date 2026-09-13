using System.Text.Json;
using System.Text.Json.Serialization;
using ZeroClean.Core.Contracts;
using ZeroClean.Core.Models;

namespace ZeroClean.Core.Engine;

/// <summary>
/// Data contract for declarative cleaner rules defined in JSON.
/// </summary>
public record JsonRuleDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("category")]
    public string Category { get; init; } = "Application";

    [JsonPropertyName("riskLevel")]
    public string RiskLevel { get; init; } = "Safe";

    [JsonPropertyName("requiresElevation")]
    public bool RequiresElevation { get; init; } = false;

    [JsonPropertyName("isDefaultEnabled")]
    public bool IsDefaultEnabled { get; init; } = true;

    [JsonPropertyName("paths")]
    public List<string> Paths { get; init; } = new();

    [JsonPropertyName("fileMask")]
    public string FileMask { get; init; } = "*";

    [JsonPropertyName("recursive")]
    public bool Recursive { get; init; } = true;
}

/// <summary>
/// Dynamic rule instance constructed at runtime from JSON definitions (inspired by CCleaner winapp2.ini).
/// </summary>
public class DynamicJsonRule : ICleanerRule
{
    private readonly JsonRuleDefinition _def;
    private readonly ISafetyGuard _safetyGuard;
    private readonly IFileLockDetector _fileLockDetector;

    public DynamicJsonRule(
        JsonRuleDefinition def, 
        ISafetyGuard? safetyGuard = null, 
        IFileLockDetector? fileLockDetector = null)
    {
        _def = def ?? throw new ArgumentNullException(nameof(def));
        _safetyGuard = safetyGuard ?? new SafetyGuard();
        _fileLockDetector = fileLockDetector ?? new FileLockDetector();
    }

    public string Id => _def.Id;
    public string Name => _def.Name;
    public string Description => _def.Description;

    public CleanCategory Category => Enum.TryParse<CleanCategory>(_def.Category, true, out var cat) 
        ? cat 
        : CleanCategory.Application;

    public CleanRiskLevel RiskLevel => Enum.TryParse<CleanRiskLevel>(_def.RiskLevel, true, out var rl) 
        ? rl 
        : CleanRiskLevel.Safe;

    public bool RequiresElevation => _def.RequiresElevation;
    public bool IsDefaultEnabled => _def.IsDefaultEnabled;

    public static DynamicJsonRule FromJson(string json, ISafetyGuard? safetyGuard = null)
    {
        var def = JsonSerializer.Deserialize<JsonRuleDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Failed to deserialize rule definition.");

        return new DynamicJsonRule(def, safetyGuard);
    }

    public async Task<RuleScanResult> ScanAsync(CleanOptions options, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            var items = new List<ScanItemResult>();
            long totalSize = 0;

            foreach (var rawPath in _def.Paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var resolvedPath = Environment.ExpandEnvironmentVariables(rawPath);

                if (!Directory.Exists(resolvedPath) || !_safetyGuard.IsSafeTargetDirectory(resolvedPath))
                    continue;

                try
                {
                    var opt = _def.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                    foreach (var file in Directory.EnumerateFiles(resolvedPath, _def.FileMask, opt))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!_safetyGuard.IsEligibleForDeletion(file, options))
                            continue;

                        try
                        {
                            var fi = new FileInfo(file);
                            bool locked = _fileLockDetector.IsFileLocked(file);

                            if (locked && options.SkipLockedFiles && !options.DryRun)
                                continue;

                            items.Add(new ScanItemResult
                            {
                                FilePath = file,
                                SizeBytes = fi.Length,
                                LastModified = fi.LastWriteTime,
                                IsLocked = locked
                            });

                            totalSize += fi.Length;
                            progress?.Report(new ScanProgress(file, items.Count, totalSize));
                        }
                        catch { }
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

            if (item.IsLocked && options.SkipLockedFiles)
            {
                skipped++;
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
                errors.Add($"Failed to delete {item.FilePath}: {ex.Message}");
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
