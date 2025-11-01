param(
    [string]$ServiceName = "MediaServer",
    [string]$InstallPath = "C:\\MediaServer",
    [int]$Port = 8090
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing MediaServer.Server for win-x64..."
dotnet publish ..\src\MediaServer.Server\MediaServer.Server.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o "$PSScriptRoot\publish"

if (Test-Path $InstallPath) {
    Write-Host "Removing existing install at $InstallPath"
    Remove-Item $InstallPath -Recurse -Force
}

Write-Host "Copying payload to $InstallPath"
New-Item -ItemType Directory -Path $InstallPath | Out-Null
Copy-Item "$PSScriptRoot\publish\*" $InstallPath -Recurse

$exe = Join-Path $InstallPath "MediaServer.Server.exe"

if (-not (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue)) {
    Write-Host "Creating Windows service $ServiceName"
    sc.exe create $ServiceName binPath= "\"$exe\"" start= auto | Out-Null
} else {
    Write-Host "Service $ServiceName already exists; updating binary path"
    sc.exe config $ServiceName binPath= "\"$exe\"" start= auto | Out-Null
}

Write-Host "Granting firewall access on TCP port $Port"
if (-not (Get-NetFirewallRule -DisplayName "MediaServer-$Port" -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName "MediaServer-$Port" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
}

Write-Host "Starting service $ServiceName"
Start-Service $ServiceName

Write-Host "Install complete"
