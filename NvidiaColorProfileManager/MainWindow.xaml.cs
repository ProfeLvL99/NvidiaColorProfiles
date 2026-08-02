using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.DependencyInjection;
using NvidiaColorProfileManager.Models;
using NvidiaColorProfileManager.Services;
using NvidiaColorProfileManager.ViewModels;
using Forms = System.Windows.Forms;

namespace NvidiaColorProfileManager;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly HotkeyService _hotkeyService;
    private readonly IProfileApplierService _profileApplier;
    private readonly IProfileRepository _profileRepository;
    private Forms.NotifyIcon? _trayIcon;
    private bool _isReallyClosing;

    public MainWindow()
    {
        InitializeComponent();
        var sp = App.Services;
        _viewModel = sp.GetRequiredService<MainViewModel>();
        _hotkeyService = sp.GetRequiredService<HotkeyService>();
        _profileApplier = sp.GetRequiredService<IProfileApplierService>();
        _profileRepository = sp.GetRequiredService<IProfileRepository>();
        DataContext = _viewModel;
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _viewModel.ProfilesChanged += RegisterAllHotkeys;
        Loaded += OnLoaded;
        Closing += OnClosing;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _viewModel.InitializeCommand.Execute(null);
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService.Initialize(handle);
        RegisterAllHotkeys();
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        SetupTrayIcon();

        if (App.StartMinimized)
            Hide();
    }

    // ── Profile card handlers ──
    private void ProfileCard_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ColorProfile profile)
            _viewModel.ApplyProfileCommand.Execute(profile);
    }

    private void EditProfileCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ColorProfile profile)
            _viewModel.SelectedProfile = profile;
    }

    private void DeleteProfileCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ColorProfile profile)
            _viewModel.DeleteProfileCommand.Execute(profile);
    }

    // ── Hotkey capture ──
    private void HotkeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;
        if (e.Key == Key.Escape || e.Key == Key.Delete || e.Key == Key.Back)
            _viewModel.ClearHotkey();
        else
        {
            var formatted = HotkeyService.FormatHotkeyFromKeyEvent(e.Key, Keyboard.Modifiers);
            if (formatted != null) _viewModel.SetHotkey(formatted);
        }
    }

    private void HotkeyBox_Focus(object sender, MouseButtonEventArgs e)
    {
        HotkeyBox.Focus();
    }

    // ── Tray ──
    private void SetupTrayIcon()
    {
        var icon = LoadAppIcon();
        _trayIcon = new Forms.NotifyIcon { Text = "NVIDIA Color Profiles", Icon = icon, Visible = true };
        _trayIcon.MouseClick += (s, e) =>
        {
            if (e.Button == Forms.MouseButtons.Right)
                BuildTrayMenu().Show(Forms.Control.MousePosition);
        };
        _trayIcon.DoubleClick += (s, e) => ShowFromTray();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        // Active profile header
        var activeProfile = _viewModel.SelectedProfile;
        if (activeProfile != null)
        {
            var header = new Forms.ToolStripMenuItem($"Active profile: {activeProfile.Name}")
            {
                Enabled = false,
                Font = new System.Drawing.Font(menu.Font, System.Drawing.FontStyle.Bold)
            };
            menu.Items.Add(header);
            menu.Items.Add(new Forms.ToolStripSeparator());
        }

        // Profile list with hotkeys
        bool hasItems = false;
        foreach (var p in _profileRepository.GetAll().Take(10))
        {
            var label = p.Name;
            if (!string.IsNullOrWhiteSpace(p.Hotkey))
                label += $"  ({p.Hotkey})";

            var item = new Forms.ToolStripMenuItem(label)
            {
                Tag = p,
                Checked = activeProfile != null && p.Id == activeProfile.Id
            };
            item.Click += (s, ev) =>
            {
                if (s is Forms.ToolStripMenuItem mi && mi.Tag is ColorProfile pr)
                {
                    _viewModel.ApplyProfileCommand.Execute(pr);
                }
            };
            menu.Items.Add(item);
            hasItems = true;
        }
        if (hasItems) menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Restore", null, (s, e) => ShowFromTray());
        menu.Items.Add("Exit", null, (s, e) => { _isReallyClosing = true; _hotkeyService.Dispose(); _trayIcon?.Dispose(); Application.Current.Shutdown(); });
        return menu;
    }

    private System.Drawing.Icon LoadAppIcon()
    {
        try { var ico = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!); if (ico != null) return ico; } catch { }
        try { var p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icons", "app.ico"); if (File.Exists(p)) return new System.Drawing.Icon(p); } catch { }
        return SystemIcons.Application;
    }

    private void ShowFromTray() { Show(); WindowState = WindowState.Normal; Activate(); }
    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e) { if (!_isReallyClosing) { e.Cancel = true; Hide(); } }
    private void OnClosed(object? sender, EventArgs e) { if (_isReallyClosing) _hotkeyService.Dispose(); _trayIcon?.Dispose(); }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        _isReallyClosing = true;
        _hotkeyService.Dispose();
        _trayIcon?.Dispose();
        Application.Current.Shutdown();
    }

    private void DonateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://ko-fi.com/profelvl99",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }

    private void AuthorLink_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "https://github.com/ProfeLvL99",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch { }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == HotkeyService.WM_HOTKEY) { _hotkeyService.ProcessHotkeyMessage(wParam); handled = true; }
        return IntPtr.Zero;
    }

    private void RegisterAllHotkeys() => _hotkeyService.RegisterAll(_profileRepository.GetAll().Select(p => (p.Id, p.Hotkey)));
    private void OnHotkeyPressed(int id)
    {
        var p = _profileRepository.GetById(id);
        if (p != null) { _profileApplier.ApplyProfile(p); _viewModel.StatusMessage = $"Hotkey: profile '{p.Name}' applied."; }
    }

    // ── Custom Title Bar ──
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            MaximizeBtn_Click(sender, e);
            return;
        }
        DragMove();
    }

    private void MinimizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeBtn_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        // Minimize to tray instead of closing (same as native X button behavior)
        Hide();
    }

    private void Window_StateChanged(object sender, EventArgs e)
    {
        MaximizeBtn.Content = WindowState == WindowState.Maximized ? "❐" : "□";
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        TitleBarGrid.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x2D, 0x2D, 0x2D));
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        TitleBarGrid.Background = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
    }
}
