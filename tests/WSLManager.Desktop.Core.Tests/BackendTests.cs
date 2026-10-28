using System.Diagnostics;
using System.Text.Json;
using WSLManager.Desktop.Core.Backend;
using WSLManager.Desktop.Core.Settings;

namespace WSLManager.Desktop.Core.Tests;

public sealed class BackendTests
{
    [Fact]
    public void Operation_state_rejects_concurrent_work_and_returns_to_idle()
    {
        var state = new OperationState();
        Assert.True(state.TryBegin("Loading distributions"));
        Assert.True(state.IsBusy);
        Assert.Equal("Loading distributions", state.Status);
        Assert.False(state.TryBegin("Starting another operation"));

        state.End("Refresh failed");
        Assert.False(state.IsBusy);
        Assert.Equal("Refresh failed", state.Status);
        Assert.True(state.TryBegin("Retrying"));
    }

    [Fact]
    public void Diagnostics_redaction_masks_machine_paths_and_sensitive_config_values()
    {
        const string input = """{"Computer":"BUILD-123","WslConfig":{"Path":"C:\\Users\\alice\\.wslconfig","Content":"[wsl2]\nkernel=C:\\Users\\alice\\custom.vhdx\nmemory=8GB\napiToken=secret-value"}}""";
        var redacted = DiagnosticsRedactor.Redact(input, @"C:\Users\alice", @"C:\Users\alice\AppData\Local");

        Assert.DoesNotContain("BUILD-123", redacted);
        Assert.DoesNotContain("alice", redacted);
        Assert.DoesNotContain("custom.vhdx", redacted);
        Assert.DoesNotContain("secret-value", redacted);
        Assert.Contains("kernel=[redacted]", redacted);
        Assert.Contains("memory=8GB", redacted);
    }

    [Fact]
    public void Diagnostics_availability_message_explains_missing_wsl_and_empty_installations()
    {
        using var missing = JsonDocument.Parse("""{"Capabilities":{"Available":false},"Distributions":[]}""");
        using var empty = JsonDocument.Parse("""{"Capabilities":{"Available":true},"Distributions":[]}""");
        using var installed = JsonDocument.Parse("""{"Capabilities":{"Available":true},"Distributions":[{"Name":"Debian"}]}""");

        Assert.Contains("wsl.exe was not found", DiagnosticsAvailabilityMessage.From(missing.RootElement));
        Assert.Contains("no distributions are registered", DiagnosticsAvailabilityMessage.From(empty.RootElement));
        Assert.StartsWith("Diagnostics refreshed.", DiagnosticsAvailabilityMessage.From(installed.RootElement));
    }

    [Fact]
    public void Error_redaction_masks_profile_paths_and_quoted_or_unquoted_secrets()
    {
        const string error = "Could not read C:\\Users\\alice\\Documents\\report.json; access_token=abc123 apiToken=secondSecret password: 'value with spaces'";

        var redacted = DiagnosticsRedactor.RedactText(error, @"C:\Users\alice", @"C:\Users\alice\AppData\Local");

        Assert.DoesNotContain("alice", redacted);
        Assert.DoesNotContain("abc123", redacted);
        Assert.DoesNotContain("secondSecret", redacted);
        Assert.DoesNotContain("value with spaces", redacted);
        Assert.Contains("%USERPROFILE%\\Documents\\report.json", redacted);
        Assert.Contains("access_token=[redacted]", redacted);
        Assert.Contains("apiToken=[redacted]", redacted);
        Assert.Contains("password: [redacted]", redacted);
    }

    [Fact]
    public void Distribution_parser_returns_empty_for_null_or_empty_output()
    {
        using var nullJson = JsonDocument.Parse("null");
        using var arrayJson = JsonDocument.Parse("[]");
        using var scalarJson = JsonDocument.Parse("false");

        Assert.Empty(DistributionSummaryParser.Parse(nullJson.RootElement));
        Assert.Empty(DistributionSummaryParser.Parse(arrayJson.RootElement));
        Assert.Empty(DistributionSummaryParser.Parse(scalarJson.RootElement));
    }

    [Fact]
    public void Distribution_parser_handles_single_and_multiple_objects_case_insensitively()
    {
        using var singleJson = JsonDocument.Parse("""{"name":"Debian","version":2,"state":"Running","isDefault":true}""");
        using var arrayJson = JsonDocument.Parse("""[{"Name":"Debian","Version":2,"State":"Running"},{"name":"Ubuntu","version":1,"state":"Stopped"}]""");

        var single = Assert.Single(DistributionSummaryParser.Parse(singleJson.RootElement));
        var many = DistributionSummaryParser.Parse(arrayJson.RootElement);

        Assert.Equal("Debian", single.Name);
        Assert.True(single.IsDefault);
        Assert.Collection(many,
            item => Assert.Equal(("Debian", 2, "Running"), (item.Name, item.Version, item.State)),
            item => Assert.Equal(("Ubuntu", 1, "Stopped"), (item.Name, item.Version, item.State)));
    }

