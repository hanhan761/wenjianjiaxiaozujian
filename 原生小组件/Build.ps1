param(
    [string]$Output = (Join-Path (Split-Path -Parent $PSScriptRoot) '文件夹卡片-Win11.msix'),
    [Parameter(Mandatory = $true)][string]$MakeAppx
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CsConsoleWidgetProvider'
$stage = Join-Path ([IO.Path]::GetTempPath()) ("FolderCardsWidget-" + [Guid]::NewGuid().ToString("N"))

dotnet publish (Join-Path $project 'CsConsoleWidgetProvider.csproj') -c Release --no-self-contained -o $stage
if ($LASTEXITCODE -ne 0) { throw "The Widget Provider build failed." }

Copy-Item -LiteralPath (Join-Path $project 'Assets') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $project 'ProviderAssets') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $project 'Package.appxmanifest') -Destination (Join-Path $stage 'AppxManifest.xml')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination $stage
New-Item -ItemType Directory -Path (Join-Path $stage 'Public') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $stage 'Public' 'LICENSE.txt')

& $MakeAppx pack /d $stage /p $Output /o
if ($LASTEXITCODE -ne 0) { throw "The package could not be created. Staging directory: $stage" }
