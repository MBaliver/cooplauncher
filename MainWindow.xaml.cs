using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RemotePlayLauncher;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    // Shared HttpClient for all image downloads
    private static readonly HttpClient ImageHttp = new() { Timeout = TimeSpan.FromSeconds(12) };

    private List<SteamGame> _allGames = new();
    private LauncherConfig  _config   = LauncherConfig.Load();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += async (_, _) => await LoadGamesAsync();
        UpdateDonorStatus();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, 4);
        try
        {
            int bg = 0x171717;
            DwmSetWindowAttribute(hwnd, 35, ref bg, 4);
            int fg = 0xF5F5F5;
            DwmSetWindowAttribute(hwnd, 36, ref fg, 4);
        }
        catch { }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private bool IsDonorGame(SteamGame g)
    {
        if (string.IsNullOrEmpty(_config.DonorExePath)) return false;

        // Perfect match on executable
        if (string.Equals(g.ExecutablePath, _config.DonorExePath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Path-based check: is the donor exe inside this game's folder?
        var donor = _config.DonorExePath;
        var folder = g.GameFolder;
        if (!folder.EndsWith(Path.DirectorySeparatorChar)) folder += Path.DirectorySeparatorChar;

        return donor.StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateDonorStatus()
    {
        if (!string.IsNullOrEmpty(_config.DonorExePath) && File.Exists(_config.DonorExePath))
            DonorRun.Text = $"Donor: {Path.GetFileNameWithoutExtension(_config.DonorExePath)}  —  {_config.DonorExePath}";
        else
            DonorRun.Text = "No donor configured — click ⚙ Donor to set up.";
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    private async Task LoadGamesAsync()
    {
        // 0. Ensure donor is set
        if (string.IsNullOrEmpty(_config.DonorExePath) || !File.Exists(_config.DonorExePath))
        {
            var msg = "Welcome to Coop Launcher!\n\nTo enable Remote Play Together benefits, you must first select a 'Donor' game from your Steam library.\n\nWould you like to set one up now?";
            if (MessageBox.Show(msg, "Coop Launcher — Donor Required", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
            {
                var win = new SettingsWindow(_allGames.Count == 0 ? await Task.Run(SteamDiscovery.Discover) : _allGames, _config) { Owner = this };
                if (win.ShowDialog() == true) 
                {
                    _config = LauncherConfig.Load();
                    UpdateDonorStatus();
                }
            }
        }

        LoadingText.Visibility = Visibility.Visible;
        NoResultsText.Visibility = Visibility.Collapsed;
        SubtitleText.Text = "Scanning Steam library…";

        _allGames = await Task.Run(SteamDiscovery.Discover);

        // Exclude the donor game from the games list
        var visible = _allGames.Where(g => !IsDonorGame(g)).ToList();

        LoadingText.Visibility = Visibility.Collapsed;
        SubtitleText.Text = visible.Count > 0
            ? $"{visible.Count} games — click to launch"
            : "No Steam games found. Is Steam installed?";

        RenderList(visible);
    }

    // ── Render ───────────────────────────────────────────────────────────────

    private void RenderList(IEnumerable<SteamGame> games)
    {
        foreach (var b in GameList.Children.OfType<Button>().ToList())
            GameList.Children.Remove(b);

        // Also exclude donor in search results
        var list = games.Where(g => !IsDonorGame(g)).ToList();

        NoResultsText.Visibility = list.Count == 0 && _allGames.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        foreach (var game in list)
            GameList.Children.Add(MakeGameRow(game));
    }

    private Button MakeGameRow(SteamGame game)
    {
        // ── Fallback letter (shown until banner loads)
        var letter = new TextBlock
        {
            Text = game.Name.Length > 0 ? game.Name[0].ToString().ToUpperInvariant() : "?",
            FontSize = 22, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(70, 65, 90)), // Muted purple
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        // ── Banner Rectangle (Inner R = 7, Outer R = 12)
        var bannerFill = new ImageBrush { Stretch = Stretch.UniformToFill };
        var bannerRect = new System.Windows.Shapes.Rectangle
        {
            Width = 130, Height = 61,
            RadiusX = 7, RadiusY = 7,
            Fill = bannerFill,
            Visibility = Visibility.Collapsed
        };
        RenderOptions.SetBitmapScalingMode(bannerRect, BitmapScalingMode.HighQuality);

        var bannerGrid = new Grid { Width = 130, Height = 61 };
        bannerGrid.Children.Add(letter);
        bannerGrid.Children.Add(bannerRect);

        var iconBorder = new Border
        {
            Width = 130, Height = 61,
            CornerRadius = new CornerRadius(7),
            Background = new SolidColorBrush(Color.FromRgb(26, 24, 38)),
            VerticalAlignment = VerticalAlignment.Center,
            Child = bannerGrid
        };

        _ = LoadIconAsync(game.AppId, bannerFill, bannerRect, letter, iconBorder);

        // ── Text stack (#EBEBEB name, #908CAF path)
        _config.ExecutableOverrides.TryGetValue(game.AppId, out var manualPath);
        var displayPath = !string.IsNullOrEmpty(manualPath) ? manualPath
                        : (string.IsNullOrEmpty(game.ExecutablePath) ? game.GameFolder : game.ExecutablePath);

        var nameBlock = new TextBlock
        {
            Text = game.Name,
            FontSize = 16.5, FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(235, 235, 235)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var pathBlock = new TextBlock
        {
            Text = displayPath,
            FontSize = 11.5, FontFamily = new FontFamily("Segoe UI"),
            Foreground = new SolidColorBrush(Color.FromRgb(144, 140, 160)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 5, 0, 0)
        };

        var textStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(18, 0, 0, 0)
        };
        textStack.Children.Add(nameBlock);
        textStack.Children.Add(pathBlock);

        // Chevron arrow
        var arrow = new TextBlock
        {
            Text = "›", FontSize = 22,
            FontFamily = new FontFamily("Segoe UI"),
            // #606060 on #1E1E1E → 3.3:1 (decorative element, not text)
            Foreground = new SolidColorBrush(Color.FromRgb(96, 96, 96)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0)
        };

        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(textStack, 1);
        Grid.SetColumn(arrow, 2);
        row.Children.Add(iconBorder);
        row.Children.Add(textStack);
        row.Children.Add(arrow);

        var btn = new Button { Style = (Style)FindResource("GameRow"), Content = row };
        btn.Click += (_, _) => LaunchGame(game);
        
        // Context menu for right-click
        var cm = new ContextMenu();
        var changeExe = new MenuItem { Header = "Change Executable..." };
        changeExe.Click += (_, _) => PickExecutableFor(game);
        cm.Items.Add(changeExe);
        btn.ContextMenu = cm;

        return btn;
    }

    // ── Icon loading (banner-first) ────────────────────────────────────────

    private async Task LoadIconAsync(string appId, ImageBrush brush, FrameworkElement visuals, TextBlock letter, Border border)
    {
        BitmapImage? bmp = null;

        // 1. Local Steam cache (header.jpg = 460×215, perfect match)
        var localPath = SteamDiscovery.GetLocalIconPath(appId);
        if (localPath != null)
            bmp = await DecodeBytesAsync(await File.ReadAllBytesAsync(localPath));

        // 2. SteamGridDB — high-quality 460×215 banner
        if (bmp == null)
        {
            var gridUrl = await SteamGridDbService.GetGridUrlAsync(appId);
            if (!string.IsNullOrEmpty(gridUrl))
                bmp = await DownloadAndDecodeAsync(gridUrl);
        }

        // 3. Steam CDN header.jpg — same 460×215 format, always available
        if (bmp == null)
            bmp = await DownloadAndDecodeAsync(
                $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/header.jpg");

        if (bmp == null) return;

        brush.ImageSource = bmp;
        visuals.Visibility = Visibility.Visible;
        letter.Visibility = Visibility.Collapsed;
        border.Background = Brushes.Transparent;
    }

    /// <summary>Downloads image bytes with HttpClient then decodes on a threadpool thread.</summary>
    private static async Task<BitmapImage?> DownloadAndDecodeAsync(string url)
    {
        try
        {
            var bytes = await ImageHttp.GetByteArrayAsync(url);
            return await DecodeBytesAsync(bytes);
        }
        catch { return null; }
    }

    /// <summary>Decodes image bytes into a frozen BitmapImage (safe on any thread).</summary>
    private static Task<BitmapImage?> DecodeBytesAsync(byte[] bytes) =>
        Task.Run<BitmapImage?>(() =>
        {
            try
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption    = BitmapCacheOption.OnLoad;
                b.StreamSource   = new MemoryStream(bytes);
                b.DecodePixelWidth = 240;
                b.EndInit();
                b.Freeze();
                return b;
            }
            catch { return null; }
        });

    // ── Launch ───────────────────────────────────────────────────────────────

    private void LaunchGame(SteamGame game)
    {
        // 1. Check for manual override first
        _config.ExecutableOverrides.TryGetValue(game.AppId, out var exe);

        // 2. Fall back to discovered path
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            exe = game.ExecutablePath;

        // 3. Fall back to scanning folder
        if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            exe = Directory.EnumerateFiles(game.GameFolder, "*.exe", SearchOption.TopDirectoryOnly)
                           .FirstOrDefault() ?? string.Empty;

        // 4. Still nothing? Let the user pick
        if (!File.Exists(exe))
        {
            exe = PickExecutableFor(game);
            if (string.IsNullOrEmpty(exe)) return;
        }

        try
        {
            DonorRun.Text = $"Launching {game.Name}…";
            Process.Start(new ProcessStartInfo(exe)
            {
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = true
            });
            WindowState = WindowState.Minimized;
        }
        catch (Exception ex) { DonorRun.Text = $"⚠  {ex.Message}"; }
    }

    private string PickExecutableFor(SteamGame game)
    {
        var diag = new Microsoft.Win32.OpenFileDialog
        {
            Title = $"Select primary .exe for {game.Name}",
            InitialDirectory = game.GameFolder,
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*"
        };

        if (diag.ShowDialog() == true && File.Exists(diag.FileName))
        {
            _config.ExecutableOverrides[game.AppId] = diag.FileName;
            _config.Save();
            // Refresh to update path display in UI if needed
            RenderList(_allGames.Where(g => !IsDonorGame(g))); 
            return diag.FileName;
        }
        return string.Empty;
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

        var q = SearchBox.Text.Trim().ToLowerInvariant();
        var source = string.IsNullOrEmpty(q)
            ? _allGames
            : _allGames.Where(g => g.Name.ToLowerInvariant().Contains(q));

        RenderList(source); // RenderList already strips the donor
    }

    private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        await LoadGamesAsync();
    }

    private void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_allGames, _config) { Owner = this };
        if (win.ShowDialog() == true)
        {
            _config = LauncherConfig.Load();
            UpdateDonorStatus();
            // Re-render to hide newly-configured donor
            _ = LoadGamesAsync();
        }
    }

    private void CreditsBtn_Click(object sender, RoutedEventArgs e)
    {
        new CreditsWindow { Owner = this }.ShowDialog();
    }
}