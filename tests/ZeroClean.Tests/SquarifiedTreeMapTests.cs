using ZeroClean.Core.Engine;
using ZeroClean.Core.Models;
using Xunit;

namespace ZeroClean.Tests;

public class SquarifiedTreeMapTests
{
    [Fact]
    public void GenerateSquarifiedTreeMap_ValidHierarchy_ProducesBoundedBlocks()
    {
        var service = new DiskAnalyzerService();
        var root = new DirectoryAnalysisNode
        {
            Name = "Root",
            FullPath = "C:\\Data",
            TotalSizeBytes = 1000,
            Children = new List<DirectoryAnalysisNode>
            {
                new() { Name = "Videos", FullPath = "C:\\Data\\Videos", TotalSizeBytes = 500 },
                new() { Name = "Music", FullPath = "C:\\Data\\Music", TotalSizeBytes = 250 },
                new() { Name = "Documents", FullPath = "C:\\Data\\Documents", TotalSizeBytes = 150 },
                new() { Name = "Downloads", FullPath = "C:\\Data\\Downloads", TotalSizeBytes = 100 },
            }
        };

        double width = 800;
        double height = 600;

        var blocks = service.GenerateSquarifiedTreeMap(root, width, height);

        Assert.Equal(4, blocks.Count);
        double totalPercent = 0;

        foreach (var block in blocks)
        {
            Assert.True(block.X >= 0, $"Block {block.Name} X={block.X} should be >= 0");
            Assert.True(block.Y >= 0, $"Block {block.Name} Y={block.Y} should be >= 0");
            Assert.True(block.X + block.Width <= width + 1.0, $"Block {block.Name} exceeds canvas width");
            Assert.True(block.Y + block.Height <= height + 1.0, $"Block {block.Name} exceeds canvas height");
            Assert.False(string.IsNullOrWhiteSpace(block.ColorHex));
            Assert.False(string.IsNullOrWhiteSpace(block.FormattedSize));
            totalPercent += block.PercentOfTotal;
        }

        Assert.InRange(totalPercent, 99.0, 101.0);
    }

    [Fact]
    public void GenerateSquarifiedTreeMap_EmptyOrZero_ReturnsEmpty()
    {
        var service = new DiskAnalyzerService();
        var emptyRoot = new DirectoryAnalysisNode
        {
            Name = "Empty",
            FullPath = "C:\\Empty",
            TotalSizeBytes = 0
        };

        var blocks = service.GenerateSquarifiedTreeMap(emptyRoot, 500, 400);
        Assert.Empty(blocks);

        var nullBlocks = service.GenerateSquarifiedTreeMap(null!, 500, 400);
        Assert.Empty(nullBlocks);

        var zeroDimensionBlocks = service.GenerateSquarifiedTreeMap(emptyRoot, 0, 0);
        Assert.Empty(zeroDimensionBlocks);
    }
}
