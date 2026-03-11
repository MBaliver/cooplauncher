using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace RemotePlayLauncher;

public partial class SettingsWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    private readonly List<SteamGame> _allGames;
    private readonly LauncherConfig  _config;
    private SteamGame? _selected;

    public SettingsWindow(List<SteamGame> games, LauncherConfig config)
    {
        InitializeComponent();
        _allGames = games;
        _config   = config;

        // Show current donor
        if (!string.IsNullOrEmpty(config.DonorExePath) && File.Exists(config.DonorExePath))
        {
            CurrentDonorName.Text = Path.GetFileNameWithoutExtension(config.DonorExePath);
            CurrentDonorPath.Text = config.DonorExePath;
        }

        RenderDonorList(_allGames);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, 4);
        try 
        { 
            int bg = 0x181112; // Adjusted to match #121118 background
            DwmSetWindowAttribute(hwnd, 35, ref bg, 4); 
        } catch { }
    }

    // ── Render ───────────────────────────────────────────────────────────────

    private void RenderDonorList(IEnumerable<SteamGame> games)
    {
        DonorList.ItemsSource = games.OrderBy(g => g.Name).ToList();
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private void DonorList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selected = DonorList.SelectedItem as SteamGame;
        if (_selected == null)
        {
            InstallBtn.IsEnabled = false;
            ActionStatus.Text = "";
            return;
        }

        InstallBtn.IsEnabled = true;
        ActionStatus.Text = $"Will install to: {_selected.GameFolder}";
    }

    // ── Install ──────────────────────────────────────────────────────────────

    private void InstallBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;

        // Determine the source exe (this running process)
        var sourceExe = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName;

        if (string.IsNullOrEmpty(sourceExe) || !File.Exists(sourceExe))
        {
            ActionStatus.Text = "Could not locate this launcher's .exe. Run the published build to install.";
            return;
        }

        // Determine the target: replace the donor's primary exe
        var target = string.IsNullOrEmpty(_selected.ExecutablePath)
            ? Path.Combine(_selected.GameFolder, "CoopLauncher.exe")
            : _selected.ExecutablePath;

        try
        {
            // Backup original
            if (File.Exists(target) && !File.Exists(target + ".bak"))
                File.Copy(target, target + ".bak");

            File.Copy(sourceExe, target, overwrite: true);

            _config.DonorExePath = target;
            _config.Save();

            // Refresh current donor display
            CurrentDonorName.Text = Path.GetFileNameWithoutExtension(target);
            CurrentDonorPath.Text = target;
            
            MessageBox.Show(
                $"✅ Installation Complete!\n\nPlease launch \"{_selected.Name}\" from your Steam Library now to start the Coop Launcher and enable Remote Play Together benefits.",
                "Coop Launcher Ready",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Shutdown the launcher so that when the user launches from Steam, there's no conflict
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            ActionStatus.Text = $"⚠  {ex.Message}";
        }
    }

    // ── Search ────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchHint != null)
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

        var q = SearchBox.Text.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allGames
            : _allGames.Where(g => g.Name.ToLowerInvariant().Contains(q)).ToList();
        
        RenderDonorList(filtered);
        _selected = null;
        InstallBtn.IsEnabled = false;
    }
}
