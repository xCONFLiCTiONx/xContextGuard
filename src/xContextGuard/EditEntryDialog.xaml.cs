using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using xContextGuard.Models;
using xContextGuard.Services;

namespace xContextGuard;

public partial class EditEntryDialog : Window
{
    public MenuEntry? OriginalEntry { get; }
    public string EntryName => NameBox.Text.Trim();
    public string TargetPath => TargetBox.Text.Trim();
    public string Arguments => ArgumentsBox.Text.Trim();
    public string SelectedGroup
    {
        get
        {
            if (GroupComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
                return tag;
            return "Group3";
        }
    }
    public bool RunAsAdmin => RunAsAdminCheckBox.IsChecked == true;
    public bool IsWinXMode { get; }

    public EditEntryDialog(MenuEntry? entry = null)
    {
        InitializeComponent();
        OriginalEntry = entry;

        if (entry is not null && entry.Kind == EntryKind.WinXShortcut)
        {
            // Editing existing built-in Win+X entry (rename / move between groups)
            IsWinXMode = true;
            Title = "Edit Win+X Entry";
            DialogTitleText.Text = "Edit Win+X Entry";
            DialogSubtitleText.Text = $"Rename or move built-in Win+X shortcut: {entry.Name}";
            NameBox.Text = entry.Name;
            TargetBox.Text = entry.Command;
            ArgumentsBox.Text = entry.Arguments;
            RunAsAdminCheckBox.IsChecked = entry.RunAsAdmin;

            TargetLabelText.Text = "Built-in Target (read-only on Windows 11 24H2)";
            TargetBox.IsReadOnly = true;
            TargetBox.Opacity = 0.75;
            BrowseTargetButton.Visibility = Visibility.Collapsed;

            ArgumentsLabelText.Text = "Command Arguments (read-only for built-in Win+X)";
            ArgumentsBox.IsReadOnly = true;
            ArgumentsBox.Opacity = 0.75;
            RunAsAdminCheckBox.IsEnabled = false;

            var grp = !string.IsNullOrEmpty(entry.Group) ? entry.Group : ParseGroupFromPath(entry.RegistryPath);
            SelectGroup(grp);
        }
        else if (entry is not null && entry.Kind == EntryKind.StartupFolder)
        {
            // Editing a Startup Folder shortcut
            IsWinXMode = false;
            Title = "Edit Startup Folder Item";
            DialogTitleText.Text = "Edit Startup Folder Item";
            DialogSubtitleText.Text = $"Editing startup shortcut: {entry.Name}";
            NameBox.Text = entry.Name;

            string rawTarget = entry.Command;
            string rawArgs = entry.Arguments;
            bool admin = entry.RunAsAdmin;
            try
            {
                var (t, a, adm) = StartupService.ReadShortcut(entry.RegistryPath);
                if (!string.IsNullOrWhiteSpace(t))
                {
                    rawTarget = t;
                    rawArgs = a;
                    admin = adm;
                }
            }
            catch
            {
            }

            TargetBox.Text = rawTarget;
            ArgumentsBox.Text = rawArgs;
            RunAsAdminCheckBox.IsChecked = admin;

            GroupPanel.Visibility = Visibility.Collapsed;
            WinXNoticeBorder.Visibility = Visibility.Collapsed;
        }
        else if (entry is not null && entry.Kind == EntryKind.StartupRegistry)
        {
            // Editing a Startup Registry entry
            IsWinXMode = false;
            Title = "Edit Startup Registry Entry";
            DialogTitleText.Text = "Edit Startup Registry Entry";
            DialogSubtitleText.Text = $"Editing {entry.Location}: {entry.Name}";
            NameBox.Text = entry.Name;
            TargetLabelText.Text = "Startup Command Line";
            TargetBox.Text = entry.Command;

            GroupPanel.Visibility = Visibility.Collapsed;
            ArgumentsPanel.Visibility = Visibility.Collapsed;
            AdminPanel.Visibility = Visibility.Collapsed;
            WinXNoticeBorder.Visibility = Visibility.Collapsed;
        }
        else if (entry is not null && entry.Kind == EntryKind.InstalledApp)
        {
            // Editing an Installed Application registry entry
            IsWinXMode = false;
            Title = "Edit Installed Application Entry";
            DialogTitleText.Text = "Edit Installed Application Entry";
            DialogSubtitleText.Text = entry.RegistryPath;
            NameBox.Text = !string.IsNullOrWhiteSpace(entry.ValueName) ? entry.ValueName : entry.Name;
            TargetLabelText.Text = "Uninstall Command (UninstallString)";
            TargetBox.Text = !string.IsNullOrWhiteSpace(entry.Arguments) ? entry.Arguments : entry.Command;

            GroupPanel.Visibility = Visibility.Collapsed;
            ArgumentsPanel.Visibility = Visibility.Collapsed;
            AdminPanel.Visibility = Visibility.Collapsed;
            WinXNoticeBorder.Visibility = Visibility.Collapsed;
        }
        else
        {
            // Editing a static context menu verb
            IsWinXMode = false;
            Title = "Edit Context Menu Command";
            DialogTitleText.Text = "Edit Context Menu Command";
            DialogSubtitleText.Text = entry is not null ? $"Editing: {entry.Name}" : "Edit command";
            NameBox.Text = entry?.Name ?? "";
            TargetBox.Text = entry?.Command ?? "";

            GroupPanel.Visibility = Visibility.Collapsed;
            ArgumentsPanel.Visibility = Visibility.Collapsed;
            AdminPanel.Visibility = Visibility.Collapsed;
            WinXNoticeBorder.Visibility = Visibility.Collapsed;
        }
    }

    private void SelectGroup(string groupName)
    {
        foreach (ComboBoxItem item in GroupComboBox.Items)
        {
            if (string.Equals(item.Tag as string, groupName, StringComparison.OrdinalIgnoreCase))
            {
                GroupComboBox.SelectedItem = item;
                return;
            }
        }
        if (GroupComboBox.Items.Count > 0)
        {
            GroupComboBox.SelectedIndex = 2; // Default to Group 3
        }
    }

    private static string ParseGroupFromPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return "Group3";
        var dir = Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(dir)) return "Group3";
        var name = Path.GetFileName(dir);
        if (name.StartsWith("Group", StringComparison.OrdinalIgnoreCase))
            return name;
        return "Group3";
    }

    private void BrowseTarget_Click(object sender, RoutedEventArgs e)
    {
        if (IsWinXMode) return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Target Executable or File",
            Filter = "Executables and Scripts (*.exe;*.msc;*.cpl;*.bat;*.cmd;*.ps1)|*.exe;*.msc;*.cpl;*.bat;*.cmd;*.ps1|All files (*.*)|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System)
        };

        if (dialog.ShowDialog(this) == true)
        {
            TargetBox.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                NameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show(this, "Please enter a display name for this entry.", "Name required", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameBox.Focus();
            return;
        }

        if (!IsWinXMode && string.IsNullOrWhiteSpace(TargetBox.Text))
        {
            MessageBox.Show(this, "Please enter or browse to a target file path.", "Target required", MessageBoxButton.OK, MessageBoxImage.Warning);
            TargetBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
