$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$PSScriptRoot\Run XBVault.lnk")
$shortcut.TargetPath = "pwsh.exe"
$shortcut.Arguments = "-NoExit -ExecutionPolicy Bypass -File `"$PSScriptRoot\run.ps1`""
$shortcut.WorkingDirectory = $PSScriptRoot
$shortcut.Description = "Run XBVault in dev mode"
$shortcut.Save()

Write-Host "Shortcut created: $PSScriptRoot\Run XBVault.lnk" -ForegroundColor Green
