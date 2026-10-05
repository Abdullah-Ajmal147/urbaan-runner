# Installs an APK on the emulator, lets it run, captures logcat + screenshots, fails on any crash marker.
# Mirrors tools/emulator_smoke.sh.   pwsh tools/emulator_smoke.ps1 [-Apk path] [-Seconds 60] [-Avd ur_api36]
param([string]$Apk, [int]$Seconds = 60, [string]$Avd = 'ur_api36')
. "$PSScriptRoot/env.ps1"
$ErrorActionPreference = 'Continue'
Set-Location $ROOT
$version = ((Select-String -Path app/pubspec.yaml -Pattern '^version:\s*([^+]+)').Matches[0].Groups[1].Value).Trim()
if (-not $Apk) { $Apk = "build/out/UrbanRunner-$version-standalone-emulator.apk" }
if (-not (Test-Path $Apk)) { Write-Host "UR_ERROR apk not found: $Apk"; exit 2 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logcat = "build/logs/smoke_logcat_$stamp.txt"
$shots = "build/smoke/$stamp"
New-Item -ItemType Directory -Force -Path $shots | Out-Null

$serial = (adb devices | Select-String '^(emulator-\d+)\s+device').Matches | Select-Object -First 1 | ForEach-Object { $_.Groups[1].Value }
if (-not $serial) {
  Start-Process emulator -ArgumentList @('-avd', $Avd, '-no-snapshot-save', '-no-boot-anim', '-gpu', 'host')
  adb wait-for-device
  $serial = (adb devices | Select-String '^(emulator-\d+)\s+device').Matches[0].Groups[1].Value
}
for ($i = 0; $i -lt 120; $i++) { if ((adb -s $serial shell getprop sys.boot_completed).Trim() -eq '1') { break }; Start-Sleep 2 }

$aapt2 = Get-ChildItem "$env:ANDROID_SDK_ROOT/build-tools/*/aapt2*" | Sort-Object FullName | Select-Object -Last 1
$pkg = (& $aapt2.FullName dump packagename $Apk).Trim()
Write-Host "== smoke $Apk ($pkg) on $serial for ${Seconds}s"
adb -s $serial install -r $Apk | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host 'UR_ERROR install failed'; exit 1 }
adb -s $serial logcat -c
$lc = Start-Process adb -ArgumentList @('-s', $serial, 'logcat', '-v', 'time') -RedirectStandardOutput $logcat -PassThru -NoNewWindow
adb -s $serial shell monkey -p $pkg -c android.intent.category.LAUNCHER 1 | Out-Null

$fail = $false
$step = [Math]::Max(1, [int]($Seconds / 4))
foreach ($i in 1..4) {
  Start-Sleep $step
  adb -s $serial shell screencap -p /sdcard/s.png; adb -s $serial pull /sdcard/s.png "$shots/shot_$i.png" | Out-Null
  if (-not (adb -s $serial shell pidof $pkg)) { Write-Host 'UR_ERROR process died'; $fail = $true; break }
}
adb -s $serial shell dumpsys meminfo $pkg | Select-String 'TOTAL PSS|TOTAL:' | Select-Object -First 1
Stop-Process $lc -ErrorAction SilentlyContinue

Select-String -Path $logcat -Pattern 'UR_PERF' | Select-Object -Last 5
$crashes = Select-String -Path $logcat -Pattern 'FATAL EXCEPTION|ANR in|Fatal signal|\*\*\* \*\*\* \*\*\*|backtrace:|UR_ERROR'
if ($crashes) { $crashes | Select-Object -First 20; $fail = $true }
Write-Host "logcat: $logcat  screenshots: $shots"
if ($fail) { Write-Host 'SMOKE FAILED'; exit 1 } else { Write-Host 'SMOKE CLEAN'; exit 0 }
