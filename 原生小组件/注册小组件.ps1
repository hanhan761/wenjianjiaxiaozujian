$ErrorActionPreference = 'Stop'

$devMode = Get-ItemPropertyValue -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock' -Name 'AllowDevelopmentWithoutDevLicense' -ErrorAction SilentlyContinue
if ($devMode -ne 1) {
    throw '请先在 Windows 设置中启用开发者模式，然后重新运行此脚本。'
}

$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $root '文件夹卡片-Win11.msix'
$destination = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FolderCardsWidget.Dev'
$hash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [IO.Compression.ZipFile]::OpenRead($package)
try {
    $manifestReader = [IO.StreamReader]::new($zip.GetEntry('AppxManifest.xml').Open())
    try { [xml] $packageManifest = $manifestReader.ReadToEnd() } finally { $manifestReader.Dispose() }
} finally { $zip.Dispose() }

$version = [version]$packageManifest.Package.Identity.Version
$registered = Get-AppxPackage -Name 'FolderCardsWidget.Local'
$installed = $registered
if (-not $registered -or $registered.Version -lt $version) {
    $deploy = Join-Path $destination "$($hash.Substring(0,12))-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($deploy) | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($package, $deploy)
    $manifest = Join-Path $deploy 'AppxManifest.xml'
    [xml] $document = Get-Content -LiteralPath $manifest -Encoding utf8 -Raw
    $document.Package.Identity.Publisher = 'CN=Folder Cards Local'
    $document.Save($manifest)

    Add-AppxPackage -Register $manifest -ForceApplicationShutdown
    $installed = Get-AppxPackage -Name 'FolderCardsWidget.Local'
    if (-not $installed -or $installed.Version -ne $version) {
        throw 'Windows 未返回已注册的文件夹组件。'
    }
}

# Retire the login listener installed by version 1.9.0.
$session = (Get-Process -Id $PID).SessionId
$prefix = $destination
$prefix += '\'
foreach($helper in Get-CimInstance Win32_Process -Filter "Name='FolderCardsWidget.exe'") {
    if ($helper.SessionId -eq $session -and $helper.ExecutablePath -and
        $helper.ExecutablePath.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        $helper.CommandLine -match '(?:^|\s)--close-explorer-hotkey\s*$') {
        Stop-Process -Id $helper.ProcessId
    }
}
$link = Join-Path ([Environment]::GetFolderPath('Startup')) 'FolderCards - Close Explorer Hotkey.lnk'
if (Test-Path -LiteralPath $link) { Remove-Item -LiteralPath $link }

Write-Output '按 Win + W，在文件夹卡片底部使用「关闭所有文件夹窗口」。'
