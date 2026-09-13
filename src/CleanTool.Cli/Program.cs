using System.Text;
using CleanTool.Core.Contracts;
using CleanTool.Core.Engine;
using CleanTool.Core.Models;
using CleanTool.Rules;

namespace CleanTool.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        PrintBanner();

        if (args.Length == 0 || args[0] is "--help" or "-h" or "help")
        {
            PrintUsage();
            return 0;
        }

        var registry = new RuleRegistry();
        RuleRegistrar.RegisterAll(registry);
        var engine = new CleanerEngine(registry);
        var analyzer = new DiskAnalyzerService();

        var command = args[0].ToLowerInvariant();
        var cmdArgs = args.Skip(1).ToArray();

        try
        {
            return command switch
            {
                "list" or "rules" => HandleListRules(registry),
                "scan" => await HandleScanAsync(engine, registry, cmdArgs),
                "clean" => await HandleCleanAsync(engine, registry, cmdArgs),
                "analyze" => await HandleAnalyzeAsync(analyzer, cmdArgs),
                "startup" => HandleStartup(),
                "dupes" => await HandleDuplicatesAsync(cmdArgs),
                "mem" => await HandleMemoryAsync(),
                _ => HandleUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Unhandled exception: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine(@"
   ______ _                  _______            _ 
  / ____/| |                |__   __|          | |
 | |     | | ___  __ _ _ __    | | ___   ___  | |
 | |     | |/ _ \/ _` | '_ \   | |/ _ \ / _ \ | |
 | |____ | |  __/ (_| | | | |  | | (_) | (_) || |
  \_____|__|\___|\__,_|_| |_|  |_|\___/ \___/ |_|
       Modern System Cleaner & Optimizer v1.0
");
        Console.ResetColor();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: cleantool <command> [options]\n");
        Console.WriteLine("Commands:");
        Console.WriteLine("  list                  List all available cleaner rules and categories");
        Console.WriteLine("  scan                  Scan system for junk files without deleting");
        Console.WriteLine("  clean                 Perform cleanup (Dry-run simulation by default)");
        Console.WriteLine("  analyze [path]        Inspect disk storage usage and directory hierarchy");
        Console.WriteLine("  startup               Inspect Windows startup programs (Registry & Folder)");
        Console.WriteLine("  dupes <folder>        Scan for duplicate files (3-stage SHA-256 analysis)");
        Console.WriteLine("  mem                   Trim process memory working sets\n");
        Console.WriteLine("Options for scan & clean:");
        Console.WriteLine("  --category <name>     Filter by category (System, Developer, Browser, Application)");
        Console.WriteLine("  --rule <id>           Run specific rule (e.g. sys.temp.user)");
        Console.WriteLine("  --execute             Actually delete files (disables safe Dry-Run mode)");
        Console.WriteLine("  --all                 Include disabled-by-default rules (e.g. Recycle Bin, NuGet)");
        Console.WriteLine("  --min-age-hours <n>   Only clean files older than N hours (default: 24)");
    }

    private static int HandleListRules(IRuleRegistry registry)
    {
        Console.WriteLine("{0,-24} {1,-14} {2,-10} {3}", "Rule ID", "Category", "Risk", "Name");
        Console.WriteLine(new string('-', 75));

        foreach (var rule in registry.GetAllRules())
        {
            var riskColor = rule.RiskLevel switch
            {
                CleanRiskLevel.Safe => ConsoleColor.Green,
                CleanRiskLevel.Moderate => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };

            Console.Write("{0,-24} {1,-14} ", rule.Id, rule.Category);
            Console.ForegroundColor = riskColor;
            Console.Write("{0,-10} ", rule.RiskLevel);
            Console.ResetColor();
            Console.WriteLine("{0} {1}", rule.Name, rule.IsDefaultEnabled ? "" : "(opt-in)");
        }

        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleScanAsync(CleanerEngine engine, IRuleRegistry registry, string[] args)
    {
        var (category, ruleId, isAll, minAgeHours, _) = ParseOptions(args);

        var rulesToRun = ResolveRules(registry, category, ruleId, isAll);
        var options = new CleanOptions
        {
            DryRun = true,
            MinFileAge = TimeSpan.FromHours(minAgeHours)
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Scanning {rulesToRun.Count} rules (Min Age: {minAgeHours}h)...");
        Console.ResetColor();

        var results = await engine.ScanAsync(rulesToRun.Select(r => r.Id), options);

        long totalSize = 0;
        int totalFiles = 0;

        Console.WriteLine("\n{0,-24} {1,10} {2,14}", "Rule ID", "Files", "Reclaimable");
        Console.WriteLine(new string('-', 52));

        foreach (var r in results)
        {
            totalSize += r.TotalSizeBytes;
            totalFiles += r.TotalCount;
            Console.WriteLine("{0,-24} {1,10:N0} {2,14}", r.RuleId, r.TotalCount, FormatBytes(r.TotalSizeBytes));
        }

        Console.WriteLine(new string('=', 52));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("{0,-24} {1,10:N0} {2,14}", "TOTAL", totalFiles, FormatBytes(totalSize));
        Console.ResetColor();
        Console.WriteLine("\nTo reclaim this space, run: cleantool clean --execute\n");

        return 0;
    }

    private static async Task<int> HandleCleanAsync(CleanerEngine engine, IRuleRegistry registry, string[] args)
    {
        var (category, ruleId, isAll, minAgeHours, isExecute) = ParseOptions(args);

        var rulesToRun = ResolveRules(registry, category, ruleId, isAll);
        var options = new CleanOptions
        {
            DryRun = !isExecute,
            MinFileAge = TimeSpan.FromHours(minAgeHours)
        };

        if (options.DryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(">>> DRY-RUN MODE: No files will be physically deleted. Use --execute to clean.\n");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(">>> LIVE CLEAN MODE: Target files will be deleted permanently.\n");
            Console.ResetColor();
        }

        var results = await engine.CleanAsync(rulesToRun.Select(r => r.Id), options);

        long totalFreed = 0;
        int totalDeleted = 0;
        int totalSkipped = 0;

        Console.WriteLine("{0,-24} {1,10} {2,10} {3,14}", "Rule ID", "Deleted", "Skipped", "Freed");
        Console.WriteLine(new string('-', 62));

        foreach (var r in results)
        {
            totalFreed += r.BytesFreed;
            totalDeleted += r.DeletedCount;
            totalSkipped += r.SkippedCount;

            Console.WriteLine("{0,-24} {1,10:N0} {2,10:N0} {3,14}", 
                r.RuleId, r.DeletedCount, r.SkippedCount, FormatBytes(r.BytesFreed));
        }

        Console.WriteLine(new string('=', 62));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("{0,-24} {1,10:N0} {2,10:N0} {3,14}", 
            "TOTAL", totalDeleted, totalSkipped, FormatBytes(totalFreed));
        Console.ResetColor();

        return 0;
    }

    private static async Task<int> HandleAnalyzeAsync(IDiskAnalyzer analyzer, string[] args)
    {
        string? targetPath = args.FirstOrDefault(a => !a.StartsWith('-'));

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            // Analyze system drives
            Console.WriteLine("Inspecting mounted drives...\n");
            var drives = analyzer.GetDrives();

            Console.WriteLine("{0,-10} {1,-16} {2,12} {3,12} {4,10}", "Drive", "Label", "Used", "Total", "Free %");
            Console.WriteLine(new string('-', 64));

            foreach (var d in drives)
            {
                Console.WriteLine("{0,-10} {1,-16} {2,12} {3,12} {4,9:F1}%",
                    d.DriveName, d.VolumeLabel, FormatBytes(d.UsedBytes), FormatBytes(d.TotalBytes), d.FreePercentage);
            }
            Console.WriteLine("\nPass a directory path to analyze subfolder usage: cleantool analyze C:\\");
            return 0;
        }

        if (!Directory.Exists(targetPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Directory does not exist: {targetPath}");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Analyzing directory: {targetPath} (depth: 2)...");
        var node = await analyzer.AnalyzeDirectoryAsync(targetPath, maxDepth: 2);

        Console.WriteLine($"\nDirectory: {node.FullPath}");
        Console.WriteLine($"Total Size: {FormatBytes(node.TotalSizeBytes)} across {node.FileCount:N0} files.\n");

        Console.WriteLine("{0,-32} {1,12} {2,10}", "Folder", "Size", "Files");
        Console.WriteLine(new string('-', 56));

        foreach (var child in node.Children.Take(15))
        {
            Console.WriteLine("{0,-32} {1,12} {2,10:N0}", 
                Truncate(child.Name, 30), FormatBytes(child.TotalSizeBytes), child.FileCount);
        }

        return 0;
    }

    private static int HandleStartup()
    {
        var startup = new StartupManagerService();
        var entries = startup.GetStartupEntries();

        Console.WriteLine($"Found {entries.Count} Windows Startup applications:\n");
        Console.WriteLine("{0,-24} {1,-18} {2,-8} {3}", "Application Name", "Location", "Exists", "Command / Path");
        Console.WriteLine(new string('-', 85));

        foreach (var e in entries)
        {
            var loc = e.LocationType switch
            {
                StartupLocationType.CurrentUserRegistry => "HKCU Run",
                StartupLocationType.LocalMachineRegistry => "HKLM Run",
                _ => "Startup Folder"
            };

            Console.Write("{0,-24} {1,-18} ", Truncate(e.Name, 22), loc);
            if (e.FileExists)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("{0,-8} ", "Yes");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("{0,-8} ", "Dead");
            }
            Console.ResetColor();
            Console.WriteLine(Truncate(e.Command, 32));
        }

        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleDuplicatesAsync(string[] args)
    {
        var targetDir = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Please specify a valid directory to scan for duplicates. Example: cleantool dupes C:\\Users\\Downloads");
            Console.ResetColor();
            return 1;
        }

        Console.WriteLine($"Scanning for duplicate files in: {targetDir}...");
        var finder = new DuplicateFinderService();
        var progress = new Progress<string>(msg => Console.WriteLine($"  {msg}"));

        var duplicates = await finder.FindDuplicatesAsync(targetDir, progress: progress);

        long totalReclaimable = duplicates.Sum(d => d.ReclaimableBytes);
        int totalDuplicateFiles = duplicates.Sum(d => d.FilePaths.Count - 1);

        Console.WriteLine($"\nFound {duplicates.Count} duplicate groups ({totalDuplicateFiles} redundant files, {FormatBytes(totalReclaimable)} reclaimable):\n");

        int groupNum = 1;
        foreach (var group in duplicates.Take(10))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[Group {groupNum++}] File Size: {FormatBytes(group.FileSizeBytes)} | SHA-256: {group.Sha256Hash[..12]}... (Duplicates: {group.FilePaths.Count})");
            Console.ResetColor();
            foreach (var path in group.FilePaths)
            {
                Console.WriteLine($"  - {path}");
            }
            Console.WriteLine();
        }

        if (duplicates.Count > 10)
        {
            Console.WriteLine($"... and {duplicates.Count - 10} more duplicate groups.\n");
        }

        return 0;
    }

    private static async Task<int> HandleMemoryAsync()
    {
        Console.WriteLine("Trimming background process working sets (EmptyWorkingSet)...");
        var memService = new MemoryOptimizerService();
        var result = await memService.OptimizeWorkingSetsAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Optimized {result.ProcessesOptimized} running processes.");
        Console.WriteLine($"Initial Working Set: {FormatBytes(result.InitialWorkingSetBytes)}");
        Console.WriteLine($"Final Working Set:   {FormatBytes(result.FinalWorkingSetBytes)}");
        Console.WriteLine($"Memory Reclaimed:    {FormatBytes(result.ReclaimedBytes)}");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static (string? Category, string? RuleId, bool IsAll, int MinAgeHours, bool IsExecute) ParseOptions(string[] args)
    {
        string? category = null;
        string? ruleId = null;
        bool isAll = false;
        int minAgeHours = 24;
        bool isExecute = false;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            if (a == "--category" && i + 1 < args.Length)
                category = args[++i];
            else if (a == "--rule" && i + 1 < args.Length)
                ruleId = args[++i];
            else if (a == "--all")
                isAll = true;
            else if (a == "--execute")
                isExecute = true;
            else if (a == "--min-age-hours" && i + 1 < args.Length && int.TryParse(args[++i], out var hours))
                minAgeHours = hours;
        }

        return (category, ruleId, isAll, minAgeHours, isExecute);
    }

    private static List<ICleanerRule> ResolveRules(IRuleRegistry registry, string? category, string? ruleId, bool isAll)
    {
        if (!string.IsNullOrWhiteSpace(ruleId))
        {
            var rule = registry.GetRule(ruleId);
            return rule != null ? new List<ICleanerRule> { rule } : new List<ICleanerRule>();
        }

        var query = registry.GetAllRules().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<CleanCategory>(category, true, out var cat))
        {
            query = query.Where(r => r.Category == cat);
        }

        if (!isAll)
        {
            query = query.Where(r => r.IsDefaultEnabled);
        }

        return query.ToList();
    }

    private static int HandleUnknownCommand(string cmd)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unknown command: '{cmd}'. Run 'cleantool --help' for instructions.");
        Console.ResetColor();
        return 1;
    }

    public static string FormatBytes(long bytes)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;
        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return $"{number:n1} {suffixes[counter]}";
    }

    private static string Truncate(string str, int maxLen)
    {
        return str.Length <= maxLen ? str : str[..(maxLen - 3)] + "...";
    }
}
