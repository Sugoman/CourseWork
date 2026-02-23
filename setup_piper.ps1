# ============================================================
# setup_piper.ps1 — Скачивает Piper TTS и голосовые модели
# ============================================================

$ErrorActionPreference = "Stop"

# Путь для LearningTrainer в AppData
$piperRoot = Join-Path $env:LOCALAPPDATA "LearningTrainer\piper"
$voicesDir = Join-Path $piperRoot "voices"

# ── Piper engine ──────────────────────────────────────────────
$piperUrl = "https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip"
$piperExe = Join-Path $piperRoot "piper.exe"

Write-Host "`n=== Piper TTS Setup ===" -ForegroundColor Cyan
Write-Host "Target: $piperRoot"

if (-not (Test-Path $piperExe)) {
    $zipPath = Join-Path $env:TEMP "piper_windows_amd64.zip"

    Write-Host "[1/2] Downloading Piper engine..." -ForegroundColor Yellow
    Invoke-WebRequest -Uri $piperUrl -OutFile $zipPath -UseBasicParsing

    Write-Host "      Extracting..." -ForegroundColor Yellow
    $tempExtract = Join-Path $env:TEMP "piper_extract"
    if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
    Expand-Archive -Path $zipPath -DestinationPath $tempExtract -Force

    $extractedDir = Join-Path $tempExtract "piper"
    if (Test-Path $piperRoot) { Remove-Item $piperRoot -Recurse -Force }
    New-Item -ItemType Directory -Path (Split-Path $piperRoot) -Force | Out-Null
    Move-Item -Path $extractedDir -Destination $piperRoot -Force

    Remove-Item $zipPath -ErrorAction SilentlyContinue
    Remove-Item $tempExtract -Recurse -ErrorAction SilentlyContinue
    Write-Host "      Piper installed!" -ForegroundColor Green
} else {
    Write-Host "[1/2] Piper already installed." -ForegroundColor Green
}

# ── Voice models ──────────────────────────────────────────────
$hfBase = "https://huggingface.co/rhasspy/piper-voices/resolve/v1.0.0"
$voices = @(
    @{ Name = "en_US-lessac-medium";   Path = "en/en_US/lessac/medium"   },
    @{ Name = "ru_RU-irina-medium";    Path = "ru/ru_RU/irina/medium"    }
)

if (-not (Test-Path $voicesDir)) { New-Item -ItemType Directory -Path $voicesDir -Force | Out-Null }

Write-Host "`n[2/2] Downloading voice models..." -ForegroundColor Yellow

foreach ($v in $voices) {
    $onnxFile = Join-Path $voicesDir "$($v.Name).onnx"
    $jsonFile = Join-Path $voicesDir "$($v.Name).onnx.json"

    if (-not (Test-Path $onnxFile)) {
        Write-Host "      $($v.Name) ..." -ForegroundColor Yellow -NoNewline
        Invoke-WebRequest -Uri "$hfBase/$($v.Path)/$($v.Name).onnx"      -OutFile $onnxFile -UseBasicParsing
        Invoke-WebRequest -Uri "$hfBase/$($v.Path)/$($v.Name).onnx.json" -OutFile $jsonFile -UseBasicParsing
        Write-Host " OK" -ForegroundColor Green
    } else {
        Write-Host "      $($v.Name) — already installed." -ForegroundColor Green
    }
}

Write-Host "`n=== Setup complete! ===" -ForegroundColor Cyan
Write-Host "Piper:  $piperExe"
Write-Host "Voices: $voicesDir"
Write-Host "`nNow build and run the app — TTS will work automatically." -ForegroundColor Green