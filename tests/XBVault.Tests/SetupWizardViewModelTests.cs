using XBVault.Models;
using XBVault.Services;
using XBVault.ViewModels;

namespace XBVault.Tests;

public class SetupWizardViewModelTests
{
    // ---- CanGoNext: Step 0 (Welcome) always true ----

    [Fact]
    public void CanGoNext_WelcomeStep_AlwaysTrue()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService());
        vm.CurrentStep = 0;
        Assert.True(vm.CanGoNext);
    }

    // ---- CanGoNext: Step 1 (Console) requires valid address + port ----

    [Fact]
    public void CanGoNext_ConsoleStep_EmptyAddress_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "";
        vm.Port = "11443";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_ValidIPv4_ValidPort_True()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "192.168.1.100";
        vm.Port = "11443";
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_ValidHostname_ValidPort_True()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "xbox.local";
        vm.Port = "443";
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_ValidAddress_InvalidPort_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "192.168.1.100";
        vm.Port = "99999";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_AddressWithPort_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "10.0.0.1:11443";
        vm.Port = "11443";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_EmptyPort_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "192.168.1.100";
        vm.Port = "";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_ConsoleStep_ValidIPv6_ValidPort_True()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        vm.Address = "::1";
        vm.Port = "11443";
        Assert.True(vm.CanGoNext);
    }

    // ---- CanGoNext: Step 2 (Auth) requires username + password ----

    [Fact]
    public void CanGoNext_AuthStep_BothPresent_True()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 2 };
        vm.Username = "user";
        vm.Password = "pass";
        Assert.True(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_AuthStep_EmptyUsername_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 2 };
        vm.Username = "";
        vm.Password = "pass";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_AuthStep_EmptyPassword_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 2 };
        vm.Username = "user";
        vm.Password = "";
        Assert.False(vm.CanGoNext);
    }

    [Fact]
    public void CanGoNext_AuthStep_BothEmpty_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 2 };
        vm.Username = "";
        vm.Password = "";
        Assert.False(vm.CanGoNext);
    }

    // ---- CanGoNext: Step 3 (Ready) always false ----

    [Fact]
    public void CanGoNext_ReadyStep_False()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 3 };
        Assert.False(vm.CanGoNext);
    }

    // ---- CanGoBack ----

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void CanGoBack_DependsOnStep(int step, bool expected)
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = step };
        Assert.Equal(expected, vm.CanGoBack);
    }

    // ---- Step flags ----

    [Theory]
    [InlineData(0, true, false, false, false)]
    [InlineData(1, false, true, false, false)]
    [InlineData(2, false, false, true, false)]
    [InlineData(3, false, false, false, true)]
    public void StepFlags_ReflectCurrentStep(int step, bool welcome, bool console, bool auth, bool ready)
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = step };
        Assert.Equal(welcome, vm.IsWelcomeStep);
        Assert.Equal(console, vm.IsConsoleStep);
        Assert.Equal(auth, vm.IsAuthStep);
        Assert.Equal(ready, vm.IsReadyStep);
    }

    // ---- GoNext / GoBack ----

    [Fact]
    public void GoNext_IncrementsStep()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 0 };
        vm.GoNextCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStep);
    }

    [Fact]
    public void GoNext_MaxStep3_NoIncrement()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 3 };
        vm.GoNextCommand.Execute(null);
        Assert.Equal(3, vm.CurrentStep);
    }

    [Fact]
    public void GoBack_DecrementsStep()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 2 };
        vm.GoBackCommand.Execute(null);
        Assert.Equal(1, vm.CurrentStep);
    }

    [Fact]
    public void GoBack_Step0_NoDecrement()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 0 };
        vm.GoBackCommand.Execute(null);
        Assert.Equal(0, vm.CurrentStep);
    }

    // ---- PropertyChanged ----

    [Fact]
    public void AddressChanged_RaisesCanGoNext()
    {
        var vm = new SetupWizardViewModel(new FakeAuthService()) { CurrentStep = 1 };
        var raised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SetupWizardViewModel.CanGoNext)) raised = true;
        };
        vm.Address = "192.168.1.1";
        Assert.True(raised);
    }

    // ---- Fake service ----

    private class FakeAuthService : Services.IXboxAuthService
    {
        public bool IsConnected => false;
        public bool IsConfigured => false;
        public string? Host => null;
        public string? SmbPassword => null;
        public event Action<bool>? ConnectionChanged { add { } remove { } }
        public void Configure(string baseUrl, string username, string password) { }
        public SshConnectionInfo GetSshCredentials() => new("", 22, "", "");
        public System.Threading.Tasks.Task<string?> FetchSmbPasswordAsync() => System.Threading.Tasks.Task.FromResult<string?>(null);
        public string? GetDevPortalUrl() => null;
        public void MarkConnected() { }
        public void Disconnect() { }
        public System.Threading.Tasks.Task<bool> EnsureConnectedAsync(CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(false);
        public System.Threading.Tasks.Task<ConnectionTestResult> TestConnectionAsync(CancellationToken ct = default) => System.Threading.Tasks.Task.FromResult(new ConnectionTestResult(false, null, null));
        public void Dispose() { }
    }
}
