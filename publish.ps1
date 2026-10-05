# builds the release exe into artifacts\SkimStats.exe (presentmon is packed inside it)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root "artifacts\publish"

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish (Join-Path $root "src\SkimStats") -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $out
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# only the exe, skia's native .pdb files get left out
Copy-Item (Join-Path $out "SkimStats.exe") (Join-Path $root "artifacts\SkimStats.exe") -Force
"built $(Join-Path $root "artifacts\SkimStats.exe")"
