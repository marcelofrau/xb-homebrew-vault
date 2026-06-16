# Xbox Device Portal API

## Base URL

```
https://<xbox-ip>:11443
```

## Authentication

HTTP Basic Authentication with the username and password configured in Xbox Developer Mode settings.

## Endpoints

### GET /api/os/info

Returns device information. Used for connection testing.

```json
{
  "Name": "XboxOne",
  "Publisher": "Microsoft",
  "Version": "10.0.22621.1000"
}
```

### GET /api/app/packagemanager/packages

Lists all installed packages on the Xbox.

```json
{
  "InstalledPackages": [
    {
      "PackageFullName": "RetroArch.20319A388A0CE8_px48cv62crvag",
      "PackageFamilyName": "RetroArch.20319A388A0CE8",
      "PackageOrigin": 0,
      "PackageSize": 124518400,
      "InstallDate": "2026-01-15T12:00:00Z"
    }
  ]
}
```

### POST /api/app/packagemanager/package

Installs a package. Accepts multipart/form-data with the package file.

Query parameters:
- `package` (optional): URL to download the package from

### DELETE /api/app/packagemanager/package

Uninstalls a package.

Query parameters:
- `package`: Full package name to uninstall

## References

- [Windows Device Portal API](https://learn.microsoft.com/en-us/windows/uwp/debug-test-perf/device-portal-api-core)
- [Xbox Developer Mode Guide](https://wiki.sternserv.xyz/docs/xbox-setup/xbox-developer-mode-setup)
