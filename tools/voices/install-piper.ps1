# Скачивает Piper (офлайн-синтез речи), три русских голоса и модель распознавания речи Whisper в %LOCALAPPDATA%\VectorATC.
# Запуск: правый клик по файлу -> "Выполнить с помощью PowerShell". Окно НЕ закрывается само: в конце ждёт Enter.
# Весь вывод пишется в файл %LOCALAPPDATA%\VectorATC\install-log.txt: его можно прислать, если что-то не скачалось.
# Файлы скачиваются на ваш компьютер из публичных каталогов и в программу не вшиваются: лицензия данных, на которых
# обучены голоса, у части из них не подтверждена (Ruslan: CC BY-NC-SA, только некоммерческое использование; Irina: "Unknown").
$ProgressPreference = 'SilentlyContinue'   # без этого Invoke-WebRequest в Windows PowerShell 5.1 качает во много раз медленнее
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$root = Join-Path $env:LOCALAPPDATA 'VectorATC'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$log = Join-Path $root 'install-log.txt'
Start-Transcript -Path $log -Force | Out-Null
$failed = @()

function Get-File($url, $target, $minBytes) {
    if ((Test-Path $target) -and ((Get-Item $target).Length -ge $minBytes)) { Write-Host "Уже есть: $target"; return }
    Write-Host "Скачиваю: $url"
    try {
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $target
        $size = (Get-Item $target).Length
        if ($size -lt $minBytes) { throw "файл слишком маленький: $size байт" }
        Write-Host "  OK, $size байт"
    } catch {
        Write-Host "  ОШИБКА: $($_.Exception.Message)" -ForegroundColor Red
        if (Test-Path $target) { Remove-Item $target -Force }
        $script:failed += $url
    }
}

$piper = Join-Path $root 'piper'
$voices = Join-Path $piper 'voices'
New-Item -ItemType Directory -Force -Path $voices | Out-Null

if (-not (Test-Path (Join-Path $piper 'piper.exe'))) {
    $zip = Join-Path $env:TEMP 'piper_windows_amd64.zip'
    Get-File 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' $zip 1000000
    if (Test-Path $zip) {
        try { Expand-Archive -Force -Path $zip -DestinationPath $root; Remove-Item $zip -Force }
        catch { Write-Host "  ОШИБКА распаковки: $($_.Exception.Message)" -ForegroundColor Red; $failed += 'распаковка piper' }
    }
}

$base = 'https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU'
foreach ($m in @(@('denis', 'ru_RU-denis-medium'), @('irina', 'ru_RU-irina-medium'), @('dmitri', 'ru_RU-dmitri-medium'))) {
    Get-File "$base/$($m[0])/medium/$($m[1]).onnx.json" (Join-Path $voices "$($m[1]).onnx.json") 1000
    Get-File "$base/$($m[0])/medium/$($m[1]).onnx" (Join-Path $voices "$($m[1]).onnx") 10000000
}

# Распознавание речи Whisper (модель small, около 470 МБ; лицензия модели MIT)
$whisper = Join-Path $root 'whisper'
New-Item -ItemType Directory -Force -Path $whisper | Out-Null
Get-File 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin' (Join-Path $whisper 'ggml-small.bin') 400000000

Write-Host ''
Write-Host '--- Итог ---'
foreach ($f in @((Join-Path $piper 'piper.exe'), (Join-Path $voices 'ru_RU-denis-medium.onnx'), (Join-Path $voices 'ru_RU-irina-medium.onnx'),
        (Join-Path $voices 'ru_RU-dmitri-medium.onnx'), (Join-Path $whisper 'ggml-small.bin'))) {
    Write-Host ("{0}: {1}" -f $f, $(if (Test-Path $f) { 'есть' } else { 'НЕТ' }))
}
if ($failed.Count -gt 0) {
    Write-Host 'Не скачано:' -ForegroundColor Red
    $failed | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} elseif (Test-Path (Join-Path $piper 'piper.exe')) {
    try {
        $test = Join-Path $env:TEMP 'vatc-test.wav'
        'Проверка связи, как слышно?' | & (Join-Path $piper 'piper.exe') --model (Join-Path $voices 'ru_RU-denis-medium.onnx') --output_file $test --quiet
        (New-Object Media.SoundPlayer $test).PlaySync()
        Write-Host 'Готово. Запустите Vector ATC.'
    } catch { Write-Host "Пробная фраза не прозвучала: $($_.Exception.Message)" -ForegroundColor Red }
}
Write-Host "Подробный журнал: $log"
Stop-Transcript | Out-Null
Read-Host 'Нажмите Enter, чтобы закрыть окно'
