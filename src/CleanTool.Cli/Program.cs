using System.Text;
using CleanTool.Core.Contracts;
using CleanTool.Core.Engine;
using CleanTool.Core.Models;
using CleanTool.Rules;

namespace CleanTool.Cli;

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
                "mem" => await HandleMemoryAsync(),
                "dev" or "workspaces" => await HandleDevWorkspacesAsync(cmdArgs),
                "advise" or "advisor" => await HandleAdviseAsync(engine, registry, cmdArgs),
                "apps" or "installed" => HandleInstalledApps(cmdArgs),
                "leftovers" => HandleLeftoverFolders(),
                "residuals" or "trace" => HandleResiduals(cmdArgs),
                "uninstall" => await HandleUninstallAsync(cmdArgs),
                "large-files" or "largefiles" => await HandleLargeFilesAsync(cmdArgs),
                "empty-dirs" or "emptydirs" => await HandleEmptyDirsAsync(cmdArgs),
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
        Console.WriteLine("  dev [path]               Scan dev workspaces for dormant repos & build artifacts\n");
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
                Console.WriteLine("⚠️  XÁC NHẬN DỌN DẸP THẬT (LIVE CLEAN CONFIRMATION)");
                Console.WriteLine($"Số rule thực hiện: {rulesToRun.Count}");
                Console.WriteLine("Các file rác sẽ bị XÓA VĨNH VIỄN khỏi hệ thống. Thao tác này KHÔNG THỂ hoàn tác!");
                Console.WriteLine("====================================================================");
                Console.ResetColor();
                Console.Write("Bạn có chắc chắn muốn tiến hành xóa file không? (nhập 'yes' hoặc 'y' để xác nhận): ");
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation is not ("yes" or "y"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n[ĐÃ HỦY] Người dùng đã từ chối xác nhận. Không có file nào bị xóa.\n");
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
                Console.WriteLine($"⚠️  XÁC NHẬN GỠ CÀI ĐẶT: '{app.DisplayName}'");
                Console.WriteLine("Hành động này sẽ thực thi trình gỡ cài đặt phần mềm trên hệ điều hành.");
                Console.WriteLine("====================================================================");
                Console.ResetColor();
                Console.Write("Bạn có chắc chắn muốn gỡ ứng dụng này không? (nhập 'yes' hoặc 'y' để xác nhận): ");
                var confirmation = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (confirmation is not ("yes" or "y"))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\n[ĐÃ HỦY] Người dùng đã từ chối xác nhận. Hủy lệnh gỡ cài đặt.\n");
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
        Console.WriteLine("Optimizing system memory working sets...");
        var memService = new MemoryOptimizerService();
        var result = await memService.OptimizeWorkingSetsAsync();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\nMemory Optimization Complete:");
        Console.WriteLine($"  Processes Optimized: {result.ProcessesOptimized}");
        Console.WriteLine($"  Memory Before:       {FormatBytes(result.InitialWorkingSetBytes)}");
        Console.WriteLine($"  Memory After:        {FormatBytes(result.FinalWorkingSetBytes)}");
        Console.WriteLine($"  RAM Reclaimed:       {FormatBytes(result.ReclaimedBytes)}\n");
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
}