    [Fact]
    public void Distribution_parser_rejects_malformed_properties_for_ui_error_handling()
    {
        using var malformedJson = JsonDocument.Parse("""{"Name":"Debian","Version":"not-a-number"}""");
        Assert.Throws<JsonException>(() => DistributionSummaryParser.Parse(malformedJson.RootElement));
    }

    [Fact]
    public async Task Diagnostics_export_writes_valid_redacted_json_to_the_selected_path()
    {
        var path = Path.Combine(Path.GetTempPath(), $"WSLManager-Diagnostics-{Guid.NewGuid():N}.json");
        const string input = """{"Computer":"BUILD-123","Path":"C:\\Users\\alice\\private.json","WslConfig":{"Content":"[wsl2]\ncommand=secret command value\nmemory=8GB"}}""";

        try
        {
            await DiagnosticsExportService.WriteJsonAsync(path, input, @"C:\Users\alice", @"C:\Users\alice\AppData\Local");

            var output = await File.ReadAllTextAsync(path);
            using var parsed = JsonDocument.Parse(output);
            Assert.Equal("[local computer]", parsed.RootElement.GetProperty("Computer").GetString());
            Assert.DoesNotContain("alice", output);
            Assert.DoesNotContain("BUILD-123", output);
            Assert.DoesNotContain("secret command value", output);
            Assert.Contains("command=[redacted]", output);
            Assert.Contains("memory=8GB", output);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Builder_preserves_spaces_and_special_characters_as_individual_arguments()
    {
        var args = BackendArgumentBuilder.Build(new BackendRequest
        {
            Action = "Restore",
            Distro = "Ubuntu Restored",
            Source = @"D:\WSL Backupssome [safe] backup.tar",
            Destination = @"D:\WSL InstallsUbuntu Restored",
        });

        Assert.Contains(@"D:\WSL Backupssome [safe] backup.tar", args);
        Assert.Contains(@"D:\WSL InstallsUbuntu Restored", args);
        Assert.Contains("Ubuntu Restored", args);
        Assert.DoesNotContain(args, value => value.Contains(";", StringComparison.Ordinal));
    }

    [Fact]
    public void Builder_passes_multiple_backup_distributions_and_format()
    {
        var args = BackendArgumentBuilder.Build(new BackendRequest
        {
            Action = "Backup",
            Distributions = ["Debian", "Ubuntu"],
            Destination = @"E:\Backups",
            Format = "Vhdx"
        });

        Assert.Equal(new[] { "-Distro", "Debian", "Ubuntu" }, args.Skip(Array.IndexOf(args.ToArray(), "-Distro")).Take(3));
        Assert.Equal("Vhdx", args[Array.IndexOf(args.ToArray(), "-Format") + 1]);
        Assert.Contains("-Confirm:$false", args);
    }

    [Fact]
    public void WhatIf_does_not_add_confirmation_override()
    {
        var args = BackendArgumentBuilder.Build(new BackendRequest { Action = "Move", Distro = "Debian", Destination = @"D:\WSLDebian", WhatIf = true });

        Assert.Contains("-WhatIf", args);
        Assert.DoesNotContain("-Confirm:$false", args);
    }

    [Fact]
    public void Builder_suppresses_console_confirmation_for_ui_confirmed_mutations()
    {
        var args = BackendArgumentBuilder.Build(new BackendRequest { Action = "Terminate", Distro = "Debian" });

        Assert.Contains("-Confirm:$false", args);
    }

    [Fact]
    public async Task Client_passes_safe_argument_list_and_parses_json()
    {
        using var fixture = new BackendFixture(new ProcessResult(0, "{\"Name\":\"Debian\",\"Version\":2}", string.Empty));
        using var result = await fixture.Client.InvokeAsync(new BackendRequest { Action = "Info", Distro = "Debian (test)" });

        Assert.Equal("Debian", result.RootElement.GetProperty("Name").GetString());
        Assert.Equal("pwsh.exe", fixture.Runner.StartInfo!.FileName);
        Assert.Contains("Debian (test)", fixture.Runner.StartInfo.ArgumentList);
        Assert.Contains("-OutputFormat", fixture.Runner.StartInfo.ArgumentList);
        Assert.Equal("17", fixture.Runner.StartInfo.Environment["WSL_MANAGER_LOG_RETENTION_DAYS"]);
        Assert.Equal("1", fixture.Runner.StartInfo.Environment["WSL_MANAGER_UTF8_OUTPUT"]);
        Assert.DoesNotContain("-Confirm:$false", fixture.Runner.StartInfo.ArgumentList);
    }

    [Fact]
    public async Task Client_rejects_nonzero_exit_code_and_retains_exit_details()
    {
        using var fixture = new BackendFixture(new ProcessResult(7, string.Empty, "Destination already exists."));

        var exception = await Assert.ThrowsAsync<BackendInvocationException>(() =>
            fixture.Client.InvokeAsync(new BackendRequest { Action = "Restore", Distro = "New", Source = "backup.tar", Destination = "new-dir" }));

        Assert.Equal(7, exception.ExitCode);
        Assert.Contains("Destination already exists", exception.Message);
    }

    [Fact]
    public async Task Client_rejects_malformed_json_instead_of_treating_it_as_success()
    {
        using var fixture = new BackendFixture(new ProcessResult(0, "not-json", string.Empty));

        var exception = await Assert.ThrowsAsync<BackendInvocationException>(() =>
            fixture.Client.InvokeAsync(new BackendRequest { Action = "Diagnose" }));

        Assert.Contains("invalid JSON", exception.Message);
    }

    [Fact]
    public async Task Client_propagates_cancellation()
    {
        using var fixture = new BackendFixture(new ProcessResult(0, "{}", string.Empty), cancel: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.Client.InvokeAsync(new BackendRequest { Action = "Diagnose" }));
    }

    private sealed class BackendFixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "WslManager-desktop-test-" + Guid.NewGuid().ToString("N"));
        public BackendFixture(ProcessResult result, bool cancel = false)
        {
            Directory.CreateDirectory(_directory);
            Script = Path.Combine(_directory, "WSL-Manager.ps1");
            File.WriteAllText(Script, "# test marker");
            Runner = new FakeProcessRunner(result, cancel);
            Client = new PowerShellBackendClient(Script, "pwsh.exe", Runner, () => 17);
        }

        public string Script { get; }
        public FakeProcessRunner Runner { get; }
        public PowerShellBackendClient Client { get; }
        public void Dispose() => Directory.Delete(_directory, recursive: true);
    }

