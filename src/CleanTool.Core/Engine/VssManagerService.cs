using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CleanTool.Core.Engine;

public record VssShadowCopy
{
    public required string ShadowCopyId { get; init; }
    public string OriginalVolume { get; init; } = "";
    public string CreationTime { get; init; } = "";
    public string Attributes { get; init; } = "";
}

public record VssStorageUsage
{
    public required string ForVolume { get; init; }
    public string UsedSpace { get; init; } = "Unknown";
    public string AllocatedSpace { get; init; } = "Unknown";
    public string MaximumSpace { get; init; } = "Unknown";
}

public record VssReport
{
    public bool Success { get; init; }
    public IReadOnlyList<VssShadowCopy> ShadowCopies { get; init; } = Array.Empty<VssShadowCopy>();
    public IReadOnlyList<VssStorageUsage> StorageUsage { get; init; } = Array.Empty<VssStorageUsage>();
    public string RawOutput { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public record VssPurgeResult
{
    public bool Success { get; init; }
    public bool IsDryRun { get; init; }
    public string TargetDrive { get; init; } = "C:";
    public string Command { get; init; } = "";
    public string Output { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Volume Shadow Copy (VSS) and System Restore Point manager.
/// Inspects system restore footprints and safely purges obsolete snapshots.
/// </summary>
public class VssManagerService
{
    /// <summary>
    /// Enumerates shadow copies and queries storage allocation for the designated drive.
    /// </summary>
    public async Task<VssReport> GetShadowCopiesReportAsync(string drive = "C:", CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new VssReport
            {
                Success = false,
                ErrorMessage = "VSS shadow copy management is only supported on Windows operating systems."
            };
        }

        var normalizedDrive = NormalizeDrive(drive);

        try
        {
            var shadowsTask = RunVssAdminAsync($"list shadows /for={normalizedDrive}", cancellationToken);
            var storageTask = RunVssAdminAsync($"list shadowstorage /for={normalizedDrive}", cancellationToken);

            await Task.WhenAll(shadowsTask, storageTask);

            var shadowsOutput = await shadowsTask;
            var storageOutput = await storageTask;

            return ParseVssOutput(shadowsOutput, storageOutput);
        }
        catch (Exception ex)
        {
            return new VssReport
            {
                Success = false,
                ErrorMessage = $"Failed to query VSS shadow copies: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses textual outputs from 'vssadmin list shadows' and 'vssadmin list shadowstorage'.
    /// </summary>
    public VssReport ParseVssOutput(string shadowsOutput, string storageOutput)
    {
        var copies = new List<VssShadowCopy>();
        var storages = new List<VssStorageUsage>();

        // Parse shadows
        if (!string.IsNullOrWhiteSpace(shadowsOutput))
        {
            var blocks = shadowsOutput.Split(new[] { "Shadow Copy ID:" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < blocks.Length; i++)
            {
                var block = "Shadow Copy ID:" + blocks[i];
                var idMatch = Regex.Match(block, @"Shadow Copy ID:\s*(\{[^}]+\})");
                var volMatch = Regex.Match(block, @"Original Volume name:\s*([^\r\n]+)");
                var timeMatch = Regex.Match(block, @"Creation Time:\s*([^\r\n]+)");
                var attrMatch = Regex.Match(block, @"Attributes:\s*([^\r\n]+)");

                if (idMatch.Success)
                {
                    copies.Add(new VssShadowCopy
                    {
                        ShadowCopyId = idMatch.Groups[1].Value.Trim(),
                        OriginalVolume = volMatch.Success ? volMatch.Groups[1].Value.Trim() : "",
                        CreationTime = timeMatch.Success ? timeMatch.Groups[1].Value.Trim() : "",
                        Attributes = attrMatch.Success ? attrMatch.Groups[1].Value.Trim() : ""
                    });
                }
            }
        }

        // Parse storage
        if (!string.IsNullOrWhiteSpace(storageOutput))
        {
            var usedMatch = Regex.Match(storageOutput, @"Used Shadow Copy Storage space:\s*([^\r\n]+)");
            var allocMatch = Regex.Match(storageOutput, @"Allocated Shadow Copy Storage space:\s*([^\r\n]+)");
            var maxMatch = Regex.Match(storageOutput, @"Maximum Shadow Copy Storage space:\s*([^\r\n]+)");
            var volMatch = Regex.Match(storageOutput, @"For volume:\s*([^\r\n]+)");

            if (usedMatch.Success || allocMatch.Success)
            {
                storages.Add(new VssStorageUsage
                {
                    ForVolume = volMatch.Success ? volMatch.Groups[1].Value.Trim() : "Current Volume",
                    UsedSpace = usedMatch.Success ? usedMatch.Groups[1].Value.Trim() : "N/A",
                    AllocatedSpace = allocMatch.Success ? allocMatch.Groups[1].Value.Trim() : "N/A",
                    MaximumSpace = maxMatch.Success ? maxMatch.Groups[1].Value.Trim() : "N/A"
                });
            }
        }

        return new VssReport
        {
            Success = true,
            ShadowCopies = copies,
            StorageUsage = storages,
            RawOutput = $"{shadowsOutput}\n\n{storageOutput}".Trim()
        };
    }

    /// <summary>
    /// Purges the oldest shadow copy on the target drive to reclaim disk space while retaining newer restore points.
    /// </summary>
    public async Task<VssPurgeResult> PurgeOldestShadowAsync(
        string drive = "C:",
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedDrive = NormalizeDrive(drive);
        var commandArgs = $"delete shadows /for={normalizedDrive} /oldest /quiet";
        var fullCommand = $"vssadmin {commandArgs}";

        if (dryRun)
        {
            return new VssPurgeResult
            {
                Success = true,
                IsDryRun = true,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                Output = $"[DRY-RUN] Prepared command: {fullCommand}\nNote: Deletes the oldest shadow copy on {normalizedDrive}, preserving recent restore points. Requires Administrator privileges."
            };
        }

        try
        {
            var output = await RunVssAdminAsync(commandArgs, cancellationToken);
            return new VssPurgeResult
            {
                Success = true,
                IsDryRun = false,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                Output = output
            };
        }
        catch (Exception ex)
        {
            return new VssPurgeResult
            {
                Success = false,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                ErrorMessage = $"Failed to purge shadow copy: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Configures the maximum storage allocation cap for shadow copies on a drive.
    /// </summary>
    public async Task<VssPurgeResult> ResizeShadowStorageAsync(
        string drive = "C:",
        int maxSizeGb = 5,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var normalizedDrive = NormalizeDrive(drive);
        var commandArgs = $"resize shadowstorage /for={normalizedDrive} /on={normalizedDrive} /maxsize={maxSizeGb}GB";
        var fullCommand = $"vssadmin {commandArgs}";

        if (dryRun)
        {
            return new VssPurgeResult
            {
                Success = true,
                IsDryRun = true,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                Output = $"[DRY-RUN] Prepared command: {fullCommand}\nNote: Caps shadow copy storage footprint at {maxSizeGb} GB. Requires Administrator privileges."
            };
        }

        try
        {
            var output = await RunVssAdminAsync(commandArgs, cancellationToken);
            return new VssPurgeResult
            {
                Success = true,
                IsDryRun = false,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                Output = output
            };
        }
        catch (Exception ex)
        {
            return new VssPurgeResult
            {
                Success = false,
                TargetDrive = normalizedDrive,
                Command = fullCommand,
                ErrorMessage = $"Failed to resize shadow storage: {ex.Message}"
            };
        }
    }

    private static async Task<string> RunVssAdminAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "vssadmin.exe",
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return string.IsNullOrWhiteSpace(stdout) ? stderr : stdout;
    }

    private static string NormalizeDrive(string drive)
    {
        var clean = drive.Trim().TrimEnd('\\', '/').ToUpperInvariant();
        return clean.EndsWith(":") ? clean : $"{clean}:";
    }
}
