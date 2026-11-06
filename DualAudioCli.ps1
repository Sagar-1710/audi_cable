<#
.SYNOPSIS
    DualAudio CLI - Pure Live Audio Streaming to Two Bluetooth Devices

.DESCRIPTION
    Captures from a single virtual medium (or Windows audio) and streams it live
    and simultaneously to two Bluetooth devices with zero delay and zero distortion.

.EXAMPLE
    .\DualAudioCli.ps1 -List
    Lists all detected audio render devices with Bluetooth indicators.

.EXAMPLE
    .\DualAudioCli.ps1
    Starts streaming directly to both connected Bluetooth devices.
#>

param (
    [switch]$List,
    [string]$Source = "default",
    [string]$Target1 = "",
    [string]$Target2 = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Load DualAudioEngine
Add-Type -Path (Join-Path $ScriptDir "DualAudioEngine.cs")

if ($List) {
    Write-Host "`n========================================================" -ForegroundColor Cyan
    Write-Host " [DUALAUDIO HUB] - DETECTED AUDIO PLAYBACK ENDPOINTS    " -ForegroundColor Cyan
    Write-Host "========================================================`n" -ForegroundColor Cyan

    $devices = [DualAudioEngine.DualRepeaterController]::GetRenderDevices()
    foreach ($d in $devices) {
        $tag = ""
        $color = "Gray"
        if ($d.IsBluetooth) {
            $tag = "[BLUETOOTH]"
            $color = "Green"
        } elseif ($d.IsVirtualCable) {
            $tag = "[VIRTUAL CABLE]"
            $color = "Magenta"
        }
        if ($d.IsDefault) {
            $tag += " [DEFAULT]"
        }

        Write-Host (" {0,-40} {1,-20} {2}Hz {3}ch" -f $d.Name, $tag, $d.SampleRate, $d.Channels) -ForegroundColor $color
        Write-Host ("   ID: " + $d.Id) -ForegroundColor DarkGray
    }
    Write-Host ""
    exit 0
}

# Auto-resolve targets
$devices = [DualAudioEngine.DualRepeaterController]::GetRenderDevices()

function Find-Device($query) {
    if ([string]::IsNullOrEmpty($query)) { return $null }
    $dev = $devices | Where-Object { $_.Id -eq $query -or $_.Name -like "*$query*" } | Select-Object -First 1
    return $dev
}

$devT1 = Find-Device $Target1
$devT2 = Find-Device $Target2

if ($null -eq $devT1) {
    $dubstep = $devices | Where-Object { $_.Name -like "*Dubstep*" } | Select-Object -First 1
    if ($dubstep) {
        $devT1 = $dubstep
    } else {
        $btDevs = $devices | Where-Object { $_.IsBluetooth }
        $devT1 = if ($btDevs.Count -gt 0) { $btDevs[0] } else { $devices[0] }
    }
}

if ($null -eq $devT2) {
    $superpods = $devices | Where-Object { $_.Name -like "*Superpod*" } | Select-Object -First 1
    if ($superpods) {
        $devT2 = $superpods
    } else {
        $otherBt = $devices | Where-Object { $_.IsBluetooth -and $_.Id -ne $devT1.Id } | Select-Object -First 1
        $devT2 = if ($otherBt) { $otherBt } else { ($devices | Where-Object { $_.Id -ne $devT1.Id } | Select-Object -First 1) }
    }
}

Write-Host "`n========================================================" -ForegroundColor Cyan
Write-Host " [DUALAUDIO HUB] - LIVE DUAL-BLUETOOTH STREAMING        " -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host (" Single Source: " + $Source) -ForegroundColor Yellow
Write-Host (" Bluetooth 1:   " + $devT1.Name) -ForegroundColor Green
if ($devT2) {
    Write-Host (" Bluetooth 2:   " + $devT2.Name) -ForegroundColor Magenta
}
Write-Host "--------------------------------------------------------" -ForegroundColor DarkGray
Write-Host " Streaming live PCM audio simultaneously to both devices." -ForegroundColor Gray
Write-Host " Press [Q] or [Ctrl+C] to stop streaming.`n" -ForegroundColor Gray

$controller = New-Object DualAudioEngine.DualRepeaterController
$t1Id = if ($devT1) { $devT1.Id } else { "" }
$t2Id = if ($devT2) { $devT2.Id } else { "" }

$started = $controller.Start($Source, $t1Id, $t2Id)
if (-not $started) {
    Write-Host "Failed to start live audio engine." -ForegroundColor Red
    exit 1
}

try {
    while ($controller.GetIsRunning()) {
        Start-Sleep -Milliseconds 120

        $pL = [Math]::Min(20, [int]($controller.PeakL * 20))
        $pR = [Math]::Min(20, [int]($controller.PeakR * 20))

        $barL = ("#" * $pL).PadRight(20, "-")
        $barR = ("#" * $pR).PadRight(20, "-")

        $frames = $controller.ProcessedFrames

        Write-Host -NoNewline ("`r [VU: L [$barL] R [$barR]]  Live Frames Streamed: {0,-10} " -f $frames)

        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            if ($key.Key -eq [ConsoleKey]::Q -or $key.Key -eq [ConsoleKey]::Escape) {
                break
            }
        }
    }
}
finally {
    $controller.Stop()
    Write-Host "`n`nStreaming stopped cleanly.`n" -ForegroundColor Cyan
}
