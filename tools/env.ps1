# Detects the toolchain (Windows / PowerShell). Dot-source it:  . tools/env.ps1
# Mirrors tools/env.sh. The detected Unity path is cached in tools/env.local (gitignored).
$ErrorActionPreference = 'Stop'
$global:ROOT = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$local = Join-Path $ROOT 'tools/env.local'

if (Test-Path $local) {
  Get-Content $local | ForEach-Object { if ($_ -match '^UNITY="(.*)"$') { $global:UNITY = $Matches[1] } }
}

function Find-Unity {
  $hubs = @("$env:ProgramFiles\Unity\Hub\Editor", "$HOME\Unity\Hub\Editor", "$HOME/Unity/Hub/Editor")
  foreach ($hub in $hubs) {
    if (-not (Test-Path $hub)) { continue }
    $versions = Get-ChildItem $hub -Directory | Where-Object { $_.Name -match '^6000\.(3|0)\.' } |
      Sort-Object @{ Expression = { $_.Name -match '^6000\.3' }; Descending = $true }, @{ Expression = { $_.Name }; Descending = $true }
    foreach ($v in $versions) {
      foreach ($exe in @('Editor\Unity.exe', 'Editor/Unity')) {
        $p = Join-Path $v.FullName $exe
        if (Test-Path $p) { return $p }
      }
    }
  }
  return $null
}

if (-not $global:UNITY -or -not (Test-Path $global:UNITY)) {
  $global:UNITY = Find-Unity
  if ($global:UNITY) { "UNITY=`"$global:UNITY`"" | Set-Content $local }
}

$unityAndroid = Join-Path (Split-Path $global:UNITY) 'Data/PlaybackEngines/AndroidPlayer'
if (-not $env:JAVA_HOME_17) { $env:JAVA_HOME = Join-Path $unityAndroid 'OpenJDK' } else { $env:JAVA_HOME = $env:JAVA_HOME_17 }
if (-not $env:ANDROID_SDK_ROOT) {
  $env:ANDROID_SDK_ROOT = if ($IsLinux -or $IsMacOS) { "$HOME/Android/Sdk" } else { "$env:LOCALAPPDATA\Android\Sdk" }
}
$env:ANDROID_HOME = $env:ANDROID_SDK_ROOT
$sep = [IO.Path]::PathSeparator
$env:PATH = @("$HOME/development/flutter/bin", "$env:ANDROID_SDK_ROOT/platform-tools", "$env:ANDROID_SDK_ROOT/emulator", $env:PATH) -join $sep
$global:APP_ID = 'com.gamefactory.urbanrunner'

function Assert-EditorClosed {
  $open = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -like 'Unity*' -and $_.CommandLine -notmatch '-batchmode' -and $_.CommandLine -like "*$ROOT*unity*" }
  if ($open) { throw 'UR_ERROR Unity Editor has this project open. Close it, then retry.' }
}

# Run Unity batchmode and wait (Unity.exe returns immediately on Windows, HANDOVER §8.3).
# Like tools/env.sh: if Unity reports its verdict but hangs during shutdown, stop it after 90 s and return the verdict.
function Invoke-Unity([string]$LogName, [string[]]$UnityArgs) {
  New-Item -ItemType Directory -Force -Path "$ROOT/build/logs", "$ROOT/build/test-results" | Out-Null
  Assert-EditorClosed
  $logFile = "$ROOT/build/logs/$LogName.log"
  Remove-Item $logFile -ErrorAction SilentlyContinue
  $env:ANDROID_ADB_SERVER_PORT = '5099'
  $all = @('-batchmode', '-projectPath', "`"$ROOT/unity`"", '-logFile', "`"$logFile`"") + $UnityArgs
  $p = Start-Process -FilePath $global:UNITY -ArgumentList $all -PassThru -NoNewWindow
  Remove-Item Env:ANDROID_ADB_SERVER_PORT
  $limit = if ($env:UR_BATCHMODE_TIMEOUT) { [int]$env:UR_BATCHMODE_TIMEOUT } else { 2700 }
  $sw = [Diagnostics.Stopwatch]::StartNew()
  $verdict = $null; $verdictAt = 0
  while (-not $p.HasExited) {
    Start-Sleep 2
    if ($null -eq $verdict -and (Test-Path $logFile)) {
      $m = Select-String -Path $logFile -Pattern 'Exiting with code (\d+)|UR_BATCH_EXIT (\d+)' | Select-Object -First 1
      if ($m) { $verdict = [int](($m.Matches[0].Groups | Where-Object { $_.Success } | Select-Object -Last 1).Value); $verdictAt = $sw.Elapsed.TotalSeconds }
      # A step's own final success line counts too (Unity has hung right after a finished Flutter export).
      elseif ($env:UR_SUCCESS_MARKER -and (Select-String -Path $logFile -SimpleMatch -Quiet -Pattern $env:UR_SUCCESS_MARKER)) { $verdict = 0; $verdictAt = $sw.Elapsed.TotalSeconds }
    }
    if ($null -ne $verdict -and ($sw.Elapsed.TotalSeconds - $verdictAt) -ge 90) {
      Write-Warning "unity '$LogName' reported exit $verdict but hung on shutdown; stopping it"
      Stop-Process -Id $p.Id -Force; return $verdict
    }
    if ($sw.Elapsed.TotalSeconds -ge $limit) { Stop-Process -Id $p.Id -Force; Write-Host "UR_ERROR unity '$LogName' timed out"; return 124 }
  }
  return $p.ExitCode
}
