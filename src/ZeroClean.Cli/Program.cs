using System.Text;
using ZeroClean.Core.Contracts;
using ZeroClean.Core.Engine;
using ZeroClean.Core.Models;
using ZeroClean.Rules;

namespace ZeroClean.Cli;

[global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
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
                "mem" or "ram" => await HandleMemoryAsync(),
                "dev" or "workspaces" => await HandleDevWorkspacesAsync(cmdArgs),
                "advise" or "advisor" => await HandleAdviseAsync(engine, registry, cmdArgs),
                "apps" or "installed" => HandleInstalledApps(cmdArgs),
                "leftovers" => HandleLeftoverFolders(),
                "residuals" or "trace" => HandleResiduals(cmdArgs),
                "uninstall" => await HandleUninstallAsync(cmdArgs),
                "large-files" or "largefiles" => await HandleLargeFilesAsync(cmdArgs),
                "empty-dirs" or "emptydirs" => await HandleEmptyDirsAsync(cmdArgs),
                "shred" => await HandleShredAsync(cmdArgs),
                "dism" => await HandleDismAsync(cmdArgs),
                "wsl" => await HandleWslAsync(cmdArgs),
                "docker" => await HandleDockerAsync(cmdArgs),
                "vss" => await HandleVssAsync(cmdArgs),
                "sentinel" => HandleSentinel(),
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
  ______                   _____ _                  
 |___  /                  / ____| |                 
    / / ___ _ __ ___     | |    | | ___  __ _ _ __  
   / / / _ \ '__/ _ \    | |    | |/ _ \/ _` | '_ \ 
  / /_|  __/ | | (_) |   | |____| |  __/ (_| | | | |
 /_____\___|_|  \___/     \_____|_|\___|\__,_|_| |_|
     Sovereign Zero Universe System Optimizer v2.0
");
        Console.ResetColor();
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: zeroclean <command> [options]\n");
        Console.WriteLine("Core Cleaner Commands:");
        Console.WriteLine("  list                     List all available cleaner rules and categories");
        Console.WriteLine("  scan [options]           Scan system for junk files without deleting");
        Console.WriteLine("  clean [options]          Perform cleanup (Dry-run simulation by default)");
        Console.WriteLine("  advise [workspace_path]  System health assessment & prioritized recommendations");
        Console.WriteLine();
        Console.WriteLine("System & Uninstaller Commands:");
        Console.WriteLine("  apps [options]           List installed apps (--search, --broken, --sort)");
        Console.WriteLine("  residuals <app-name>     Scan Registry & Disk residuals (Revo-style post-uninstall trace)");
        Console.WriteLine("  uninstall <app-name>     Uninstall software safely (--execute to run, dry-run by default)");
        Console.WriteLine("  leftovers                Detect orphan leftover AppData folders from uninstalled apps");
        Console.WriteLine("  startup                  Inspect Windows startup programs (Registry & Folder)");
        Console.WriteLine();
        Console.WriteLine("Storage & Developer Commands:");
        Console.WriteLine("  analyze [path]           Inspect disk storage usage and directory hierarchy");
        Console.WriteLine("  large-files [path]       Scan for heavy space hogs (--min-mb 100)");
        Console.WriteLine("  empty-dirs [path]        Find empty directory trees");
        Console.WriteLine("  dupes <folder>           Scan for duplicate files (3-stage SHA-256 analysis)");
        Console.WriteLine("  mem                      Trim process memory working sets");
        Console.WriteLine("  dev [path]               Scan dev workspaces for dormant repos & build artifacts");
        Console.WriteLine();
        Console.WriteLine("High-Value Extension Commands:");
        Console.WriteLine("  sentinel                 Real-time disk threshold watchdog (< 15% / < 5% margin alerts)");
        Console.WriteLine("  shred <path>             DoD 5220.22-M 3-pass secure cryptographic file shredder");
        Console.WriteLine("  dism [options]           WinSxS Component Store analysis & cleanup (--reset-base)");
        Console.WriteLine("  wsl [options]            Inspect & shrink WSL2 ext4.vhdx virtual disks (--compact)");
        Console.WriteLine("  docker [options]         Docker container/image/cache reclamation (--prune, --volumes)");
        Console.WriteLine("  vss [options]            Volume Shadow Copies & restore points manager (--purge-old)\n");
        Console.WriteLine("Options for scan & clean:");
        Console.WriteLine("  --profile <quick|deep|dev|system>  Pre-configured scan profile");
        Console.WriteLine("  --category <name>        Filter by category (System, Developer, Browser, Application)");
        Console.WriteLine("  --rule <id>              Run specific rule (e.g. sys.temp.user)");
        Console.WriteLine("  --execute                Actually delete files or execute uninstaller");
        Console.WriteLine("  --all                    Include disabled-by-default rules (Recycle Bin, etc.)");
        Console.WriteLine("  --min-age-hours <n>      Only clean files older than N hours (default: 24)");
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
        var (category, ruleId, isAll, minAgeHours, _, profile) = ParseOptions(args);

        var rulesToRun = ResolveRules(engine, registry, category, ruleId, isAll, profile);
        var options = new CleanOptions
        {
            DryRun = true,
            MinFileAge = TimeSpan.FromHours(minAgeHours)
        };

        Console.ForegroundColor = ConsoleColor.Yellow;
        string profileTag = profile.HasValue ? $" [Profile: {profile.Value}]" : "";
        Console.WriteLine($"Scanning {rulesToRun.Count} rules{profileTag} (Min Age: {minAgeHours}h)...");
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
        var (category, ruleId, isAll, minAgeHours, isExecute, profile) = ParseOptions(args);

        var rulesToRun = ResolveRules(engine, registry, category, ruleId, isAll, profile);
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
            bool autoConfirm = args.Contains("--yes") || args.Contains("-y");
            if (!autoConfirm)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("====================================================================");
                Console.WriteLine("⚠️  PERMANENT DELETION CONFIRMATION (LIVE CLEAN)");
                Console.WriteLine($"Target Rules: {rulesToRun.Count}");
                Console.WriteLine("Files will be PERMANENTLY REMOVED from disk. This cannot be undone!");
                Console.WriteLine("====================================================================");
                Console.ResetColor();
                Console.Write("Are you sure you want to permanently delete these files? (type 'yes' or 'y'): ");
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation is not ("yes" or "y"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n[ABORTED] Operation cancelled by user. No files were deleted.\n");
                    Console.ResetColor();
                    return 0;
                }
                Console.WriteLine();
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(">>> LIVE CLEAN MODE: User confirmed deletion. Target files will be deleted permanently.\n");
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
            Console.WriteLine("{0,-24} {1,10:N0} {2,10:N0} {3,14}", r.RuleId, r.DeletedCount, r.SkippedCount, FormatBytes(r.BytesFreed));
        }

        Console.WriteLine(new string('=', 62));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("{0,-24} {1,10:N0} {2,10:N0} {3,14}", "TOTAL", totalDeleted, totalSkipped, FormatBytes(totalFreed));
        Console.ResetColor();

        if (options.DryRun)
        {
            Console.WriteLine("\n[SIMULATION COMPLETE] To execute physical deletion: cleantool clean --execute\n");
        }
        else
        {
            Console.WriteLine("\nCleanup operation finished successfully.\n");
        }

        return 0;
    }

    private static async Task<int> HandleAdviseAsync(CleanerEngine engine, IRuleRegistry registry, string[] args)
    {
        string? workspacePath = args.FirstOrDefault(a => !a.StartsWith('-'));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Running Clean Advisor (Intelligent Heuristic Assessment)...\n");
        Console.ResetColor();

        var advisor = new CleanAdvisorService(engine, registry);
        var report = await advisor.GenerateRecommendationsAsync(workspacePath);

        // Display Health Score Badge
        Console.Write("System Cleanliness Health: ");
        var scoreColor = report.HealthScore.Grade switch
        {
            HealthGrade.A => ConsoleColor.Green,
            HealthGrade.B => ConsoleColor.Cyan,
            HealthGrade.C => ConsoleColor.Yellow,
            _ => ConsoleColor.Red
        };
        Console.ForegroundColor = scoreColor;
        Console.WriteLine($"{report.HealthScore.Score}/100 [Grade {report.HealthScore.Grade}]");
        Console.ResetColor();
        Console.WriteLine($"Status: {report.HealthScore.Summary}\n");

        if (report.HealthScore.IssuesDetected.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Key Issues Detected:");
            foreach (var issue in report.HealthScore.IssuesDetected)
            {
                Console.WriteLine($"  * {issue}");
            }
            Console.ResetColor();
            Console.WriteLine();
        }

        if (report.Recommendations.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("System is in excellent health! No immediate cleanup actions recommended.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("{0,-8} {1,-14} {2,-36} {3,12} {4}", "Priority", "Category", "Recommended Action", "Potential", "Suggested Command");
        Console.WriteLine(new string('-', 100));

        foreach (var rec in report.Recommendations)
        {
            switch (rec.Priority)
            {
                case RecommendationPriority.High:
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("{0,-8} ", "HIGH");
                    break;
                case RecommendationPriority.Medium:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("{0,-8} ", "MEDIUM");
                    break;
                default:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.Write("{0,-8} ", "LOW");
                    break;
            }
            Console.ResetColor();

            Console.Write("{0,-14} ", rec.ActionCategory);
            Console.Write("{0,-36} ", Truncate(rec.Title, 34));
            Console.Write("{0,12} ", rec.EstimatedSavingsBytes > 0 ? FormatBytes(rec.EstimatedSavingsBytes) : "-");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(rec.SuggestedCommand);
            Console.ResetColor();
        }

        Console.WriteLine(new string('=', 100));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total Potential Space Reclaimable: {FormatBytes(report.TotalPotentialSavingsBytes)} across {report.Recommendations.Count} recommendations.");
        Console.WriteLine($"Summary: {report.HighPriorityCount} High Priority | {report.MediumPriorityCount} Medium Priority | {report.LowPriorityCount} Low Priority");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static int HandleInstalledApps(string[] args)
    {
        var uninstaller = new AppUninstallerService();
        var apps = uninstaller.GetInstalledApplications();

        string? search = null;
        bool onlyBroken = false;
        string sortBy = "name";

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            if (a == "--search" && i + 1 < args.Length)
                search = args[++i];
            else if (a == "--broken")
                onlyBroken = true;
            else if (a == "--sort" && i + 1 < args.Length)
                sortBy = args[++i].ToLowerInvariant();
        }

        var query = apps.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a => a.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                     a.Publisher.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (onlyBroken)
        {
            query = query.Where(a => a.IsBroken);
        }

        query = sortBy == "size" 
            ? query.OrderByDescending(a => a.EstimatedSizeBytes) 
            : query.OrderBy(a => a.DisplayName);

        var list = query.ToList();

        Console.WriteLine($"Showing {list.Count} applications:\n");
        Console.WriteLine("{0,-34} {1,-14} {2,-18} {3,10} {4}", "Application Name", "Version", "Publisher", "Size", "Status");
        Console.WriteLine(new string('-', 95));

        int brokenCount = 0;
        foreach (var app in list.Take(40))
        {
            Console.Write("{0,-34} {1,-14} {2,-18} ", 
                Truncate(app.DisplayName, 32), 
                Truncate(app.DisplayVersion, 12), 
                Truncate(app.Publisher, 16));

            Console.Write("{0,10} ", app.EstimatedSizeBytes > 0 ? FormatBytes(app.EstimatedSizeBytes) : "-");

            if (app.IsBroken)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Broken Path");
                brokenCount++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Installed");
            }
            Console.ResetColor();
        }

        if (list.Count > 40)
        {
            Console.WriteLine($"... and {list.Count - 40} more applications.\n");
        }

        Console.WriteLine(new string('=', 95));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total: {list.Count} applications shown.");
        if (brokenCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Notice: {brokenCount} applications have missing directories/uninstallers.");
        }
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static int HandleResiduals(string[] args)
    {
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Usage: cleantool residuals <app-name>");
            Console.ResetColor();
            return 1;
        }

        string appName = args[0];
        var uninstaller = new AppUninstallerService();
        var installed = uninstaller.GetInstalledApplications();

        var app = installed.FirstOrDefault(a => a.DisplayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                  ?? new InstalledAppInfo
                  {
                      Id = appName,
                      DisplayName = appName,
                      UninstallString = ""
                  };

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Scanning Registry & Disk residual footprint for '{app.DisplayName}'...\n");
        Console.ResetColor();

        var report = uninstaller.ScanAppResiduals(app);

        Console.WriteLine($"Registry Keys Found: {report.RegistryKeysFound.Count}");
        foreach (var k in report.RegistryKeysFound)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [REG] {k}");
            Console.ResetColor();
        }

        Console.WriteLine($"\nResidual Folders Found: {report.DirectoriesFound.Count}");
        foreach (var d in report.DirectoriesFound)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [DIR] {d.FolderPath} ({FormatBytes(d.EstimatedSizeBytes)}, {d.FileCount} files)");
            Console.ResetColor();
        }

        Console.WriteLine(new string('=', 70));
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Total Residual Trace: {FormatBytes(report.TotalResidualSizeBytes)} in {report.DirectoriesFound.Count} folders.");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleUninstallAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Usage: cleantool uninstall <app-name> [--execute] [--no-quiet]");
            Console.ResetColor();
            return 1;
        }

        string appName = args[0];
        bool isExecute = args.Contains("--execute");
        bool isQuiet = !args.Contains("--no-quiet");

        var uninstaller = new AppUninstallerService();
        var installed = uninstaller.GetInstalledApplications();

        var app = installed.FirstOrDefault(a => a.DisplayName.Contains(appName, StringComparison.OrdinalIgnoreCase));
        if (app == null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"No installed application matching '{appName}' was found.");
            Console.ResetColor();
            return 1;
        }

        var (exe, uninstArgs) = uninstaller.BuildUninstallCommand(app, quiet: isQuiet);
        Console.WriteLine($"Target Application: {app.DisplayName}");
        Console.WriteLine($"Publisher:          {app.Publisher}");
        Console.WriteLine($"Version:            {app.DisplayVersion}");
        Console.WriteLine($"Uninstaller:        {exe} {uninstArgs}\n");

        if (isExecute)
        {
            bool autoConfirm = args.Contains("--yes") || args.Contains("-y");
            if (!autoConfirm)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("====================================================================");
                Console.WriteLine($"⚠️  APPLICATION UNINSTALL CONFIRMATION: '{app.DisplayName}'");
                Console.WriteLine("This will execute the software uninstaller on your operating system.");
                Console.WriteLine("====================================================================");
                Console.ResetColor();
                Console.Write("Are you sure you want to proceed with uninstallation? (type 'yes' or 'y'): ");
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation is not ("yes" or "y"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n[ABORTED] Uninstallation cancelled by user.\n");
                    Console.ResetColor();
                    return 0;
                }
                Console.WriteLine();
            }
        }

        var result = await uninstaller.UninstallAppAsync(app, quiet: isQuiet, dryRun: !isExecute);

        if (result.WasDryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine(result.Message);
            Console.WriteLine("To perform actual uninstallation, append --execute flag.");
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = result.Success ? ConsoleColor.Green : ConsoleColor.Red;
            Console.WriteLine(result.Message);
            Console.ResetColor();
        }

        Console.WriteLine();
        return result.Success ? 0 : 1;
    }

    private static int HandleLeftoverFolders()
    {
        var uninstaller = new AppUninstallerService();
        Console.WriteLine("Scanning %APPDATA% and %LOCALAPPDATA% for leftover residual folders...\n");

        var leftovers = uninstaller.DetectLeftoverFolders();

        if (leftovers.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("No unassociated residual application folders found.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("{0,-30} {1,12} {2,10} {3}", "Leftover Folder", "Size", "Files", "Full Path");
        Console.WriteLine(new string('-', 95));

        long totalSize = 0;
        foreach (var l in leftovers.Take(25))
        {
            totalSize += l.EstimatedSizeBytes;
            Console.WriteLine("{0,-30} {1,12} {2,10:N0} {3}",
                Truncate(l.FolderName, 28),
                FormatBytes(l.EstimatedSizeBytes),
                l.FileCount,
                Truncate(l.FolderPath, 40));
        }

        if (leftovers.Count > 25)
        {
            Console.WriteLine($"... and {leftovers.Count - 25} more residual folders.");
        }

        Console.WriteLine(new string('=', 95));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Found {leftovers.Count} candidate leftover folders totaling {FormatBytes(totalSize)}.");
        Console.WriteLine("Inspect individual folders before manual removal.");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleLargeFilesAsync(string[] args)
    {
        string targetPath = args.FirstOrDefault(a => !a.StartsWith('-')) ?? Environment.CurrentDirectory;
        int minMb = 100;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--min-mb" && i + 1 < args.Length && int.TryParse(args[i + 1], out var mb))
                minMb = mb;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Scanning '{targetPath}' for files >= {minMb} MB...\n");
        Console.ResetColor();

        var scanner = new LargeFileScannerService();
        var files = await scanner.ScanLargeFilesAsync(targetPath, minSizeBytes: (long)minMb * 1024 * 1024);

        if (files.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"No files >= {minMb} MB found.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("{0,-12} {1,12} {2,-30} {3}", "Category", "Size", "File Name", "Full Path");
        Console.WriteLine(new string('-', 95));

        long totalBytes = 0;
        foreach (var f in files)
        {
            totalBytes += f.SizeBytes;
            var catColor = f.Category switch
            {
                LargeFileCategory.DiskImage => ConsoleColor.Magenta,
                LargeFileCategory.Archive => ConsoleColor.Yellow,
                LargeFileCategory.Database => ConsoleColor.Red,
                LargeFileCategory.Media => ConsoleColor.Cyan,
                _ => ConsoleColor.White
            };

            Console.ForegroundColor = catColor;
            Console.Write("{0,-12} ", f.Category);
            Console.ResetColor();

            Console.WriteLine("{0,12} {1,-30} {2}",
                FormatBytes(f.SizeBytes),
                Truncate(f.FileName, 28),
                Truncate(f.FullPath, 45));
        }

        Console.WriteLine(new string('=', 95));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total: {files.Count} large files totaling {FormatBytes(totalBytes)}.");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleEmptyDirsAsync(string[] args)
    {
        string targetPath = args.FirstOrDefault(a => !a.StartsWith('-')) ?? Environment.CurrentDirectory;
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Scanning '{targetPath}' for empty directories...\n");
        Console.ResetColor();

        var scanner = new EmptyFolderScannerService();
        var folders = await scanner.FindEmptyFoldersAsync(targetPath);

        if (folders.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("No empty directory trees found.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("{0,-35} {1}", "Folder Name", "Full Path");
        Console.WriteLine(new string('-', 85));

        foreach (var f in folders.Take(40))
        {
            Console.WriteLine("{0,-35} {1}", Truncate(f.Name, 33), f.Path);
        }

        if (folders.Count > 40)
        {
            Console.WriteLine($"... and {folders.Count - 40} more empty folders.");
        }

        Console.WriteLine(new string('=', 85));
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Found {folders.Count} empty directory branches.");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleAnalyzeAsync(DiskAnalyzerService analyzer, string[] args)
    {
        string? targetPath = args.FirstOrDefault(a => !a.StartsWith('-'));
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            var drives = analyzer.GetDrives();
            Console.WriteLine("{0,-8} {1,-14} {2,12} {3,12} {4,12} {5,8}", "Drive", "Format", "Total", "Free", "Used", "Used %");
            Console.WriteLine(new string('-', 72));

            foreach (var d in drives)
            {
                var color = d.UsedPercentage > 90 ? ConsoleColor.Red : d.UsedPercentage > 75 ? ConsoleColor.Yellow : ConsoleColor.Green;
                Console.Write("{0,-8} {1,-14} {2,12} {3,12} {4,12} ", d.DriveName, d.DriveFormat, FormatBytes(d.TotalBytes), FormatBytes(d.FreeBytes), FormatBytes(d.UsedBytes));
                Console.ForegroundColor = color;
                Console.WriteLine("{0,7:F1}%", d.UsedPercentage);
                Console.ResetColor();
            }

            Console.WriteLine("\nTo inspect a directory: cleantool analyze C:\\");
            return 0;
        }

        Console.WriteLine($"Analyzing storage breakdown for: {targetPath} (Top 10 items)...\n");
        var node = await analyzer.AnalyzeDirectoryAsync(targetPath, maxDepth: 2);

        Console.WriteLine("{0,-45} {1,14} {2,10}", "Path", "Size", "Files");
        Console.WriteLine(new string('-', 72));
        PrintNode(node, 0);
        Console.WriteLine();
        return 0;
    }

    private static void PrintNode(DirectoryAnalysisNode node, int indent)
    {
        string prefix = new string(' ', indent * 2);
        string name = prefix + (string.IsNullOrEmpty(node.Name) ? node.FullPath : node.Name);
        Console.WriteLine("{0,-45} {1,14} {2,10:N0}", Truncate(name, 43), FormatBytes(node.TotalSizeBytes), node.FileCount);

        foreach (var child in node.Children.OrderByDescending(c => c.TotalSizeBytes).Take(10))
        {
            PrintNode(child, indent + 1);
        }
    }

    [global::System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static int HandleStartup()
    {
        var startup = new StartupManagerService();
        var entries = startup.GetStartupEntries();

        Console.WriteLine("{0,-30} {1,-18} {2,-10} {3}", "Name", "Location", "Valid?", "Command / Path");
        Console.WriteLine(new string('-', 95));

        int invalidCount = 0;
        foreach (var e in entries)
        {
            Console.Write("{0,-30} {1,-18} ", Truncate(e.Name, 28), e.LocationType);
            if (e.FileExists)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("{0,-10} ", "Valid");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("{0,-10} ", "Broken");
                invalidCount++;
            }
            Console.ResetColor();
            Console.WriteLine(Truncate(e.Command, 35));
        }

        Console.WriteLine(new string('=', 95));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total: {entries.Count} startup items ({invalidCount} invalid / broken).");
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleDuplicatesAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Error: Please provide a directory to scan for duplicates.");
            Console.ResetColor();
            return 1;
        }

        string root = args[0];
        Console.WriteLine($"Scanning for duplicate files in: {root}...");

        var finder = new DuplicateFinderService();
        var dupes = await finder.FindDuplicatesAsync(root);

        if (dupes.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("No duplicate files found!");
            Console.ResetColor();
            return 0;
        }

        long totalWasted = dupes.Sum(d => d.ReclaimableBytes);
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\nFound {dupes.Count} duplicate groups wasting {FormatBytes(totalWasted)}:\n");
        Console.ResetColor();

        foreach (var group in dupes.Take(15))
        {
            Console.WriteLine($"Hash: {group.Sha256Hash[..12]}... | Size: {FormatBytes(group.FileSizeBytes)} each | Wasted: {FormatBytes(group.ReclaimableBytes)}");
            foreach (var f in group.FilePaths)
            {
                Console.WriteLine($"  - {f}");
            }
            Console.WriteLine();
        }

        return 0;
    }

    private static async Task<int> HandleMemoryAsync()
    {
        Console.WriteLine("Analyzing and optimizing system memory working sets...");
        var memService = new MemoryOptimizerService();
        var snapshot = memService.GetSystemMemorySnapshot();
        Console.WriteLine($"Current System RAM: {FormatBytes((long)snapshot.UsedPhysicalBytes)} / {FormatBytes((long)snapshot.TotalPhysicalBytes)} ({snapshot.MemoryLoadPercent}% in use)");

        var result = await memService.OptimizeWorkingSetsAsync(purgeStandby: true);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nMemory Optimization Complete:");
        Console.WriteLine($"  Processes Trimmed:   {result.ProcessesOptimized}");
        Console.WriteLine($"  Standby Cache:       {(result.StandbyPurged ? "Purged successfully" : "Skipped (Run as Admin for standby cache purge)")}");
        Console.WriteLine($"  Working Set Before:  {FormatBytes(result.InitialWorkingSetBytes)}");
        Console.WriteLine($"  Working Set After:   {FormatBytes(result.FinalWorkingSetBytes)}");
        Console.WriteLine($"  Working Set Trimmed: {FormatBytes(result.ReclaimedBytes)}");
        if (result.InitialMemory != null && result.FinalMemory != null)
        {
            Console.WriteLine($"  System RAM In-Use:   {result.InitialMemory.MemoryLoadPercent}% -> {result.FinalMemory.MemoryLoadPercent}%");
            long sysReclaimed = (long)result.FinalMemory.AvailablePhysicalBytes - (long)result.InitialMemory.AvailablePhysicalBytes;
            if (sysReclaimed > 0)
            {
                Console.WriteLine($"  Free RAM Increased:  +{FormatBytes(sysReclaimed)}");
            }
        }
        Console.WriteLine();
        Console.ResetColor();
        return 0;
    }

    private static async Task<int> HandleDevWorkspacesAsync(string[] args)
    {
        string rootPath = args.FirstOrDefault(a => !a.StartsWith('-')) ?? Environment.CurrentDirectory;
        int dormantDays = 30;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dormant-days" && i + 1 < args.Length && int.TryParse(args[i + 1], out var d))
                dormantDays = d;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Scanning Developer Workspaces in: {rootPath} (Dormant threshold: {dormantDays} days)...\n");
        Console.ResetColor();

        var devService = new DevWorkspaceService();
        var repos = await devService.ScanWorkspacesAsync(rootPath, dormantDaysThreshold: dormantDays);

        if (repos.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No Git repositories found in target directory.");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("{0,-25} {1,-14} {2,-16} {3,12} {4,10}", "Repository", "State", "Last Activity", "Build Waste", "Dirty?");
        Console.WriteLine(new string('-', 84));

        long totalReclaimable = 0;
        int dormantCount = 0;

        foreach (var r in repos)
        {
            totalReclaimable += r.TotalReclaimableBytes;
            if (r.State == DevRepoState.Dormant) dormantCount++;

            Console.Write("{0,-25} ", Truncate(r.RepoName, 24));

            var stateColor = r.State switch
            {
                DevRepoState.Active => ConsoleColor.Green,
                DevRepoState.Dormant => ConsoleColor.Yellow,
                _ => ConsoleColor.Magenta
            };

            Console.ForegroundColor = stateColor;
            Console.Write("{0,-14} ", r.State);
            Console.ResetColor();

            string lastActivityStr = $"{(int)(DateTime.Now - r.LastActivityTime).TotalDays}d ago";
            Console.Write("{0,-16} {1,12} ", lastActivityStr, FormatBytes(r.TotalReclaimableBytes));

            if (r.State == DevRepoState.DirtyWorktree)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("YES (Protected)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Clean");
            }
            Console.ResetColor();
        }

        Console.WriteLine(new string('=', 84));
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Total: {repos.Count} repositories. Reclaimable build cache: {FormatBytes(totalReclaimable)}");
        if (dormantCount > 0)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Notice: {dormantCount} repos are Dormant (> {dormantDays} days inactive).");
        }
        Console.ResetColor();
        Console.WriteLine();
        return 0;
    }

    private static (string? Category, string? RuleId, bool IsAll, int MinAgeHours, bool IsExecute, ScanProfile? Profile) ParseOptions(string[] args)
    {
        string? category = null;
        string? ruleId = null;
        bool isAll = false;
        int minAgeHours = 24;
        bool isExecute = false;
        ScanProfile? profile = null;

        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i].ToLowerInvariant();
            if (a == "--category" && i + 1 < args.Length)
                category = args[++i];
            else if (a == "--rule" && i + 1 < args.Length)
                ruleId = args[++i];
            else if (a == "--profile" && i + 1 < args.Length)
            {
                var profStr = args[++i].ToLowerInvariant();
                profile = profStr switch
                {
                    "quick" => ScanProfile.Quick,
                    "deep" => ScanProfile.Deep,
                    "dev" or "developer" => ScanProfile.Developer,
                    "system" or "sys" => ScanProfile.SystemOnly,
                    _ => null
                };
            }
            else if (a == "--all")
                isAll = true;
            else if (a == "--execute")
                isExecute = true;
            else if (a == "--min-age-hours" && i + 1 < args.Length && int.TryParse(args[++i], out var hours))
                minAgeHours = hours;
        }

        return (category, ruleId, isAll, minAgeHours, isExecute, profile);
    }

    private static List<ICleanerRule> ResolveRules(
        CleanerEngine engine, 
        IRuleRegistry registry, 
        string? category, 
        string? ruleId, 
        bool isAll, 
        ScanProfile? profile)
    {
        if (profile.HasValue)
        {
            return engine.GetRulesForProfile(profile.Value).ToList();
        }

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
        while (Math.Round(number / 1024) >= 1 && counter < suffixes.Length - 1)
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

    private static async Task<int> HandleShredAsync(string[] args)
    {
        if (args.Length == 0 || args[0].StartsWith("-"))
        {
            Console.WriteLine("Usage: cleantool shred <file-or-dir> [--method <dod|quick|gutmann>] [--execute] [--yes|-y]");
            Console.WriteLine("  --method <name>   dod (3 passes, default), quick (1 pass), gutmann (7 passes)");
            Console.WriteLine("  --execute         Perform actual overwriting and destruction (Dry-run by default)");
            Console.WriteLine("  --yes, -y         Skip interactive confirmation prompt");
            return 1;
        }

        var targetPath = args[0];
        bool isExecute = args.Contains("--execute");
        bool autoConfirm = args.Contains("--yes") || args.Contains("-y");
        var method = ShredMethod.DoD_5220_22_M;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--method" && i + 1 < args.Length)
            {
                method = args[++i].ToLowerInvariant() switch
                {
                    "quick" => ShredMethod.QuickZero,
                    "gutmann" => ShredMethod.GutmannLite,
                    _ => ShredMethod.DoD_5220_22_M
                };
            }
        }

        var shredder = new FileShredderService();
        var fullPath = Path.GetFullPath(targetPath);

        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Target path does not exist: '{fullPath}'");
            Console.ResetColor();
            return 1;
        }

        bool isDir = Directory.Exists(fullPath);

        if (!isExecute)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($">>> DRY-RUN MODE: Simulating shred of {(isDir ? "directory" : "file")}: {fullPath}");
            Console.WriteLine($"Method: {method} (DoD Standard)");
            Console.WriteLine("No data will be overwritten or deleted. Run with '--execute' to perform secure wipe.\n");
            Console.ResetColor();

            var dryResult = isDir
                ? await shredder.ShredDirectoryAsync(fullPath, new ShredOptions { DryRun = true, Method = method })
                : await shredder.ShredFileAsync(fullPath, new ShredOptions { DryRun = true, Method = method });

            Console.WriteLine($"Target: {dryResult.TargetPath}");
            Console.WriteLine($"Files: {dryResult.FilesShredded:N0}");
            Console.WriteLine($"Size: {FormatBytes(dryResult.TotalBytesShredded)}");
            Console.WriteLine($"Passes: {dryResult.PassesPerformed}");
            return 0;
        }

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("====================================================================");
            Console.WriteLine("⚠️  PERMANENT CRYPTOGRAPHIC FILE SHREDDER (DOD 5220.22-M)");
            Console.WriteLine($"Target: {fullPath}");
            Console.WriteLine($"Method: {method}");
            Console.WriteLine("Files will be OVERWRITTEN WITH BYTE PATTERNS, TRUNCATED, AND DESTROYED.");
            Console.WriteLine("THIS DATA CANNOT BE RECOVERED BY ANY FORENSIC OR UNDELETE SOFTWARE!");
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.Write("Are you absolutely sure you want to permanently shred this? (type 'yes' or 'y'): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm is not ("yes" or "y"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ABORTED] Shred operation cancelled by user. No files were modified.\n");
                Console.ResetColor();
                return 0;
            }
            Console.WriteLine();
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($">>> SHREDDING IN PROGRESS: {fullPath}...");
        Console.ResetColor();

        var result = isDir
            ? await shredder.ShredDirectoryAsync(fullPath, new ShredOptions { DryRun = false, Method = method })
            : await shredder.ShredFileAsync(fullPath, new ShredOptions { DryRun = false, Method = method });

        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Shred completed!");
            Console.WriteLine($"Files obliterated: {result.FilesShredded:N0}");
            Console.WriteLine($"Total bytes shredded: {FormatBytes(result.TotalBytesShredded)}");
            Console.WriteLine($"Overwriting passes: {result.PassesPerformed} ({result.Method})");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Shred failed: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleDismAsync(string[] args)
    {
        bool resetBase = args.Contains("--reset-base");
        bool isExecute = args.Contains("--execute");
        bool autoConfirm = args.Contains("--yes") || args.Contains("-y");

        var dism = new DismComponentService();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("Analyzing WinSxS Component Store via DISM (this may take a moment)...");
        Console.ResetColor();

        var analysis = await dism.AnalyzeComponentStoreAsync();
        if (analysis.Success)
        {
            Console.WriteLine("\nWinSxS Component Store Analysis:");
            Console.WriteLine(new string('-', 55));
            Console.WriteLine("{0,-35} : {1}", "Component Store (WinSxS) Size", analysis.ComponentStoreSize);
            Console.WriteLine("{0,-35} : {1}", "Actual Size of Component Store", analysis.ActualSize);
            Console.WriteLine("{0,-35} : {1}", "Shared with Windows", analysis.SharedWithWindows);
            Console.WriteLine("{0,-35} : {1}", "Backups & Disabled Features", analysis.BackupsAndFeatures);
            Console.WriteLine("{0,-35} : {1}", "Cache & Temporary Data", analysis.CacheAndTemp);
            Console.WriteLine("{0,-35} : {1}", "Superseded Packages Count", analysis.SupersededPackagesCount);
            Console.WriteLine("{0,-35} : {1}", "Date of Last Cleanup", analysis.DateOfLastCleanup);
            Console.Write("{0,-35} : ", "Cleanup Recommended");

            if (analysis.IsCleanupRecommended)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("YES (Cleanup will reclaim space)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("NO (Store is already compact)");
            }
            Console.ResetColor();
            Console.WriteLine(new string('-', 55));
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[NOTICE] DISM Analysis: {analysis.ErrorMessage}");
            Console.ResetColor();
        }

        if (!isExecute)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n>>> DRY-RUN: To execute DISM component store cleanup, run:");
            Console.WriteLine($"    cleantool dism {(resetBase ? "--reset-base " : "")}--execute");
            Console.WriteLine("Note: Administrator privileges required.");
            Console.ResetColor();
            return 0;
        }

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n====================================================================");
            Console.WriteLine("⚠️  DISM COMPONENT STORE CLEANUP CONFIRMATION");
            Console.WriteLine($"ResetBase Enabled: {resetBase}");
            if (resetBase)
            {
                Console.WriteLine("WARNING: With --reset-base, superseded updates cannot be uninstalled!");
            }
            Console.WriteLine("This will execute Windows DISM /StartComponentCleanup.");
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.Write("Are you sure you want to proceed with DISM cleanup? (type 'yes' or 'y'): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm is not ("yes" or "y"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ABORTED] DISM cleanup cancelled by user.\n");
                Console.ResetColor();
                return 0;
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\nExecuting DISM StartComponentCleanup (this may take several minutes)...");
        Console.ResetColor();

        var result = await dism.RunCleanupAsync(resetBase, dryRun: false);
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[SUCCESS] DISM Component Store cleanup completed successfully!");
            Console.ResetColor();
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Console.WriteLine(result.StandardOutput);
            }
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] DISM cleanup failed (Exit code: {result.ExitCode}): {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleWslAsync(string[] args)
    {
        bool isCompact = args.Contains("--compact");
        bool isExecute = args.Contains("--execute");
        bool autoConfirm = args.Contains("--yes") || args.Contains("-y");
        string? targetPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length)
            {
                targetPath = args[++i];
            }
        }

        var wsl = new DockerWslService();
        var vdisks = await wsl.DiscoverWslVdisksAsync();

        Console.WriteLine("Discovered WSL2 / Docker Virtual Disks (ext4.vhdx):");
        Console.WriteLine(new string('-', 85));
        Console.WriteLine("{0,-25} {1,12} {2}", "Distro / Identifier", "Size", "File Path");
        Console.WriteLine(new string('-', 85));

        if (vdisks.Count == 0)
        {
            Console.WriteLine("No WSL2 or Docker ext4.vhdx virtual disks detected in standard locations.");
            return 0;
        }

        foreach (var v in vdisks)
        {
            Console.WriteLine("{0,-25} {1,12} {2}", Truncate(v.DistroName, 24), v.SizeFormatted, v.FilePath);
        }
        Console.WriteLine(new string('-', 85));

        if (!isCompact)
        {
            Console.WriteLine("\nTo compact a virtual disk and reclaim unallocated space, run:");
            Console.WriteLine("    cleantool wsl --compact [--path <vhdx-path>] --execute");
            return 0;
        }

        var selectedDisk = !string.IsNullOrWhiteSpace(targetPath)
            ? vdisks.FirstOrDefault(v => v.FilePath.Equals(targetPath, StringComparison.OrdinalIgnoreCase)) ?? new WslVdiskInfo { DistroName = "Custom", FilePath = targetPath }
            : vdisks.OrderByDescending(v => v.SizeBytes).First();

        if (!isExecute)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n>>> DRY-RUN: Prepared compaction for '{selectedDisk.DistroName}' ({selectedDisk.SizeFormatted}):");
            Console.WriteLine($"Path: {selectedDisk.FilePath}");
            Console.WriteLine("Generated diskpart commands:");
            Console.WriteLine(wsl.GenerateDiskpartScript(selectedDisk.FilePath));
            Console.WriteLine("Run with '--execute' and Administrator privileges to execute diskpart compaction.\n");
            Console.ResetColor();
            return 0;
        }

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n====================================================================");
            Console.WriteLine("⚠️  WSL2 VIRTUAL DISK COMPACTION CONFIRMATION");
            Console.WriteLine($"Target: {selectedDisk.FilePath} ({selectedDisk.SizeFormatted})");
            Console.WriteLine("WSL will be temporarily shut down ('wsl --shutdown').");
            Console.WriteLine("Windows diskpart will attach readonly, compact, and detach the disk.");
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.Write("Are you sure you want to compact this virtual disk? (type 'yes' or 'y'): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm is not ("yes" or "y"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ABORTED] Compaction cancelled by user.\n");
                Console.ResetColor();
                return 0;
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nCompacting '{Path.GetFileName(selectedDisk.FilePath)}' via diskpart (please wait)...");
        Console.ResetColor();

        var result = await wsl.CompactVdiskAsync(selectedDisk.FilePath, dryRun: false);
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] WSL virtual disk successfully compacted!");
            Console.WriteLine($"Initial Size : {FormatBytes(result.InitialSizeBytes)}");
            Console.WriteLine($"Final Size   : {FormatBytes(result.FinalSizeBytes)}");
            Console.WriteLine($"Space Saved  : {FormatBytes(result.ReclaimedBytes)}");
            Console.ResetColor();
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] WSL compact failed: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleDockerAsync(string[] args)
    {
        bool isPrune = args.Contains("--prune");
        bool includeVolumes = args.Contains("--volumes");
        bool isExecute = args.Contains("--execute");
        bool autoConfirm = args.Contains("--yes") || args.Contains("-y");

        var docker = new DockerWslService();
        var status = await docker.GetDockerStatusAsync();

        if (!status.IsDockerInstalled)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[NOTICE] Docker CLI is not found on your system PATH.");
            Console.ResetColor();
            return 0;
        }

        if (!status.IsDockerRunning)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[NOTICE] Docker daemon is currently not running or unreachable: {status.ErrorMessage}");
            Console.ResetColor();
            return 0;
        }

        Console.WriteLine("Docker System Space Utilization (docker system df):");
        Console.WriteLine(new string('-', 68));
        Console.WriteLine("{0,-18} {1,8} {2,8} {3,14} {4,16}", "Type", "Total", "Active", "Size", "Reclaimable");
        Console.WriteLine(new string('-', 68));

        foreach (var item in status.Items)
        {
            Console.WriteLine("{0,-18} {1,8} {2,8} {3,14} {4,16}", item.Type, item.TotalCount, item.ActiveCount, item.Size, item.Reclaimable);
        }
        Console.WriteLine(new string('-', 68));

        if (!isPrune)
        {
            Console.WriteLine("\nTo reclaim Docker space (dangling images, stopped containers, build cache):");
            Console.WriteLine("    cleantool docker --prune [--volumes] --execute");
            return 0;
        }

        if (!isExecute)
        {
            var dryResult = await docker.PruneDockerAsync(includeVolumes, dryRun: true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n{dryResult.Output}");
            Console.WriteLine("Run with '--execute' to perform Docker prune.\n");
            Console.ResetColor();
            return 0;
        }

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n====================================================================");
            Console.WriteLine("⚠️  DOCKER PRUNE CONFIRMATION");
            Console.WriteLine($"Include Volumes: {includeVolumes}");
            Console.WriteLine("Stopped containers, dangling images, build cache will be purged.");
            if (includeVolumes)
            {
                Console.WriteLine("WARNING: Unused local volumes will also be PERMANENTLY REMOVED!");
            }
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.Write("Are you sure you want to prune Docker resources? (type 'yes' or 'y'): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm is not ("yes" or "y"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ABORTED] Docker prune cancelled by user.\n");
                Console.ResetColor();
                return 0;
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\nExecuting Docker prune...");
        Console.ResetColor();

        var result = await docker.PruneDockerAsync(includeVolumes, dryRun: false);
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n[SUCCESS] Docker prune finished successfully!");
            Console.ResetColor();
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                Console.WriteLine(result.Output);
            }
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] Docker prune failed: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleVssAsync(string[] args)
    {
        bool isPurgeOld = args.Contains("--purge-old");
        bool isExecute = args.Contains("--execute");
        bool autoConfirm = args.Contains("--yes") || args.Contains("-y");
        string drive = "C:";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--drive" && i + 1 < args.Length)
            {
                drive = args[++i];
            }
        }

        var vss = new VssManagerService();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Querying Volume Shadow Copies & Restore Points for {drive}...");
        Console.ResetColor();

        var report = await vss.GetShadowCopiesReportAsync(drive);

        if (report.Success)
        {
            Console.WriteLine("\nVolume Shadow Storage Allocation:");
            Console.WriteLine(new string('-', 60));
            foreach (var st in report.StorageUsage)
            {
                Console.WriteLine("{0,-20} : {1}", "Volume", st.ForVolume);
                Console.WriteLine("{0,-20} : {1}", "Used Storage", st.UsedSpace);
                Console.WriteLine("{0,-20} : {1}", "Allocated Storage", st.AllocatedSpace);
                Console.WriteLine("{0,-20} : {1}", "Maximum Storage", st.MaximumSpace);
                Console.WriteLine(new string('-', 60));
            }

            Console.WriteLine($"Shadow Copies Count: {report.ShadowCopies.Count}");
            if (report.ShadowCopies.Count > 0)
            {
                Console.WriteLine("\n{0,-38} {1,-24} {2}", "Shadow Copy ID", "Creation Time", "Volume");
                Console.WriteLine(new string('-', 75));
                foreach (var sc in report.ShadowCopies)
                {
                    Console.WriteLine("{0,-38} {1,-24} {2}", sc.ShadowCopyId, sc.CreationTime, sc.OriginalVolume);
                }
                Console.WriteLine(new string('-', 75));
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[NOTICE] VSS Query: {report.ErrorMessage}");
            Console.ResetColor();
        }

        if (!isPurgeOld)
        {
            Console.WriteLine("\nTo safely purge the oldest shadow copy while retaining recent restore points:");
            Console.WriteLine($"    cleantool vss --purge-old [--drive {drive}] --execute");
            return 0;
        }

        if (!isExecute)
        {
            var dryResult = await vss.PurgeOldestShadowAsync(drive, dryRun: true);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n{dryResult.Output}");
            Console.WriteLine("Run with '--execute' and Administrator privileges to purge.\n");
            Console.ResetColor();
            return 0;
        }

        if (!autoConfirm)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("\n====================================================================");
            Console.WriteLine("⚠️  VSS SHADOW COPY PURGE CONFIRMATION");
            Console.WriteLine($"Drive: {drive}");
            Console.WriteLine("This will permanently delete the OLDEST restore point/shadow copy.");
            Console.WriteLine("Recent restore points will remain intact.");
            Console.WriteLine("====================================================================");
            Console.ResetColor();
            Console.Write("Are you sure you want to delete the oldest shadow copy? (type 'yes' or 'y'): ");
            var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (confirm is not ("yes" or "y"))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[ABORTED] VSS purge cancelled by user.\n");
                Console.ResetColor();
                return 0;
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\nPurging oldest shadow copy on {drive}...");
        Console.ResetColor();

        var result = await vss.PurgeOldestShadowAsync(drive, dryRun: false);
        if (result.Success)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n[SUCCESS] Oldest shadow copy on {drive} purged successfully!");
            Console.ResetColor();
            if (!string.IsNullOrWhiteSpace(result.Output))
            {
                Console.WriteLine(result.Output);
            }
            return 0;
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[ERROR] VSS purge failed: {result.ErrorMessage}");
            Console.ResetColor();
            return 1;
        }
    }

    private static int HandleSentinel()
    {
        var sentinel = new StorageSentinelService();
        var report = sentinel.EvaluateSystemDrives();

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("STORAGE SENTINEL - REAL-TIME DISK MARGIN WATCHDOG");
        Console.ResetColor();
        Console.WriteLine($"Report Time: {report.Timestamp:yyyy-MM-dd HH:mm:ss}\n");

        Console.WriteLine("{0,-8} {1,-16} {2,12} {3,12} {4,10} {5,-10}", "Drive", "Label", "Total", "Free", "% Free", "Status");
        Console.WriteLine(new string('-', 72));

        foreach (var d in report.Drives)
        {
            var statusColor = d.AlertLevel switch
            {
                DriveAlertLevel.Critical => ConsoleColor.Red,
                DriveAlertLevel.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.Green
            };

            Console.Write("{0,-8} {1,-16} {2,12} {3,12} {4,9:0.0}% ", 
                d.DriveName, Truncate(d.DriveLabel, 15), d.TotalFormatted, d.FreeFormatted, d.PercentFree);

            Console.ForegroundColor = statusColor;
            Console.WriteLine("{0,-10}", d.AlertLevel.ToString().ToUpperInvariant());
            Console.ResetColor();

            if (!string.IsNullOrWhiteSpace(d.AlertMessage))
            {
                Console.ForegroundColor = statusColor;
                Console.WriteLine($"   └─ {d.AlertMessage}");
                Console.ResetColor();
            }
        }
        Console.WriteLine(new string('=', 72));

        Console.Write("Overall Sentinel Health: ");
        var overallColor = report.OverallStatus switch
        {
            DriveAlertLevel.Critical => ConsoleColor.Red,
            DriveAlertLevel.Warning => ConsoleColor.Yellow,
            _ => ConsoleColor.Green
        };
        Console.ForegroundColor = overallColor;
        Console.WriteLine(report.OverallStatus.ToString().ToUpperInvariant());
        Console.ResetColor();

        Console.WriteLine("\nActionable Recommendations:");
        foreach (var rec in report.ActionableRecommendations)
        {
            Console.WriteLine($"  {rec}");
        }
        Console.WriteLine();

        return report.OverallStatus == DriveAlertLevel.Critical ? 2 : 0;
    }
}