    private sealed class FakeProcessRunner(ProcessResult result, bool cancel) : IProcessRunner
    {
        public ProcessStartInfo? StartInfo { get; private set; }

        public Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            StartInfo = startInfo;
            if (cancel) throw new OperationCanceledException(cancellationToken);
            return Task.FromResult(result);
        }
    }
}

public sealed class SettingsTests
{
    [Theory]
    [InlineData("Start", true)]
    [InlineData("Info", true)]
    [InlineData("Backup", true)]
    [InlineData("Terminate", false)]
    [InlineData("SetDefault", false)]
    [InlineData("Convert", false)]
    [InlineData("Move", false)]
    [InlineData("Clone", false)]
    [InlineData("Resize", false)]
    [InlineData("Unregister", false)]
    public void Protected_distribution_policy_restricts_mutating_management_actions(string action, bool expected)
    {
        Assert.Equal(expected, ProtectedDistributionPolicy.IsAllowed(action, isProtected: true));
        Assert.True(ProtectedDistributionPolicy.IsAllowed(action, isProtected: false));
    }

    [Fact]
    public void Settings_default_to_system_theme_and_retention()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "settings.json");
        var settings = new DesktopSettingsStore(path).Load();

        Assert.Equal(ThemePreference.System, settings.Theme);
        Assert.Equal(90, settings.LogRetentionDays);
    }

    [Fact]
    public void Settings_are_persisted_and_retention_is_clamped()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WslManager-settings-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "desktop-settings.json");
        try
        {
            var store = new DesktopSettingsStore(path);
            store.Save(new DesktopSettings { Theme = ThemePreference.Dark, LogRetentionDays = 9000 });
            var loaded = store.Load();

            Assert.Equal(ThemePreference.Dark, loaded.Theme);
            Assert.Equal(3650, loaded.LogRetentionDays);
            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WslManager-settings-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "desktop-settings.json");
        try
        {
            File.WriteAllText(path, "{");
            Assert.Equal(new DesktopSettings(), new DesktopSettingsStore(path).Load());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Theory]
    [InlineData("Backup", true)]
    [InlineData("Restore", true)]
    [InlineData("Move", true)]
    [InlineData("Clone", true)]
    [InlineData("Terminate", true)]
    [InlineData("Resize", true)]
    [InlineData("List", false)]
    [InlineData("Diagnose", false)]
    public void Confirmation_policy_is_explicit(string action, bool expected) =>
        Assert.Equal(expected, ConfirmationPolicy.RequiresConfirmation(action));

    [Fact]
    public void Restore_confirmation_explains_root_default_and_no_overwrite()
    {
        var message = ConfirmationPolicy.Describe("Restore", "Ubuntu-Restored", @"D:\WSL Installs\Ubuntu-Restored");

        Assert.Contains("Existing distributions and directories will not be overwritten", message);
        Assert.Contains("may start as root", message);
        Assert.Contains("default Linux user", message);
    }

    [Fact]
    public void Clone_confirmation_does_not_duplicate_the_new_distribution_name()
    {
        var message = ConfirmationPolicy.Describe("Clone", "Debian → Debian-Test", @"D:\WSL Installs\Debian-Test");

        Assert.Contains("Create a separate clone (Debian → Debian-Test)", message);
        Assert.Contains("source distribution will be preserved", message);
        Assert.DoesNotContain("named Debian → Debian-Test at", message);
    }
}
