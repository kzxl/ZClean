using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ZeroClean.Core.Engine;

public record DismAnalysisReport
{
    public bool Success { get; init; }
    public string ComponentStoreSize { get; init; } = "Unknown";
    public string ActualSize { get; init; } = "Unknown";
    public string SharedWithWindows { get; init; } = "Unknown";
    public string BackupsAndFeatures { get; init; } = "Unknown";
    public string CacheAndTemp { get; init; } = "Unknown";
    public string DateOfLastCleanup { get; init; } = "Unknown";
    public int SupersededPackagesCount { get; init; }
    public bool IsCleanupRecommended { get; init; }
    public string RawOutput { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

public record DismCleanupResult
{
    public bool Success { get; init; }
    public bool IsDryRun { get; init; }
    public bool ResetBase { get; init; }
    public string Command { get; init; } = "";
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = "";
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Microsoft DISM (Deployment Image Servicing and Management) orchestrator.
/// Safely inspects and purges superseded Windows Update packages and WinSxS component store bloating.
/// </summary>
public class DismComponentService
{
    /// <summary>
    /// Analyzes the WinSxS component store using 'dism.exe /Online /Cleanup-Image /AnalyzeComponentStore'.
    /// </summary>
    public async Task<DismAnalysisReport> AnalyzeComponentStoreAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new DismAnalysisReport
            {
                Success = false,
                ErrorMessage = "DISM component store servicing is only supported on Windows operating systems."
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = "/Online /Cleanup-Image /AnalyzeComponentStore",
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

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                return new DismAnalysisReport
                {
                    Success = false,
                    ErrorMessage = $"DISM exited with code {process.ExitCode}: {stderr}".Trim(),
                    RawOutput = stderr
                };
            }

            return ParseDismOutput(stdout);
        }
        catch (Exception ex)
        {
            return new DismAnalysisReport
            {
                Success = false,
                ErrorMessage = $"Failed to execute DISM: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Parses textual output from DISM /AnalyzeComponentStore.
    /// </summary>
    public DismAnalysisReport ParseDismOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return new DismAnalysisReport { Success = false, ErrorMessage = "Empty output received from DISM." };
        }

        var report = new DismAnalysisReport
        {
            Success = true,
            RawOutput = output,
            ComponentStoreSize = ExtractDismValue(output, @"Component Store \(WinSxS\) Size\s*:\s*([^\r\n]+)") ?? "N/A",
            ActualSize = ExtractDismValue(output, @"Actual Size of Component Store\s*:\s*([^\r\n]+)") ?? "N/A",
            SharedWithWindows = ExtractDismValue(output, @"Shared with Windows\s*:\s*([^\r\n]+)") ?? "N/A",
            BackupsAndFeatures = ExtractDismValue(output, @"Backups and Disabled Features\s*:\s*([^\r\n]+)") ?? "N/A",
            CacheAndTemp = ExtractDismValue(output, @"Cache and Temporary Data\s*:\s*([^\r\n]+)") ?? "N/A",
            DateOfLastCleanup = ExtractDismValue(output, @"Date of Last Cleanup\s*:\s*([^\r\n]+)") ?? "N/A",
            IsCleanupRecommended = (ExtractDismValue(output, @"Component Store Cleanup Recommended\s*:\s*([^\r\n]+)") ?? "").Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase)
        };

        var supersededStr = ExtractDismValue(output, @"Number of Superseded Packages\s*:\s*([0-9]+)");
        if (int.TryParse(supersededStr, out int count))
        {
            report = report with { SupersededPackagesCount = count };
        }

        return report;
    }

    /// <summary>
    /// Executes WinSxS Component Store cleanup.
    /// If ResetBase is true, superseded package versions are permanently deleted, unlocking maximum disk space.
    /// </summary>
    public async Task<DismCleanupResult> RunCleanupAsync(
        bool resetBase = false,
        bool dryRun = true,
        CancellationToken cancellationToken = default)
    {
        var commandArgs = resetBase
            ? "/Online /Cleanup-Image /StartComponentCleanup /ResetBase"
            : "/Online /Cleanup-Image /StartComponentCleanup";

        var fullCommand = $"dism.exe {commandArgs}";

        if (dryRun)
        {
            return new DismCleanupResult
            {
                Success = true,
                IsDryRun = true,
                ResetBase = resetBase,
                Command = fullCommand,
                ExitCode = 0,
                StandardOutput = $"[DRY-RUN] Command prepared: {fullCommand}\nNote: Requires Administrator privileges. Reclaims superseded update backups from WinSxS."
            };
        }

        if (!OperatingSystem.IsWindows())
        {
            return new DismCleanupResult
            {
                Success = false,
                Command = fullCommand,
                ErrorMessage = "DISM execution is only supported on Windows operating systems."
            };
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "dism.exe",
                Arguments = commandArgs,
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

            return new DismCleanupResult
            {
                Success = process.ExitCode == 0,
                IsDryRun = false,
                ResetBase = resetBase,
                Command = fullCommand,
                ExitCode = process.ExitCode,
                StandardOutput = stdout,
                ErrorMessage = process.ExitCode == 0 ? null : (string.IsNullOrWhiteSpace(stderr) ? stdout : stderr).Trim()
            };
        }
        catch (Exception ex)
        {
            return new DismCleanupResult
            {
                Success = false,
                Command = fullCommand,
                ErrorMessage = $"Failed to run DISM cleanup: {ex.Message}"
            };
        }
    }

    private static string? ExtractDismValue(string input, string pattern)
    {
        var match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }
}
