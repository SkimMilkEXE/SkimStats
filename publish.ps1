# builds the release zip: .\publish.ps1 (version comes from the csproj)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "artifacts\publish"
$version = ([xml](Get-Content (Join-Path $root "src\SkimStats\SkimStats.csproj"))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\SkimStats") -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# only what users need, skia's native .pdb files get left out
$stage = Join-Path $root "artifacts\SkimStats"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage "Tools") | Out-Null
Copy-Item (Join-Path $out "SkimStats.exe") $stage
Copy-Item (Join-Path $out "Tools\PresentMon.exe") (Join-Path $stage "Tools")
Copy-Item (Join-Path $root "LICENSE"), (Join-Path $root "THIRD-PARTY-NOTICES.md") $stage

$zip = Join-Path $root "artifacts\SkimStats-v$version-win-x64.zip"
Compress-Archive -Path $stage -DestinationPath $zip -Force
"built $zip"
