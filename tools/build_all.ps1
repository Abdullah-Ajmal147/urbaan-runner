# Builds every output into build/out/. Mirrors tools/build_all.sh.
#   pwsh tools/build_all.ps1 [emulator] [phone] [aab] [standalone]   (default: all)
. "$PSScriptRoot/env.ps1"
Set-Location $ROOT
$targets = if ($args.Count) { $args } else { @('emulator', 'phone', 'aab', 'standalone') }
$version = ((Select-String -Path app/pubspec.yaml -Pattern '^version:\s*([^+]+)').Matches[0].Groups[1].Value).Trim()
$out = "$ROOT/build/out"
New-Item -ItemType Directory -Force -Path $out, build/logs | Out-Null

function Export-Unity([string]$kind) {
  Write-Host "== export unityLibrary ($kind)"
  $a = @('-nographics', '-quit', '-buildTarget', 'Android', '-executeMethod', 'BuildScripts.ExportForFlutter', '-exportPath', "`"$ROOT/app/android/unityLibrary`"")
  if ($kind -eq 'emulator') { $a += '-emulator' }
  $env:UR_SUCCESS_MARKER = 'Building project for Flutter succeeded'
  $rc = Invoke-Unity "export_$kind" $a
  Remove-Item Env:UR_SUCCESS_MARKER
  if ($rc -ne 0) { throw "Unity export ($kind) failed; see build/logs/export_$kind.log" }
}

function Invoke-Flutter([string]$log, [string[]]$fargs) {
  Push-Location app
  flutter @fargs *> "$ROOT/build/logs/$log.log"
  $code = $LASTEXITCODE
  Pop-Location
  if ($code -ne 0) { throw "flutter $($fargs -join ' ') failed; see build/logs/$log.log" }
}

if ($targets -contains 'emulator') {
  Export-Unity 'emulator'
  Invoke-Flutter 'flutter_apk_emulator' @('build', 'apk', '--release', '--target-platform', 'android-arm,android-arm64,android-x64')
  Copy-Item app/build/app/outputs/flutter-apk/app-release.apk "$out/UrbanRunner-$version-emulator.apk"
}
if ($targets -contains 'phone' -or $targets -contains 'aab') {
  Export-Unity 'phone'
  if ($targets -contains 'phone') {
    Invoke-Flutter 'flutter_apk_phone' @('build', 'apk', '--release', '--target-platform', 'android-arm,android-arm64')
    Copy-Item app/build/app/outputs/flutter-apk/app-release.apk "$out/UrbanRunner-$version-phone.apk"
  }
  if ($targets -contains 'aab') {
    Invoke-Flutter 'flutter_aab' @('build', 'appbundle', '--release', '--target-platform', 'android-arm,android-arm64')
    Copy-Item app/build/app/outputs/bundle/release/app-release.aab "$out/UrbanRunner-$version.aab"
  }
}
if ($targets -contains 'standalone') {
  Write-Host '== unity standalone emulator apk'
  $a = @('-nographics', '-quit', '-buildTarget', 'Android', '-executeMethod', 'BuildScripts.BuildStandaloneEmulatorApk', '-outputPath', "`"$out/UrbanRunner-$version-standalone-emulator.apk`"")
  if ((Invoke-Unity 'standalone' $a) -ne 0) { throw 'standalone build failed; see build/logs/standalone.log' }
}
Write-Host '==== build_all ===='
Get-ChildItem $out
