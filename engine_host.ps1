# DualAudio Engine Background Host
param (
    [string]$EngineDll = ""
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Compile / Load DualAudioEngine
Add-Type -Path (Join-Path $ScriptDir "DualAudioEngine.cs")

$controller = New-Object DualAudioEngine.DualRepeaterController

# Keep process alive and process JSON commands from standard input
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding = [System.Text.Encoding]::UTF8

Write-Output '{"event":"ready"}'

while ($true) {
    $line = [Console]::ReadLine()
    if ($null -eq $line) { break }
    $line = $line.Trim()
    if ($line.Length -eq 0) { continue }

    try {
        if ($line -eq "PING") {
            Write-Output '{"status":"ok","pong":true}'
        }
        elseif ($line -eq "GET_DEVICES") {
            $json = [DualAudioEngine.DualRepeaterController]::GetRenderDevicesJson()
            Write-Output ('{"status":"ok","devices":' + $json + '}')
        }
        elseif ($line -eq "GET_STATUS") {
            $json = $controller.GetStatusJson()
            Write-Output ('{"status":"ok","status":' + $json + '}')
        }
        elseif ($line.StartsWith("START ")) {
            $parts = $line.Substring(6).Split('|')
            $src = $parts[0]
            $t1 = $parts[1]
            $t2 = $parts[2]

            $ok = $controller.Start($src, $t1, $t2)
            $okStr = if ($ok) { "true" } else { "false" }
            Write-Output ('{"status":"ok","started":' + $okStr + '}')
        }
        elseif ($line -eq "STOP") {
            $controller.Stop()
            Write-Output '{"status":"ok","stopped":true}'
        }
        else {
            Write-Output ('{"status":"error","message":"Unknown command: ' + $line + '"}')
        }
    }
    catch {
        $msg = $_.Exception.Message.Replace('"', '\"')
        Write-Output ('{"status":"error","message":"' + $msg + '"}')
    }
}

$controller.Stop()
