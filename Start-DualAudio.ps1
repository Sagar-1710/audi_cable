# Start-DualAudio.ps1
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " 🎧 DUALAUDIO HUB - Starting Dashboard & Engine...      " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan

# Launch default browser
Start-Process "http://127.0.0.1:40880"

# Start Node.js server
& node server.js
