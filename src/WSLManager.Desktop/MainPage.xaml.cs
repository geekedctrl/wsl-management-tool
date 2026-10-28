using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using WSLManager.Desktop.Core.Backend;
using WSLManager.Desktop.Core.Settings;
using WSLManager.Desktop.Core.Models;

namespace WSLManager_Desktop;

public sealed partial class MainPage : Page
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    private readonly ObservableCollection<DistributionSummary> _distributions = [];
    private readonly OperationState _operationState = new();
    private bool _initializing = true;

    public MainPage()
    {
        InitializeComponent();
        DistributionList.ItemsSource = _distributions;
        BackupDistroList.ItemsSource = _distributions;
        MigrationDistro.ItemsSource = _distributions;
        MigrationDistro.DisplayMemberPath = nameof(DistributionSummary.Name);
        StorageDistro.ItemsSource = _distributions;
        StorageDistro.DisplayMemberPath = nameof(DistributionSummary.Name);

        RetentionDays.Text = App.Current.Settings.LogRetentionDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ThemeChoice.SelectedIndex = (int)App.Current.Settings.Theme;
        ApplyTheme(App.Current.Settings.Theme);
        _initializing = false;
        Loaded += async (_, _) =>
        {
            ApplyResponsiveLayout();
            await RefreshDistributionsAsync();
        };
    }

    private void Workspace_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyResponsiveLayout();

    private void ApplyResponsiveLayout()
    {
        var availableWidth = Workspace.ActualWidth;
        if (availableWidth <= 0) return;
        SetFrameWidth(DistributionsFrame, availableWidth, 2800);
        SetFrameWidth(BackupFrame, availableWidth, 2800);
        SetFrameWidth(MigrationFrame, availableWidth, 2800);
        SetFrameWidth(StorageFrame, availableWidth, 2800);
        SetFrameWidth(DiagnosticsFrame, availableWidth, 2800);
        SetFrameWidth(SettingsFrame, availableWidth, 1400);
        var wide = availableWidth >= 1450;
        ConfigureColumns(DistributionColumns, DistributionDetailsPane, wide);
        ConfigureColumns(BackupColumns, RestoreCard, wide);
        ConfigureColumns(MigrationColumns, CloneCard, wide);
        ConfigureColumns(StorageColumns, ResizeCard, wide);
        ConfigureColumns(DiagnosticsColumns, HistoryCard, wide);
    }

    private static void SetFrameWidth(Border frame, double availableWidth, double maxWidth) =>
        frame.Width = Math.Min(availableWidth, maxWidth);

    private static void ConfigureColumns(Grid grid, FrameworkElement secondary, bool wide)
    {
        if (grid.ColumnDefinitions.Count < 2) return;
        grid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        grid.ColumnDefinitions[1].Width = wide ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        grid.ColumnSpacing = wide ? 24 : 0;
        Grid.SetColumn(secondary, wide ? 1 : 0);
        Grid.SetRow(secondary, wide ? 0 : 1);
    }

    public void ApplyTheme(ThemePreference preference)
    {
        RequestedTheme = preference switch
        {
            ThemePreference.Light => ElementTheme.Light,
            ThemePreference.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
    }

    private void Navigation_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item || item.Tag is not string key)
            return;

        DistributionsPanel.Visibility = key == "Distributions" ? Visibility.Visible : Visibility.Collapsed;
        BackupPanel.Visibility = key == "Backup" ? Visibility.Visible : Visibility.Collapsed;
        MigrationPanel.Visibility = key == "Migration" ? Visibility.Visible : Visibility.Collapsed;
        StoragePanel.Visibility = key == "Storage" ? Visibility.Visible : Visibility.Collapsed;
        DiagnosticsPanel.Visibility = key == "Diagnostics" ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = key == "Settings" ? Visibility.Visible : Visibility.Collapsed;

        if (key == "Storage") _ = RefreshStorageChoicesAsync();
        if (key == "Diagnostics") _ = RefreshDiagnosticsAsync();
        if (key == "Settings") SettingsStatus.Text = $"Settings file: {App.Current.SettingsStore.PathName}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshDistributionsAsync();

    private async Task RefreshDistributionsAsync()
    {
        if (_operationState.IsBusy) return;
        SetBusy(BusyRing, true, "Reading WSL distributions…");
        try
        {
            using var document = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "List" });
            var items = DistributionSummaryParser.Parse(document.RootElement);
            _distributions.Clear();
            foreach (var item in items.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)) _distributions.Add(item);
            UpdateBackupSelectionSummary();
            EmptyDistributions.Visibility = _distributions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyDistributions.Text = "No distributions detected. Install or register a WSL distribution, then refresh.";
            if (DistributionList.SelectedItem is not DistributionSummary selected || !_distributions.Any(item => item.Name == selected.Name))
            {
                SelectedDistroCard.Visibility = Visibility.Collapsed;
                DetailsHint.Visibility = Visibility.Visible;
            }
            await RefreshStorageChoicesAsync();
            await RefreshConversionCapabilityAsync();
            StatusText.Text = _distributions.Count == 0 ? "No distributions registered." : $"{_distributions.Count} distribution(s) found.";
        }
        catch (Exception ex)
        {
            _distributions.Clear();
            EmptyDistributions.Visibility = Visibility.Visible;
            EmptyDistributions.Text = "Could not read distributions. Verify WSL is installed and check diagnostics.";
            ConvertPanel.Visibility = Visibility.Collapsed;
            ConvertCapabilityStatus.Text = "Conversion capability is unavailable.";
            StatusText.Text = "Could not read distributions.";
            OperationOutput.Text = SanitizeError(ex);
        }
        finally { SetBusy(BusyRing, false, StatusText.Text); }
    }

    private Task RefreshStorageChoicesAsync()
    {
        if (_distributions.Count == 0) return Task.CompletedTask;
        var selectedMigration = MigrationDistro.SelectedItem as DistributionSummary;
        var selectedStorage = StorageDistro.SelectedItem as DistributionSummary;
        MigrationDistro.SelectedItem = selectedMigration is null ? _distributions.FirstOrDefault() : FindDistro(selectedMigration.Name);
        StorageDistro.SelectedItem = selectedStorage is null ? _distributions.FirstOrDefault() : FindDistro(selectedStorage.Name);
        return Task.CompletedTask;
    }

    private async Task RefreshConversionCapabilityAsync()
    {
        try
        {
            using var document = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Diagnose" });
            var supported = document.RootElement.TryGetProperty("Capabilities", out var capabilities) &&
                            capabilities.TryGetProperty("SupportsSetVersion", out var value) && value.ValueKind == JsonValueKind.True;
            ConvertPanel.Visibility = supported ? Visibility.Visible : Visibility.Collapsed;
            ConvertCapabilityStatus.Text = supported ? string.Empty : "Conversion is not advertised by the installed WSL release.";
        }
        catch
        {
            ConvertPanel.Visibility = Visibility.Collapsed;
            ConvertCapabilityStatus.Text = "Conversion capability could not be determined.";
        }
    }

    private DistributionSummary? FindDistro(string name) => _distributions.FirstOrDefault(item => item.Name.Equals(name, StringComparison.Ordinal));

    private async void DistributionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DistributionList.SelectedItem is not DistributionSummary item)
        {
            SelectedDistroCard.Visibility = Visibility.Collapsed;
            DetailsHint.Visibility = Visibility.Visible;
            return;
        }
        SelectedDistroCard.Visibility = Visibility.Visible;
        DetailsHint.Visibility = Visibility.Collapsed;
        SelectedDistroName.Text = item.Name;
        SelectedDistroDetails.Text = item.Summary + (item.IsDefault ? " · Default" : string.Empty) + (item.IsProtected ? " · System-managed" : string.Empty);
        OperationOutput.Text = "Loading distribution details…";
        try
        {
            using var document = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Info", Distro = item.Name });
            OperationOutput.Text = FormatJson(document.RootElement);
        }
        catch (Exception ex) { OperationOutput.Text = SanitizeError(ex); }
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await RunDistributionActionAsync("Start");
    private async void Terminate_Click(object sender, RoutedEventArgs e) => await RunDistributionActionAsync("Terminate");
    private async void SetDefault_Click(object sender, RoutedEventArgs e) => await RunDistributionActionAsync("SetDefault");

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        var selected = DistributionList.SelectedItem as DistributionSummary;
        if (selected is null || ConvertVersion.SelectedItem is not ComboBoxItem target || target.Tag is not string version)
        {
            StatusText.Text = "Select a distribution and target WSL version.";
            return;
        }
        if (RefuseProtectedAction("Convert", selected.Name, StatusText)) return;
        await RunRequestAsync(new BackendRequest { Action = "Convert", Distro = selected.Name, Version = version }, selected.Name, "Convert", selected.Name, $"WSL {version}", BusyRing, StatusText, OperationOutput);
    }

    private async Task RunDistributionActionAsync(string action)
    {
        var selected = DistributionList.SelectedItem as DistributionSummary;
        if (selected is null) { StatusText.Text = "Select a distribution first."; return; }
        if (RefuseProtectedAction(action, selected.Name, StatusText)) return;
        await RunRequestAsync(new BackendRequest { Action = action, Distro = selected.Name }, action, action, selected.Name, null, BusyRing, StatusText, OperationOutput);
    }

    private async void BackupBrowse_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) BackupDestination.Text = path;
    }

    private void BackupDistroList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateBackupSelectionSummary();

    private void BackupSelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var distro in _distributions)
            if (!BackupDistroList.SelectedItems.Contains(distro)) BackupDistroList.SelectedItems.Add(distro);
        UpdateBackupSelectionSummary();
    }

    private void BackupClearSelection_Click(object sender, RoutedEventArgs e) => BackupDistroList.SelectedItems.Clear();

    private void UpdateBackupSelectionSummary()
    {
        if (BackupSelectionSummary is null) return;
        var count = BackupDistroList.SelectedItems.Count;
        BackupSelectionSummary.Text = count == 0 ? "No distributions selected" : $"{count} distribution{(count == 1 ? string.Empty : "s")} selected";
    }

    private async void RestoreBrowse_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".tar");
        picker.FileTypeFilter.Add(".vhdx");
        InitializePicker(picker);
        var file = await picker.PickSingleFileAsync();
        if (file is not null) RestoreSource.Text = file.Path;
    }

    private async void RestoreFolderBrowse_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) RestoreDestination.Text = path;
    }

    private async void Backup_Click(object sender, RoutedEventArgs e)
    {
        var selected = BackupDistroList.SelectedItems.Cast<DistributionSummary>().ToArray();
        var destination = BackupDestination.Text.Trim();
        var format = (BackupFormat.SelectedItem as ComboBoxItem)?.Tag as string ?? "Tar";
        if (selected.Length == 0) { BackupStatus.Text = "Select at least one distribution."; return; }
        if (!Directory.Exists(destination)) { BackupStatus.Text = "Choose an existing destination folder."; return; }
        if (format == "Vhdx" && selected.Any(item => item.Version != 2)) { BackupStatus.Text = "VHDX export is available only for WSL 2 distributions. Select only WSL 2 distributions or choose TAR."; return; }
        var names = selected.Select(item => item.Name).ToArray();
        await RunRequestAsync(new BackendRequest { Action = "Backup", Distributions = names, Destination = destination, Format = format },
            "Backup", "Backup", string.Join(", ", names), destination, BackupBusy, BackupStatus, BackupOutput);
        await RefreshHistoryAsync();
    }

    private async void Restore_Click(object sender, RoutedEventArgs e)
    {
        var source = RestoreSource.Text.Trim();
        var name = RestoreName.Text.Trim();
        var destination = RestoreDestination.Text.Trim();
        if (!File.Exists(source) || Path.GetExtension(source).ToLowerInvariant() is not (".tar" or ".vhdx")) { BackupStatus.Text = "Select an existing .tar or .vhdx backup file."; return; }
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(destination)) { BackupStatus.Text = "Enter a new distribution name and installation folder."; return; }
        if (Directory.Exists(destination) || File.Exists(destination)) { BackupStatus.Text = "The installation path already exists. Restore refuses to overwrite it."; return; }
        await RunRequestAsync(new BackendRequest { Action = "Restore", Source = source, Distro = name, Destination = destination },
            "Restore", "Restore", name, destination, BackupBusy, BackupStatus, BackupOutput);
        await RefreshDistributionsAsync();
    }

    private async void MoveBrowse_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) MoveDestination.Text = path;
    }

    private async void CloneBrowse_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null) CloneDestination.Text = path;
    }

    private void MigrationDistro_SelectionChanged(object sender, SelectionChangedEventArgs e) => _movePreview = null;
    private void MoveDestination_TextChanged(object sender, TextChangedEventArgs e) => _movePreview = null;

    private async void MovePreview_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationDistro.SelectedItem is not DistributionSummary distro) { MigrationStatus.Text = "Select a source distribution."; return; }
        await PreviewMoveAsync(distro.Name, MoveDestination.Text.Trim());
    }

    private (string Distro, string Destination)? _movePreview;

    private async Task<bool> PreviewMoveAsync(string distroName, string destination)
    {
        _movePreview = null;
        if (RefuseProtectedAction("Move", distroName, MigrationStatus)) return false;
        if (string.IsNullOrWhiteSpace(destination)) { MigrationStatus.Text = "Choose a new installation folder before previewing the move."; return false; }
        if (_operationState.IsBusy) { MigrationStatus.Text = "Wait for the current operation to finish."; return false; }
        SetBusy(MigrationBusy, true, "Checking native move support and destination…");
        try
        {
            var full = Path.GetFullPath(destination);
            var root = Path.GetPathRoot(full);
            if (string.IsNullOrWhiteSpace(root)) throw new IOException("Choose a destination on a local Windows volume.");
            var drive = new DriveInfo(root);
            var exists = Directory.Exists(full);
            var nonEmpty = exists && Directory.EnumerateFileSystemEntries(full).Any();
            using var info = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Info", Distro = distroName });
            using var diagnostics = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Diagnose" });
            var capability = diagnostics.RootElement.TryGetProperty("Capabilities", out var caps) &&
                             caps.TryGetProperty("SupportsManageMove", out var supported) && supported.ValueKind == JsonValueKind.True;
            var version = FindDistro(distroName)?.Version;
            var canMove = version == 2 && capability && !nonEmpty && drive.DriveFormat == "NTFS";
            MigrationOutput.Text = $"Move preview\nSource: {distroName} (WSL {version?.ToString() ?? "unknown"})\nDestination: {full}\nDestination exists: {exists}\nDestination empty: {!nonEmpty}\nVolume: {drive.DriveFormat}\nFree space: {drive.AvailableFreeSpace:N0} bytes\nNative move advertised: {capability}\n\nBackend storage details:\n{FormatJson(info.RootElement)}";
            MigrationStatus.Text = version != 2 ? "Move is available only for WSL 2." : !capability ? "This WSL version does not advertise native move support; no fallback will be attempted." : nonEmpty ? "Destination is not empty; move cannot proceed." : drive.DriveFormat != "NTFS" ? "Destination must be on a local NTFS volume." : "Preview complete. Review the source and destination, then confirm the move.";
            if (canMove) _movePreview = (distroName, full);
            return canMove;
        }
        catch (Exception ex) { MigrationStatus.Text = "Move preview could not be completed."; MigrationOutput.Text = SanitizeError(ex); return false; }
        finally { SetBusy(MigrationBusy, false, MigrationStatus.Text); }
    }

    private async void Move_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationDistro.SelectedItem is not DistributionSummary distro) { MigrationStatus.Text = "Select a source distribution."; return; }
        var distroName = distro.Name;
        var destination = MoveDestination.Text.Trim();
        if (!await PreviewMoveAsync(distroName, destination)) return;
        if (MigrationDistro.SelectedItem is not DistributionSummary current || current.Name != distroName || MoveDestination.Text.Trim() != destination)
        {
            _movePreview = null;
            MigrationStatus.Text = "The source or destination changed during preview. Review the move again before continuing.";
            return;
        }
        var preview = _movePreview;
        if (preview is null || preview.Value.Distro != distroName) { MigrationStatus.Text = "Preview the current source and destination before moving."; return; }
        await RunRequestAsync(new BackendRequest { Action = "Move", Distro = preview.Value.Distro, Destination = preview.Value.Destination },
            "Move", "Move", preview.Value.Distro, preview.Value.Destination, MigrationBusy, MigrationStatus, MigrationOutput);
        _movePreview = null;
        await RefreshDistributionsAsync();
    }

    private async void Clone_Click(object sender, RoutedEventArgs e)
    {
        if (MigrationDistro.SelectedItem is not DistributionSummary distro) { MigrationStatus.Text = "Select a source distribution."; return; }
        if (RefuseProtectedAction("Clone", distro.Name, MigrationStatus)) return;
        var name = CloneName.Text.Trim();
        var destination = CloneDestination.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(destination)) { MigrationStatus.Text = "Enter a new distribution name and destination folder."; return; }
        if (_distributions.Any(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) { MigrationStatus.Text = $"A distribution named '{name}' is already registered. Choose a different name."; return; }
        if (Directory.Exists(destination) || File.Exists(destination)) { MigrationStatus.Text = "The clone destination already exists."; return; }
        await RunRequestAsync(new BackendRequest { Action = "Clone", Distro = distro.Name, NewName = name, Destination = destination },
            "Clone", "Clone", $"{distro.Name} → {name}", destination, MigrationBusy, MigrationStatus, MigrationOutput);
        await RefreshDistributionsAsync();
    }

    private async void StorageInspect_Click(object sender, RoutedEventArgs e)
    {
        if (StorageDistro.SelectedItem is not DistributionSummary distro) { StorageStatus.Text = "Select a distribution."; return; }
        await RunRequestAsync(new BackendRequest { Action = "Disk", Distro = distro.Name }, "Storage inspection", null, distro.Name, null, StorageBusy, StorageStatus, StorageOutput);
    }

    private async void Resize_Click(object sender, RoutedEventArgs e)
    {
        if (StorageDistro.SelectedItem is not DistributionSummary distro || string.IsNullOrWhiteSpace(ResizeTarget.Text)) { StorageStatus.Text = "Select a distribution and enter a larger capacity."; return; }
        if (RefuseProtectedAction("Resize", distro.Name, StorageStatus)) return;
        if (_resizePreview != (distro.Name, ResizeTarget.Text.Trim())) { StorageStatus.Text = "Run and review the expansion preview for this distribution and target first."; return; }
        await RunRequestAsync(new BackendRequest { Action = "Resize", Distro = distro.Name, Size = ResizeTarget.Text.Trim() },
            "Expand virtual disk", "Resize", distro.Name, ResizeTarget.Text.Trim(), StorageBusy, StorageStatus, StorageOutput);
        _resizePreview = null;
    }

    private (string Distro, string Size)? _resizePreview;

    private async void ResizePreview_Click(object sender, RoutedEventArgs e)
    {
        if (StorageDistro.SelectedItem is not DistributionSummary distro || string.IsNullOrWhiteSpace(ResizeTarget.Text)) { StorageStatus.Text = "Select a distribution and enter a larger capacity."; return; }
        if (RefuseProtectedAction("Resize", distro.Name, StorageStatus)) return;
        if (_operationState.IsBusy) { StorageStatus.Text = "Wait for the current operation to finish."; return; }
        SetBusy(StorageBusy, true, "Checking disk expansion support and preflight…");
        try
        {
            using var preview = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Resize", Distro = distro.Name, Size = ResizeTarget.Text.Trim(), WhatIf = true });
            StorageOutput.Text = FormatJson(preview.RootElement);
            _resizePreview = (distro.Name, ResizeTarget.Text.Trim());
            StorageStatus.Text = "Preview complete. Review the current capacity, target, and preflight details before expanding.";
        }
        catch (Exception ex)
        {
            _resizePreview = null;
            StorageStatus.Text = "Expansion preview failed; no resize was started.";
            StorageOutput.Text = SanitizeError(ex);
        }
        finally { SetBusy(StorageBusy, false, StorageStatus.Text); }
    }

    private async void DiagnosticsRefresh_Click(object sender, RoutedEventArgs e) => await RefreshDiagnosticsAsync();

    private async Task RefreshDiagnosticsAsync()
    {
        if (_operationState.IsBusy) { DiagnosticsStatus.Text = "Wait for the current operation to finish before refreshing diagnostics."; return; }
        SetBusy(DiagnosticsBusy, true, "Reading WSL diagnostics…");
        try
        {
            using var document = await App.Current.Backend.InvokeAsync(new BackendRequest { Action = "Diagnose" });
            DiagnosticsOutput.Text = DiagnosticsRedactor.Redact(document.RootElement.GetRawText(),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            DiagnosticsStatus.Text = DiagnosticsAvailabilityMessage.From(document.RootElement);
        }
        catch (Exception ex) { DiagnosticsStatus.Text = "Diagnostics failed."; DiagnosticsOutput.Text = SanitizeError(ex); }
        finally { SetBusy(DiagnosticsBusy, false, DiagnosticsStatus.Text); }
    }

    private async void DiagnosticsExport_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DiagnosticsOutput.Text)) await RefreshDiagnosticsAsync();
        if (string.IsNullOrWhiteSpace(DiagnosticsOutput.Text)) return;
        var picker = new FileSavePicker();
        picker.SuggestedFileName = "wsl-manager-diagnostics";
        picker.FileTypeChoices.Add("JSON", new List<string> { ".json" });
        InitializePicker(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return;
        try
        {
            await DiagnosticsExportService.WriteJsonAsync(
                file.Path,
                DiagnosticsOutput.Text,
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            DiagnosticsStatus.Text = $"Diagnostics saved to {file.Path}";
        }
        catch (Exception ex) { DiagnosticsStatus.Text = "Could not save diagnostics: " + SanitizeError(ex); }
    }

    private async void HistoryRefresh_Click(object sender, RoutedEventArgs e) => await RefreshHistoryAsync();
    private async void BackupHistoryRefresh_Click(object sender, RoutedEventArgs e) => await RefreshBackupHistoryAsync();

    private async Task RefreshHistoryAsync()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WSLManager");
        var operations = await ReadRecentLinesAsync(Path.Combine(root, "operations.jsonl"), 30);
        var backups = await ReadRecentLinesAsync(Path.Combine(root, "backup-history.jsonl"), 20);
        BackupHistoryOutput.Text = backups.Count == 0 ? "No backups logged yet." : string.Join(Environment.NewLine, backups);
        HistoryOutput.Text = (operations.Count == 0 ? "No operations logged yet." : string.Join(Environment.NewLine, operations)) +
                             Environment.NewLine + Environment.NewLine + "Backup history" + Environment.NewLine +
                             (backups.Count == 0 ? "No backups logged yet." : string.Join(Environment.NewLine, backups));
        DiagnosticsStatus.Text = "Operation and backup history refreshed.";
    }

    private async Task RefreshBackupHistoryAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WSLManager", "backup-history.jsonl");
        var backups = await ReadRecentLinesAsync(path, 20);
        BackupHistoryOutput.Text = backups.Count == 0 ? "No backups logged yet." : string.Join(Environment.NewLine, backups);
    }

    private async Task<List<string>> ReadRecentLinesAsync(string path, int count)
    {
        try
        {
            if (!File.Exists(path)) return [];
            var lines = await File.ReadAllLinesAsync(path);
            return lines.TakeLast(count).Select(line =>
            {
                try { using var row = JsonDocument.Parse(line); return FormatJson(row.RootElement); }
                catch (JsonException) { return line; }
            }).ToList();
        }
        catch (Exception ex) { return [$"Could not read {Path.GetFileName(path)}: {SanitizeError(ex)}"]; }
    }

    private void ThemeChoice_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing || ThemeChoice.SelectedItem is not ComboBoxItem item || item.Tag is not string theme || !Enum.TryParse(theme, out ThemePreference preference)) return;
        if (App.Current.Settings.Theme == preference) return;
        ApplyTheme(preference);
        App.Current.Settings = App.Current.Settings with { Theme = preference };
        try { App.Current.SettingsStore.Save(App.Current.Settings); SettingsStatus.Text = $"{preference} theme applied and saved."; }
        catch (Exception ex) { SettingsStatus.Text = $"Theme applied for this session; save failed: {SanitizeError(ex)}"; }
    }

    private void SettingsSave_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(RetentionDays.Text, out var days) || days is < 1 or > 3650)
        {
            SettingsStatus.Text = "Log retention must be a whole number from 1 to 3650.";
            return;
        }
        var preference = ThemeChoice.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Tag as string, out ThemePreference theme) ? theme : ThemePreference.System;
        App.Current.Settings = new DesktopSettings { Theme = preference, LogRetentionDays = days };
        ApplyTheme(preference);
        try { App.Current.SettingsStore.Save(App.Current.Settings); SettingsStatus.Text = "Settings saved for this Windows user."; }
        catch (Exception ex) { SettingsStatus.Text = "Could not save settings: " + SanitizeError(ex); }
    }

    private async Task RunRequestAsync(BackendRequest request, string label, string? confirmationAction, string target, string? destination,
        ProgressRing progress, TextBlock status, UIElement output)
    {
        if (_operationState.IsBusy) { status.Text = "Another WSL operation is already running."; return; }
        if (request.Distro is not null && RefuseProtectedAction(request.Action, request.Distro, status)) return;
        if (confirmationAction is not null && ConfirmationPolicy.RequiresConfirmation(confirmationAction))
        {
            var confirmed = await ConfirmAsync(label, ConfirmationPolicy.Describe(confirmationAction, target, destination));
            if (!confirmed) { status.Text = "Operation cancelled. No backend command was run."; return; }
        }
        if (_operationState.IsBusy) { status.Text = "Another WSL operation started while confirmation was open."; return; }

        SetBusy(progress, true, $"Running {label}…");
        SetOutput(output, string.Empty);
        try
        {
            using var document = await App.Current.Backend.InvokeAsync(request);
            SetOutput(output, FormatJson(document.RootElement));
            status.Text = $"{label} completed. Review the backend result and verification fields.";
        }
        catch (OperationCanceledException) { status.Text = $"{label} was cancelled. Any partial export is retained for review."; }
        catch (Exception ex) { status.Text = $"{label} failed."; SetOutput(output, SanitizeError(ex)); }
        finally
        {
            SetBusy(progress, false, status.Text);
        }
    }

    private bool RefuseProtectedAction(string action, string distroName, TextBlock status)
    {
        var distro = FindDistro(distroName);
        if (distro is null || ProtectedDistributionPolicy.IsAllowed(action, distro.IsProtected)) return false;
        status.Text = $"{action} is unavailable for system-managed distribution '{distroName}'.";
        return true;
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = $"Confirm {title.ToLowerInvariant()}",
            Content = message,
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private static string FormatJson(JsonElement element) => JsonSerializer.Serialize(element, JsonOptions);

    private static string SanitizeError(Exception exception) => DiagnosticsRedactor.RedactText(
        exception.Message,
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    private static void SetOutput(UIElement output, string value)
    {
        if (output is TextBlock textBlock) textBlock.Text = value;
        else if (output is TextBox textBox) textBox.Text = value;
    }

    private async Task<string?> PickFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");
        InitializePicker(picker);
        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    private void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow!);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }

    private void SetBusy(ProgressRing ring, bool active, string text)
    {
        ring.IsActive = active;
        if (active) _operationState.TryBegin(text);
        else _operationState.End(text);
        if (ReferenceEquals(ring, BusyRing)) StatusText.Text = text;
    }
}
