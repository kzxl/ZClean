using System.Security.Cryptography;
using CleanTool.Core.Contracts;

namespace CleanTool.Core.Engine;

public enum ShredMethod
{
    QuickZero,        // 1 pass (zeros)
    DoD_5220_22_M,    // 3 passes (zeros, ones, random)
    GutmannLite       // 7 passes (pattern alternation, random)
}

public record ShredOptions
{
    public bool DryRun { get; init; } = true;
    public ShredMethod Method { get; init; } = ShredMethod.DoD_5220_22_M;
    public int BufferSize { get; init; } = 64 * 1024; // 64 KB chunks
}

public record ShredProgress
{
    public string CurrentFile { get; init; } = "";
    public int FilesCompleted { get; init; }
    public int TotalFiles { get; init; }
    public long BytesShredded { get; init; }
}

public record ShredResult
{
    public bool Success { get; init; }
    public bool IsDryRun { get; init; }
    public string TargetPath { get; init; } = "";
    public int FilesShredded { get; init; }
    public long TotalBytesShredded { get; init; }
    public int PassesPerformed { get; init; }
    public ShredMethod Method { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// DoD 5220.22-M compliant secure file shredder.
/// Overwrites data patterns, truncates byte length, scrambles MFT directory entry, and deletes.
/// Guarded by ISafetyGuard to prevent accidental destruction of system assets.
/// </summary>
public class FileShredderService
{
    private readonly ISafetyGuard _safetyGuard;

    public FileShredderService(ISafetyGuard? safetyGuard = null)
    {
        _safetyGuard = safetyGuard ?? new SafetyGuard();
    }

    /// <summary>
    /// Securely shreds a single target file.
    /// </summary>
    public async Task<ShredResult> ShredFileAsync(
        string filePath,
        ShredOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ShredOptions();

        if (string.IsNullOrWhiteSpace(filePath))
            return new ShredResult { Success = false, TargetPath = filePath, ErrorMessage = "File path cannot be empty." };

        var fullPath = Path.GetFullPath(filePath);

        if (_safetyGuard.IsProtectedPath(fullPath))
        {
            throw new InvalidOperationException($"Security violation: Refusing to shred protected system asset '{fullPath}'. Action blocked by SafetyGuard.");
        }

        if (!File.Exists(fullPath))
        {
            return new ShredResult
            {
                Success = false,
                TargetPath = fullPath,
                ErrorMessage = $"File not found: '{fullPath}'."
            };
        }

        var fileInfo = new FileInfo(fullPath);
        long fileLength = fileInfo.Length;
        int passes = GetPassCount(options.Method);

        if (options.DryRun)
        {
            return new ShredResult
            {
                Success = true,
                IsDryRun = true,
                TargetPath = fullPath,
                FilesShredded = 1,
                TotalBytesShredded = fileLength,
                PassesPerformed = passes,
                Method = options.Method
            };
        }

        try
        {
            // Clear read-only / hidden flags
            File.SetAttributes(fullPath, FileAttributes.Normal);

            await OverwriteBytesAsync(fullPath, fileLength, options.Method, options.BufferSize, cancellationToken);

            // Scramble filename in MFT before deletion
            var dir = Path.GetDirectoryName(fullPath) ?? Path.GetTempPath();
            var scrambledPath = Path.Combine(dir, $"__shred_{Guid.NewGuid():N}.tmp");
            File.Move(fullPath, scrambledPath);
            File.Delete(scrambledPath);

            return new ShredResult
            {
                Success = true,
                IsDryRun = false,
                TargetPath = fullPath,
                FilesShredded = 1,
                TotalBytesShredded = fileLength,
                PassesPerformed = passes,
                Method = options.Method
            };
        }
        catch (Exception ex)
        {
            return new ShredResult
            {
                Success = false,
                TargetPath = fullPath,
                ErrorMessage = $"Shredding failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Recursively shreds all files in a directory and cleans up the structure.
    /// </summary>
    public async Task<ShredResult> ShredDirectoryAsync(
        string directoryPath,
        ShredOptions? options = null,
        IProgress<ShredProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new ShredOptions();

        if (string.IsNullOrWhiteSpace(directoryPath))
            return new ShredResult { Success = false, TargetPath = directoryPath, ErrorMessage = "Directory path cannot be empty." };

        var fullPath = Path.GetFullPath(directoryPath);

        if (_safetyGuard.IsProtectedPath(fullPath))
        {
            throw new InvalidOperationException($"Security violation: Refusing to shred protected directory '{fullPath}'. Action blocked by SafetyGuard.");
        }

        if (!Directory.Exists(fullPath))
        {
            return new ShredResult
            {
                Success = false,
                TargetPath = fullPath,
                ErrorMessage = $"Directory not found: '{fullPath}'."
            };
        }

        var files = Directory.GetFiles(fullPath, "*", SearchOption.AllDirectories);
        long totalBytes = files.Sum(f => {
            try { return new FileInfo(f).Length; } catch { return 0; }
        });

        int passes = GetPassCount(options.Method);

        if (options.DryRun)
        {
            return new ShredResult
            {
                Success = true,
                IsDryRun = true,
                TargetPath = fullPath,
                FilesShredded = files.Length,
                TotalBytesShredded = totalBytes,
                PassesPerformed = passes,
                Method = options.Method
            };
        }

        int completed = 0;
        long bytesProcessed = 0;

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var res = await ShredFileAsync(file, options with { DryRun = false }, cancellationToken);
            if (res.Success)
            {
                completed++;
                bytesProcessed += res.TotalBytesShredded;
                progress?.Report(new ShredProgress
                {
                    CurrentFile = file,
                    FilesCompleted = completed,
                    TotalFiles = files.Length,
                    BytesShredded = bytesProcessed
                });
            }
        }

        // Delete empty directories bottom-up
        try
        {
            var subdirs = Directory.GetDirectories(fullPath, "*", SearchOption.AllDirectories)
                                   .OrderByDescending(d => d.Length);
            foreach (var d in subdirs)
            {
                try { Directory.Delete(d, false); } catch { }
            }
            Directory.Delete(fullPath, false);
        }
        catch
        {
            // Non-fatal if folder deletion is partially delayed by lock
        }

        return new ShredResult
        {
            Success = true,
            IsDryRun = false,
            TargetPath = fullPath,
            FilesShredded = completed,
            TotalBytesShredded = bytesProcessed,
            PassesPerformed = passes,
            Method = options.Method
        };
    }

    private async Task OverwriteBytesAsync(
        string fullPath,
        long length,
        ShredMethod method,
        int bufferSize,
        CancellationToken cancellationToken)
    {
        if (length == 0)
        {
            // Just truncate/wipe empty file
            await using var emptyStream = new FileStream(fullPath, FileMode.Truncate, FileAccess.Write, FileShare.None);
            return;
        }

        var bytePatterns = GetPatternsForMethod(method);
        var buffer = new byte[Math.Min(bufferSize, Math.Max(1024, (int)Math.Min(length, int.MaxValue)))];

        await using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Write, FileShare.None))
        {
            foreach (var pattern in bytePatterns)
            {
                cancellationToken.ThrowIfCancellationRequested();
                stream.Position = 0;
                long bytesRemaining = length;

                while (bytesRemaining > 0)
                {
                    int chunk = (int)Math.Min(bytesRemaining, buffer.Length);

                    if (pattern.HasValue)
                    {
                        Array.Fill(buffer, pattern.Value, 0, chunk);
                    }
                    else
                    {
                        // Cryptographic random pass
                        RandomNumberGenerator.Fill(buffer.AsSpan(0, chunk));
                    }

                    await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                    bytesRemaining -= chunk;
                }

                await stream.FlushAsync(cancellationToken);
            }

            // Truncate file length to 0
            stream.SetLength(0);
            await stream.FlushAsync(cancellationToken);
        }
    }

    private static byte?[] GetPatternsForMethod(ShredMethod method) => method switch
    {
        ShredMethod.QuickZero => new byte?[] { 0x00 },
        ShredMethod.DoD_5220_22_M => new byte?[] { 0x00, 0xFF, null }, // null = cryptographically random
        ShredMethod.GutmannLite => new byte?[] { 0x55, 0xAA, 0x92, 0x49, 0x24, 0x00, null },
        _ => new byte?[] { 0x00, 0xFF, null }
    };

    private static int GetPassCount(ShredMethod method) => method switch
    {
        ShredMethod.QuickZero => 1,
        ShredMethod.DoD_5220_22_M => 3,
        ShredMethod.GutmannLite => 7,
        _ => 3
    };
}
