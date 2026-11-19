using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using xContextGuard.Models;
using xContextGuard.Services;

namespace xContextGuard;

public partial class MainWindow : Window
{
    private record WindowSettings(
        double Left,
        double Top,
        double Width,
        double Height,
        bool IsMaximized,
        bool IsDarkTheme);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;

    private readonly ContextMenuScanner _scanner = new();
    private readonly ContextMenuManager _manager = new();
    private readonly ObservableCollection<MenuEntry> _entries = [];
    private List<MenuEntry> _allEntries = [];
    private bool _isDarkTheme = true;

    private string SettingsFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "xContextGuard", "window.json");

    public MainWindow()
    {
        InitializeComponent();
        EntriesGrid.ItemsSource = _entries;

        RestoreWindowSettings();
        ApplyTheme(_isDarkTheme);

        SourceInitialized += (_, _) =>
        {
            UpdateTitleBarTheme(_isDarkTheme);
        };

        Closing += (_, _) => SaveWindowSettings();
        Loaded += (_, _) => RefreshEntries();
    }

    private void RestoreWindowSettings()
    {
        var workArea = SystemParameters.WorkArea;

        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<WindowSettings>(json);
                if (settings != null)
                {
                    _isDarkTheme = settings.IsDarkTheme;

                    if (settings.IsMaximized)
                    {
                        WindowState = WindowState.Maximized;
                        return;
                    }

                    // Strict bounds validation against current working area
                    double w = Math.Min(Math.Max(settings.Width, MinWidth), workArea.Width);
                    double h = Math.Min(Math.Max(settings.Height, MinHeight), workArea.Height);

                    Width = w;
                    Height = h;
                    Left = Math.Max(workArea.Left, Math.Min(settings.Left, workArea.Right - w));
                    Top = Math.Max(workArea.Top, Math.Min(settings.Top, workArea.Bottom - h));
                    WindowState = WindowState.Normal;
                    return;
                }
            }
        }
        catch
        {
        }

        // Default: Sized comfortably within the work area, title bar fully visible
        double defaultWidth = Math.Min(1140, workArea.Width - 40);
        double defaultHeight = Math.Min(640, workArea.Height - 40);

        Width = defaultWidth;
        Height = defaultHeight;
        Left = workArea.Left + (workArea.Width - defaultWidth) / 2;
        Top = Math.Max(workArea.Top, workArea.Top + (workArea.Height - defaultHeight) / 2);
        WindowState = WindowState.Normal;
        _isDarkTheme = true;
    }

    private void SaveWindowSettings()
    {
        try
        {
            var isMax = WindowState == WindowState.Maximized;
            var bounds = RestoreBounds;
            var settings = new WindowSettings(
                Left: isMax ? bounds.Left : Left,
                Top: isMax ? bounds.Top : Top,
                Width: isMax ? bounds.Width : Width,
                Height: isMax ? bounds.Height : Height,
                IsMaximized: isMax,
                IsDarkTheme: _isDarkTheme
            );

            var dir = Path.GetDirectoryName(SettingsFilePath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFilePath, JsonSerializer.Serialize(settings));
        }
        catch
        {
        }
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        _isDarkTheme = !_isDarkTheme;
        ApplyTheme(_isDarkTheme);
        UpdateTitleBarTheme(_isDarkTheme);
    }

    private void ApplyTheme(bool isDark)
    {
        // Canvas & Panels
        SetThemeBrush("WindowBrush", isDark ? "#0D1117" : "#F4F6FA");
        SetThemeBrush("PanelBrush", isDark ? "#161B22" : "#FFFFFF");
        SetThemeBrush("PanelAltBrush", isDark ? "#1E242E" : "#F0F3F8");
        SetThemeBrush("PanelHoverBrush", isDark ? "#28303E" : "#E4E9F2");

        // Borders
        SetThemeBrush("BorderBrush", isDark ? "#303642" : "#D0D7DE");
        SetThemeBrush("BorderHoverBrush", isDark ? "#485366" : "#AFC0D2");

        // Text & Accents
        SetThemeBrush("TextBrush", isDark ? "#F0F3F6" : "#1F2328");
        SetThemeBrush("MutedBrush", isDark ? "#8B949E" : "#656D76");
        SetThemeBrush("AccentBrush", isDark ? "#7C6EF6" : "#6355DC");
        SetThemeBrush("AccentHoverBrush", isDark ? "#9184FF" : "#5244C9");

        // Selection
        SetThemeBrush("SelectionBrush", isDark ? "#283149" : "#E6E8FD");
        SetThemeBrush("SelectionTextBrush", isDark ? "#FFFFFF" : "#1F2328");

        // ScrollBar Thumb
        SetThemeBrush("ScrollBarThumbBrush", isDark ? "#3C4456" : "#C5CCD6");
        SetThemeBrush("ScrollBarThumbHoverBrush", isDark ? "#5A657E" : "#8C95A3");

        // Danger Action
        SetThemeBrush("DangerBackgroundBrush", isDark ? "#381A22" : "#FFEBE9");
        SetThemeBrush("DangerBorderBrush", isDark ? "#662432" : "#FF8182");
        SetThemeBrush("DangerHoverBrush", isDark ? "#4D202B" : "#FFD8D6");
        SetThemeBrush("DangerTextBrush", isDark ? "#FF7588" : "#CF222E");

        // Status Indicators
        SetThemeBrush("StatusEnabledBgBrush", isDark ? "#132F23" : "#DCFCE7");
        SetThemeBrush("StatusEnabledTextBrush", isDark ? "#4ADE80" : "#166534");
        SetThemeBrush("StatusDisabledBgBrush", isDark ? "#2B2314" : "#FEF3C7");
        SetThemeBrush("StatusDisabledTextBrush", isDark ? "#FBBF24" : "#92400E");

        // WPF System selection overrides
        SetResourceBrush(SystemColors.HighlightBrushKey, isDark ? "#283149" : "#E6E8FD");
        SetResourceBrush(SystemColors.HighlightTextBrushKey, isDark ? "#FFFFFF" : "#1F2328");
        SetResourceBrush(SystemColors.InactiveSelectionHighlightBrushKey, isDark ? "#283149" : "#E6E8FD");
        SetResourceBrush(SystemColors.InactiveSelectionHighlightTextBrushKey, isDark ? "#FFFFFF" : "#1F2328");

        Background = (Brush)Application.Current.Resources["WindowBrush"];
        Foreground = (Brush)Application.Current.Resources["TextBrush"];

        if (ThemeToggleButton != null)
        {
            ThemeToggleButton.Content = isDark ? "☀️ Light mode" : "🌙 Dark mode";
        }
    }

    private static void SetThemeBrush(string key, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        Application.Current.Resources[key] = brush;
    }

    private static void SetResourceBrush(object resourceKey, string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        Application.Current.Resources[resourceKey] = brush;
    }

    private void UpdateTitleBarTheme(bool isDark)
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                int useDarkMode = isDark ? 1 : 0;
                if (DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDarkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDarkMode, sizeof(int));
                }
            }
        }
        catch
        {
        }
    }

    private MenuEntry? SelectedEntry => EntriesGrid.SelectedItem as MenuEntry;

    private void RefreshEntries()
    {
        try
        {
            _allEntries = _scanner.Scan();
            ApplyFilters();
            StatusText.Text = $"Found {_allEntries.Count} entries. Select one to manage it.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Scan failed.";
            MessageBox.Show(this, ex.Message, "xContextGuard", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilters()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        var selectedLocation = (LocationFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All locations";
        var filtered = _allEntries.Where(e =>
            string.IsNullOrWhiteSpace(query) ||
            e.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            e.Command.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            e.RegistryPath.Contains(query, StringComparison.OrdinalIgnoreCase));

        filtered = selectedLocation switch
        {
            "Installed applications" => filtered
                .Where(e => e.Kind == EntryKind.InstalledApp)
                .OrderByDescending(e => e.IsMissingFiles)
                .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            "Installed apps (Missing files)" => filtered
                .Where(e => e.Kind == EntryKind.InstalledApp && e.IsMissingFiles)
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase),
            "Startup items (All)" => filtered.Where(e => e.IsStartup),
            "Startup (Registry)" => filtered.Where(e => e.Kind == EntryKind.StartupRegistry),
            "Startup (Folders)" => filtered.Where(e => e.Kind == EntryKind.StartupFolder),
            "Win+X menu" => filtered.Where(e => e.Kind == EntryKind.WinXShortcut),
            "File context menu" => filtered.Where(e => e.Location.StartsWith("*", StringComparison.Ordinal)),
            "Folder context menu" => filtered.Where(e => e.Location is "Directory\\shell" or "Folder\\shell"),
            "Folder background" => filtered.Where(e => e.Location.Contains("Background", StringComparison.OrdinalIgnoreCase)),
            "Drive context menu" => filtered.Where(e => e.Location.StartsWith("Drive", StringComparison.OrdinalIgnoreCase)),
            "Desktop background" => filtered.Where(e => e.Location.StartsWith("DesktopBackground", StringComparison.OrdinalIgnoreCase)),
            _ => filtered
        };

        _entries.Clear();
        foreach (var entry in filtered) _entries.Add(entry);
        StatusText.Text = $"Showing {_entries.Count} of {_allEntries.Count} entries.";
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void LocationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded) ApplyFilters();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshEntries();

    private void RestartExplorer_Click(object sender, RoutedEventArgs e)
    {
        var answer = MessageBox.Show(this,
            "Restart Windows Explorer now to reload the taskbar and Win+X menu?",
            "Restart Explorer", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        RunOperation(() =>
        {
            _manager.RestartExplorer();
            StatusText.Text = "Windows Explorer restarted.";
        });
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show(this, "Select an entry first to edit it.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (entry.Kind == EntryKind.ContextMenuHandler)
        {
            MessageBox.Show(this, "Shell extension handlers (COM CLSIDs) cannot be edited directly.\nUse Enable or Disable to toggle them, or Delete to remove them.", "Cannot edit shell extension", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new EditEntryDialog(entry) { Owner = this };
        if (dialog.ShowDialog() != true) return;

        RunOperation(() =>
        {
            if (entry.Kind == EntryKind.WinXShortcut)
            {
                _manager.SaveWinXShortcut(
                    originalPath: entry.RegistryPath,
                    displayName: dialog.EntryName,
                    groupName: dialog.SelectedGroup,
                    targetPath: dialog.TargetPath,
                    arguments: dialog.Arguments,
                    runAsAdmin: dialog.RunAsAdmin);

                RefreshEntries();
                PromptRestartExplorer($"Updated Win+X entry “{dialog.EntryName}”.");
            }
            else if (entry.IsStartup)
            {
                var backup = _manager.SaveStartupEntry(
                    entry,
                    dialog.EntryName,
                    dialog.TargetPath,
                    dialog.Arguments,
                    dialog.RunAsAdmin);
                RefreshEntries();
                StatusText.Text = $"Updated startup entry “{dialog.EntryName}”. Backup: {Path.GetFileName(backup)}";
            }
            else if (entry.Kind == EntryKind.InstalledApp)
            {
                var backup = _manager.SaveInstalledApp(entry, dialog.EntryName, dialog.TargetPath);
                RefreshEntries();
                StatusText.Text = $"Updated installed app entry “{dialog.EntryName}”. Backup: {Path.GetFileName(backup)}";
            }
            else if (entry.Kind == EntryKind.StaticVerb)
            {
                var backup = _manager.SaveStaticVerb(entry, dialog.EntryName, dialog.TargetPath);
                RefreshEntries();
                StatusText.Text = $"Updated command. Backup: {Path.GetFileName(backup)}";
            }
        });
    }

    private void EntriesGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedEntry != null)
        {
            Edit_Click(sender, e);
        }
    }

    private void Enable_Click(object sender, RoutedEventArgs e) => ChangeSelected(enabled: true);
    private void Disable_Click(object sender, RoutedEventArgs e) => ChangeSelected(enabled: false);

    private void ChangeSelected(bool enabled)
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show(this, "Select an entry first.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var action = enabled ? "enable" : "disable";
        bool alreadyInTargetState = entry.Kind == EntryKind.InstalledApp
            ? entry.IsHiddenApp == !enabled
            : entry.IsDisabled == !enabled;

        if (alreadyInTargetState)
        {
            MessageBox.Show(this, $"“{entry.Name}” is already {(enabled ? "enabled" : "disabled")}.", "No change needed", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var answer = MessageBox.Show(this,
            $"Back up and {action} this entry?\n\n{entry.Name}\n{entry.RegistryPath}",
            $"Confirm {action}", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        RunOperation(() =>
        {
            var backup = _manager.SetEnabled(entry, enabled);
            RefreshEntries();
            if (entry.Kind == EntryKind.WinXShortcut)
            {
                PromptRestartExplorer($"Entry {(enabled ? "enabled" : "disabled")}. Backup: {Path.GetFileName(backup)}");
            }
            else
            {
                StatusText.Text = $"Entry {(enabled ? "enabled" : "disabled")}. Backup: {Path.GetFileName(backup)}";
            }
        });
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show(this, "Select an entry first.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var targetDescription = entry.Kind switch
        {
            EntryKind.WinXShortcut => "this Win+X shortcut file",
            EntryKind.StartupRegistry => "this startup registry entry",
            EntryKind.StartupFolder => "this startup folder item",
            EntryKind.InstalledApp => "this installed application (Uninstall) registry entry",
            _ => "this registry registration"
        };

        var answer = MessageBox.Show(this,
            $"Permanently delete {targetDescription}? A backup will be created first.\n\n{entry.Name}\n{entry.RegistryPath}\n\nThis can affect Explorer or the associated application.",
            "Confirm permanent deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        RunOperation(() =>
        {
            var backup = _manager.Delete(entry);
            RefreshEntries();
            if (entry.Kind == EntryKind.WinXShortcut)
            {
                PromptRestartExplorer($"Deleted Win+X entry. Backup: {Path.GetFileName(backup)}");
            }
            else
            {
                StatusText.Text = $"Deleted entry. Backup: {Path.GetFileName(backup)}";
            }
        });
    }

    private void PromptRestartExplorer(string message)
    {
        StatusText.Text = message;
        var answer = MessageBox.Show(this,
            $"{message}\n\nWindows Explorer caches Win+X menu items. Would you like to restart Explorer now to apply the changes immediately?",
            "Restart Windows Explorer?", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer == MessageBoxResult.Yes)
        {
            _manager.RestartExplorer();
            StatusText.Text = message + " Windows Explorer restarted.";
        }
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        var entry = SelectedEntry;
        if (entry is null)
        {
            MessageBox.Show(this, "Select an entry first.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        RunOperation(() =>
        {
            var backup = _manager.CreateBackup(entry);
            StatusText.Text = $"Backup created: {backup}";
            MessageBox.Show(this, $"Backup created successfully:\n{backup}", "Backup complete", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an xContextGuard backup",
            Filter = "xContextGuard backups (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_manager.BackupDirectory) ? _manager.BackupDirectory : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
        };
        if (dialog.ShowDialog(this) != true) return;

        var answer = MessageBox.Show(this,
            $"Restore the selected backup?\n\n{dialog.FileName}",
            "Confirm restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;

        RunOperation(() =>
        {
            _manager.Restore(dialog.FileName);
            RefreshEntries();
            PromptRestartExplorer($"Restored backup: {Path.GetFileName(dialog.FileName)}");
        });
    }

    private void RunOperation(Action action)
    {
        try
        {
            action();
        }
        catch (UnauthorizedAccessException ex)
        {
            MessageBox.Show(this, $"Windows denied access to this location. Try running xContextGuard as administrator.\n\n{ex.Message}", "Access denied", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (System.Security.SecurityException ex)
        {
            MessageBox.Show(this, ex.Message, "Security error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
