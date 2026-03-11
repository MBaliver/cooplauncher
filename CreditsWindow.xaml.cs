using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace RemotePlayLauncher;

public partial class CreditsWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    public CreditsWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int on = 1;
        DwmSetWindowAttribute(hwnd, 20, ref on, 4);
        try
        {
            int bg = 0x181112; 
            DwmSetWindowAttribute(hwnd, 35, ref bg, 4);
        }
        catch { }
    }

    private void Github_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo("https://github.com/MBaliver/") { UseShellExecute = true });
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
