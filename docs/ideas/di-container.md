# DI Container + Composition Root

**Impact:** High | **Effort:** Medium | **Suggested priority:** Phase 2

## Problem

Project does not use dependency injection consistently:

- `SettingsService` accessed as static singleton (`SettingsService.Current.XboxConnection`)
- `MainViewModel` receives delegates injected by the View (`ShowAboutAction`, `ShowConnectAction`)
- `XboxDeviceService` creates `HttpClient` internally (not injected)
- ViewModels resolved manually or via code-behind
- No centralized composition root

This makes testing, swapping implementations, and adding new services difficult.

## Proposal

### 1. Add Microsoft.Extensions.DependencyInjection
Lightweight package, standard in .NET ecosystem.

### 2. Composition root in Program.cs
```csharp
var services = new ServiceCollection();
services.AddSingleton<SettingsService>();
services.AddSingleton<IXboxAuthService, XboxAuthService>();
services.AddSingleton<IXboxPackageService, XboxPackageService>();
services.AddSingleton<IXboxProcessService, XboxProcessService>();
services.AddSingleton<IXboxSystemService, XboxSystemService>();
services.AddSingleton<IXboxNetworkService, XboxNetworkService>();
services.AddSingleton<IXboxPerformanceService, XboxPerformanceService>();
services.AddTransient<MainViewModel>();
services.AddTransient<BrowseViewModel>();
// ...
var provider = services.BuildServiceProvider();
var vm = provider.GetRequiredService<MainViewModel>();
```

### 3. Services to make DI-friendly
- `SettingsService` — remove static singleton pattern, inject where needed
- `XboxAuthService` — receive `HttpClient` via `IHttpClientFactory` or injection
- `PackageInstallService` — already receives `CacheService` and `XboxPackageService` in constructor (good)
- `CacheService` — make injectable (already instantiable)

### 4. ViewModels
- All ViewModels currently use `SettingsService.Current.XboxConnection` — migrate to constructor injection
- Delegates like `ShowAboutAction` can become events or be replaced by `IDialogService`

### 5. DialogService (optional, advanced)
```csharp
interface IDialogService
{
    Task ShowAboutAsync();
    Task<ConnectionResult?> ShowConnectAsync();
    Task<bool> ShowConfirmAsync(string message);
    // ...
}
```

This eliminates delegates and allows testing ViewModels without real UI.

### Dependencies
- `Microsoft.Extensions.DependencyInjection` NuGet
- Ideally after [Split XboxDeviceService](refactor-xboxdeviceservice.md) and before [Testing](testing-infrastructure.md)

### Files to modify
- `XBVault.csproj` — add package
- `Program.cs` — composition root
- `App.axaml.cs` — pass provider or use service locator as fallback
- All ViewModels — DI constructors
- `SettingsService` — remove static singleton
