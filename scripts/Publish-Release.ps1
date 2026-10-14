param([string]$Version = 'v1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^v[0-9]+\.[0-9]+\.[0-9]+$') { throw 'Версия должна иметь вид v1.0.0' }
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'Backend_Quques_Kashnikov/Backend_Quques_Kashnikov.csproj'
$stage = Join-Path $root ('artifacts/package-' + [guid]::NewGuid().ToString('N'))
$api = Join-Path $stage 'api'
dotnet publish $project -c Release --no-restore -o $api
if ($LASTEXITCODE -ne 0) { throw 'Не удалось опубликовать проект.' }
Copy-Item -LiteralPath (Join-Path $root 'postman') -Destination $stage -Recurse
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $stage
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $stage -Recurse
@'
@echo off
cd /d "%~dp0api"
dotnet Backend_Quques_Kashnikov.dll --urls http://localhost:5046
pause
'@ | Set-Content -LiteralPath (Join-Path $stage 'Start-Api.cmd') -Encoding ascii
$zip = Join-Path $root "artifacts/EduQueue-Lab1-$Version.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Host "Release package: $zip"
