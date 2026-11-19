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

$argument = '--close-explorer-hotkey'
$exe = Join-Path $installed.InstallLocation 'FolderCardsWidget.exe'
$startup = Join-Path ([Environment]::GetFolderPath('Startup')) 'FolderCards - Close Explorer Hotkey.lnk'
$shell = New-Object -ComObject WScript.Shell
$link = $shell.CreateShortcut($startup)
$link.TargetPath = $exe
$link.WorkingDirectory = $installed.InstallLocation
$link.Arguments = $argument
$link.Description = 'Folder Cards close-Explorer shortcut'
$link.Save()

$session = (Get-Process -Id $PID).SessionId
$ownPrefix = [IO.Path]::GetFullPath($destination).TrimEnd('\') + '\'
$helpers = @(Get-CimInstance Win32_Process -Filter "Name='FolderCardsWidget.exe'" |
    Where-Object {
        $_.SessionId -eq $session -and $_.ExecutablePath -and
        $_.ExecutablePath.StartsWith($ownPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        $_.CommandLine -match '(?:^|\s)--close-explorer-hotkey\s*$'
    })

foreach ($old in $helpers | Where-Object ExecutablePath -ne $exe) {
    Stop-Process -Id $old.ProcessId -ErrorAction SilentlyContinue
}
if (-not ($helpers | Where-Object ExecutablePath -eq $exe)) {
    Start-Process -FilePath $exe -ArgumentList $argument -WindowStyle Hidden
}

for ($try = 0; $try -lt 20; $try++) {
    $ready = $null
    if ([Threading.Mutex]::TryOpenExisting('Local\FolderCardsWidget.CloseExplorerHotkey', [ref]$ready)) {
        $ready.Dispose()
        Write-Output '关闭文件夹窗口快捷键已启用。'
        return
    }
    Start-Sleep -Milliseconds 150
}
Write-Warning '关闭文件夹窗口快捷键未注册，可能已被其他应用占用。'
