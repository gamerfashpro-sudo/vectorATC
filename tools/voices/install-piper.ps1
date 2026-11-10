# Скачивает Piper (офлайн-синтез речи) и три русских голоса в %LOCALAPPDATA%\VectorATC\piper.
# Запуск: правый клик по файлу -> "Выполнить с помощью PowerShell". Нужен интернет только на время скачивания.
# Голоса скачиваются на ваш компьютер из публичного каталога Piper и в программу не вшиваются: лицензия данных, на которых
# обучены голоса, у части из них не подтверждена (Ruslan: CC BY-NC-SA, только некоммерческое использование; Irina: "Unknown").
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:LOCALAPPDATA 'VectorATC'
$piper = Join-Path $root 'piper'
$voices = Join-Path $piper 'voices'
New-Item -ItemType Directory -Force -Path $voices | Out-Null

$zip = Join-Path $env:TEMP 'piper_windows_amd64.zip'
if (-not (Test-Path (Join-Path $piper 'piper.exe'))) {
    Write-Host 'Скачиваю Piper...'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/rhasspy/piper/releases/download/2023.11.14-2/piper_windows_amd64.zip' -OutFile $zip
    Expand-Archive -Force -Path $zip -DestinationPath $root   # архив содержит папку piper/
    Remove-Item $zip -Force
}

$base = 'https://huggingface.co/rhasspy/piper-voices/resolve/main/ru/ru_RU'
$models = @(
    @{ Name = 'denis';  File = 'ru_RU-denis-medium'  },   # диспетчер
    @{ Name = 'irina';  File = 'ru_RU-irina-medium'  },   # бортпроводница
    @{ Name = 'dmitri'; File = 'ru_RU-dmitri-medium' }    # пилот
)
foreach ($m in $models) {
    foreach ($ext in @('.onnx', '.onnx.json')) {
        $target = Join-Path $voices ($m.File + $ext)
        if (Test-Path $target) { continue }
        $url = "$base/$($m.Name)/medium/$($m.File)$ext"
        Write-Host "Скачиваю $($m.File)$ext ..."
        Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $target
    }
}

# Распознавание речи Whisper (модель small, около 470 МБ; лицензия модели MIT)
$whisper = Join-Path $root 'whisper'
New-Item -ItemType Directory -Force -Path $whisper | Out-Null
$wm = Join-Path $whisper 'ggml-small.bin'
if (-not (Test-Path $wm)) {
    Write-Host 'Скачиваю модель распознавания речи (около 470 МБ)...'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin' -OutFile $wm
}

# пробная фраза: если слышно голос, всё работает
$test = Join-Path $env:TEMP 'vatc-test.wav'
'Проверка связи, как слышно?' | & (Join-Path $piper 'piper.exe') --model (Join-Path $voices 'ru_RU-denis-medium.onnx') --output_file $test --quiet
(New-Object Media.SoundPlayer $test).PlaySync()
Write-Host 'Готово. Запустите Vector ATC.'
