using System.Windows;
using ZeroUI.Core.Theme;
using ZeroUI.Wpf.Theme;

namespace CleanTool.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // 1. Initialize ZeroUI Standard Theme Engine & Skin Manager
        ZeroSkinManager.ResetToDefaults();
        ZeroThemeEngine.Initialize(this, "obsidian_dark");
        ZeroWpfStyles.ApplyStyles(this);

        // 2. Map and synchronize ZeroUI theme tokens to application resources
        SyncZeroUiTokens();
        ZeroWpfTheme.ThemeChanged += () =>
        {
            Dispatcher.BeginInvoke(new Action(SyncZeroUiTokens));
        };

        base.OnStartup(e);
    }

    private void SyncZeroUiTokens()
    {
        Resources["BgDarkBrush"] = ZeroWpfTheme.BgPrimary;
        Resources["BgCardBrush"] = ZeroWpfTheme.BgCard;
        Resources["BgInputBrush"] = ZeroWpfTheme.BgInput;
        Resources["BgHoverBrush"] = ZeroWpfTheme.BgHover;
        Resources["BgActiveBrush"] = ZeroWpfTheme.BgActive;
        Resources["BorderDefaultBrush"] = ZeroWpfTheme.BorderDefault;
        Resources["BorderSubtleBrush"] = ZeroWpfTheme.BorderSubtle;
        Resources["PrimaryAccentBrush"] = ZeroWpfTheme.PrimaryAccent;
        Resources["PrimaryAccentDarkBrush"] = ZeroWpfTheme.PrimaryAccentDark;
        Resources["SecondaryAccentBrush"] = ZeroWpfTheme.SecondaryAccent;
        Resources["TextPrimaryBrush"] = ZeroWpfTheme.TextPrimary;
        Resources["TextSecondaryBrush"] = ZeroWpfTheme.TextSecondary;
        Resources["TextMutedBrush"] = ZeroWpfTheme.TextMuted;
        Resources["DangerAccentBrush"] = ZeroWpfTheme.DangerAccent;
        Resources["SuccessAccentBrush"] = ZeroWpfTheme.SuccessAccent;
        Resources["WarningAccentBrush"] = ZeroWpfTheme.WarningAccent;
    }
}
