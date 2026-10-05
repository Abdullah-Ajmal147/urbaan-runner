# Runs every automated test layer that is available; exits non-zero on any failure. Mirrors tools/test_all.sh.
#   pwsh tools/test_all.ps1 [-SkipUnity] [-SkipFlutter] [-SkipFirebase]
param([switch]$SkipUnity, [switch]$SkipFlutter, [switch]$SkipFirebase)
. "$PSScriptRoot/env.ps1"
$ErrorActionPreference = 'Continue'
Set-Location $ROOT
$results = [ordered]@{}

function Test-UnityXml([string]$xml) {
  if (-not (Test-Path $xml)) { return $false }
  $run = ([xml](Get-Content $xml -Raw))['test-run']
  return ($run.failed -eq '0' -and $run.total -ne '0')
}

if (-not $SkipUnity) {
  if (-not $UNITY) { $results['unity'] = 'MISSING' } else {
    Remove-Item build/test-results/editmode.xml, build/test-results/playmode.xml -ErrorAction SilentlyContinue
    Invoke-Unity 'editmode' @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', "`"$ROOT/build/test-results/editmode.xml`"") | Out-Null
    $results['unity editmode'] = if (Test-UnityXml 'build/test-results/editmode.xml') { 'PASS' } else { 'FAIL' }
    Invoke-Unity 'playmode' @('-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$ROOT/build/test-results/playmode.xml`"") | Out-Null
    $results['unity playmode'] = if (Test-UnityXml 'build/test-results/playmode.xml') { 'PASS' } else { 'FAIL' }
  }
}

if (-not $SkipFlutter) {
  Push-Location app
  flutter pub get *> $null
  flutter analyze *> "$ROOT/build/logs/flutter_analyze.log"
  $results['flutter analyze'] = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
  flutter test --reporter expanded *> "$ROOT/build/logs/flutter_test.log"
  $results['flutter test'] = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
  Pop-Location
}

if (-not $SkipFirebase -and (Test-Path firebase/package.json)) {
  Push-Location firebase
  npm ci --silent *> $null
  firebase emulators:exec --project demo-urban-runner --only firestore,auth "npm test" *> "$ROOT/build/logs/firebase_test.log"
  $results['firebase rules'] = if ($LASTEXITCODE -eq 0) { 'PASS' } else { 'FAIL' }
  Pop-Location
}

Write-Host '==== test_all ===='
$results.GetEnumerator() | ForEach-Object { Write-Host "$($_.Key): $($_.Value)" }
if ($results.Values -contains 'FAIL' -or $results.Values -contains 'MISSING') { Write-Host 'FAILURES (see build/logs)'; exit 1 }
Write-Host 'ALL GREEN'; exit 0
