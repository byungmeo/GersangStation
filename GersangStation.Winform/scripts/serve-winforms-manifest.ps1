param(
    [string]$Root = "E:\Projects\dotnet\GersangStation",
    [int]$Port = 8765
)

$ErrorActionPreference = "Stop"

$python = Get-Command python -ErrorAction Stop
Write-Host "Serving $Root on http://localhost:$Port/"
& $python.Source -m http.server $Port --bind 127.0.0.1 --directory $Root
