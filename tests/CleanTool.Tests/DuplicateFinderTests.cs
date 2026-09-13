using CleanTool.Core.Engine;
using Xunit;

namespace CleanTool.Tests;

public class DuplicateFinderTests
{
    [Fact]
    public async Task FindDuplicatesAsync_IdentifiesIdenticalFiles_InSandbox()
    {
        var sandbox = Path.Combine(Path.GetTempPath(), "CleanTool_DupeTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sandbox);

        try
        {
            // Create two identical files > 1KB
            var identicalContent = new string('A', 5000);
            var file1 = Path.Combine(sandbox, "original.dat");
            var file2 = Path.Combine(sandbox, "copy.dat");
            await File.WriteAllTextAsync(file1, identicalContent);
            await File.WriteAllTextAsync(file2, identicalContent);

            // Create a different file of identical length
            var differentContent = new string('B', 5000);
            var file3 = Path.Combine(sandbox, "different.dat");
            await File.WriteAllTextAsync(file3, differentContent);

            var finder = new DuplicateFinderService();
            var results = await finder.FindDuplicatesAsync(sandbox, minFileSizeBytes: 500);

            Assert.Single(results);
            var group = results[0];
            Assert.Equal(2, group.FilePaths.Count);
            Assert.Contains(file1, group.FilePaths);
            Assert.Contains(file2, group.FilePaths);
            Assert.DoesNotContain(file3, group.FilePaths);
        }
        finally
        {
            if (Directory.Exists(sandbox))
                Directory.Delete(sandbox, true);
        }
    }
}
