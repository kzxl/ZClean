using System.Security.Cryptography;

namespace CleanTool.Core.Engine;

public record DuplicateGroup
{
    public required long FileSizeBytes { get; init; }
    public required string Sha256Hash { get; init; }
    public required IReadOnlyList<string> FilePaths { get; init; }
    public long ReclaimableBytes => FileSizeBytes * (FilePaths.Count - 1);
}

public class DuplicateFinderService
{
    private const int PartialHashBufferSize = 4096; // 4 KB

    public async Task<IReadOnlyList<DuplicateGroup>> FindDuplicatesAsync(
        string rootDirectory, 
        long minFileSizeBytes = 1024, // Ignore files < 1KB by default
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            if (!Directory.Exists(rootDirectory))
                return (IReadOnlyList<DuplicateGroup>)Array.Empty<DuplicateGroup>();

            progress?.Report("Stage 1/3: Enumerating files and grouping by length...");

            // Stage 1: Group by file length
            var sizeGroups = new Dictionary<long, List<string>>();
            foreach (var file in Directory.EnumerateFiles(rootDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var len = new FileInfo(file).Length;
                    if (len < minFileSizeBytes)
                        continue;

                    if (!sizeGroups.TryGetValue(len, out var list))
                    {
                        list = new List<string>();
                        sizeGroups[len] = list;
                    }
                    list.Add(file);
                }
                catch { }
            }

            // Keep only groups with at least 2 files
            var candidateGroups = sizeGroups.Where(kvp => kvp.Value.Count > 1).ToList();

            progress?.Report($"Stage 2/3: Checking header signatures for {candidateGroups.Count} size buckets...");

            // Stage 2: Partial hash comparison (first 4KB)
            var partialHashGroups = new Dictionary<string, List<string>>();
            foreach (var (size, files) in candidateGroups)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var file in files)
                {
                    try
                    {
                        var partialHash = ComputePartialHash(file);
                        var compositeKey = $"{size}_{partialHash}";

                        if (!partialHashGroups.TryGetValue(compositeKey, out var list))
                        {
                            list = new List<string>();
                            partialHashGroups[compositeKey] = list;
                        }
                        list.Add(file);
                    }
                    catch { }
                }
            }

            var confirmedCandidateBuckets = partialHashGroups.Where(kvp => kvp.Value.Count > 1).ToList();

            progress?.Report($"Stage 3/3: Computing full SHA-256 on {confirmedCandidateBuckets.Count} candidate clusters...");

            // Stage 3: Full SHA-256 comparison
            var finalDuplicates = new List<DuplicateGroup>();
            using var sha = SHA256.Create();

            foreach (var (_, files) in confirmedCandidateBuckets)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullHashGroups = new Dictionary<string, List<string>>();

                foreach (var file in files)
                {
                    try
                    {
                        var fullHash = ComputeFullHash(file, sha);
                        if (!fullHashGroups.TryGetValue(fullHash, out var list))
                        {
                            list = new List<string>();
                            fullHashGroups[fullHash] = list;
                        }
                        list.Add(file);
                    }
                    catch { }
                }

                foreach (var (hash, matchingFiles) in fullHashGroups.Where(kvp => kvp.Value.Count > 1))
                {
                    long fileSize = 0;
                    try { fileSize = new FileInfo(matchingFiles[0]).Length; } catch { }

                    finalDuplicates.Add(new DuplicateGroup
                    {
                        FileSizeBytes = fileSize,
                        Sha256Hash = hash,
                        FilePaths = matchingFiles
                    });
                }
            }

            // Sort by reclaimable bytes descending
            finalDuplicates.Sort((a, b) => b.ReclaimableBytes.CompareTo(a.ReclaimableBytes));
            return (IReadOnlyList<DuplicateGroup>)finalDuplicates;
        }, cancellationToken);
    }

    private static string ComputePartialHash(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var buffer = new byte[PartialHashBufferSize];
        int read = fs.Read(buffer, 0, buffer.Length);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(buffer, 0, read);
        return Convert.ToHexString(hash);
    }

    private static string ComputeFullHash(string filePath, SHA256 sha)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash);
    }
}
