#!/usr/bin/env python3
"""Режет одну дорожку (слова через паузы) на клипы и пишет manifest.json для папки voices/ru/<роль>/.

Использование: split_clips.py дорожка.mp3 список_слов.txt папка_вывода [--noise -35] [--pause 0.5]
Строка списка = один клип. Если число найденных кусков не равно числу строк, скрипт ничего не пишет и сообщает число:
угадывать границы нельзя, иначе слова окажутся под чужими именами.
"""
import json, re, subprocess, sys, pathlib


def main(argv):
    if len(argv) < 4:
        print(__doc__)
        return 2
    track, words_file, out = argv[1], argv[2], pathlib.Path(argv[3])
    noise = argv[argv.index("--noise") + 1] if "--noise" in argv else "-35"
    pause = argv[argv.index("--pause") + 1] if "--pause" in argv else "0.5"
    words = [w.strip() for w in open(words_file, encoding="utf-8") if w.strip()]

    log = subprocess.run(
        ["ffmpeg", "-hide_banner", "-i", track, "-af", f"silencedetect=noise={noise}dB:d={pause}", "-f", "null", "-"],
        capture_output=True, text=True).stderr
    h, m, sec = re.search(r"Duration: (\d+):(\d+):([\d.]+)", log).groups()
    total = int(h) * 3600 + int(m) * 60 + float(sec)
    starts = [float(x) for x in re.findall(r"silence_start: ([\d.]+)", log)]
    ends = [float(x) for x in re.findall(r"silence_end: ([\d.]+)", log)]

    # звук = промежутки между концом одной тишины и началом следующей
    segs, cur = [], 0.0
    for s, e in zip(starts, ends):
        if s - cur > 0.05:
            segs.append((cur, s))
        cur = e
    if total - cur > 0.05:
        segs.append((cur, total))

    if len(segs) != len(words):
        print(f"Найдено кусков: {len(segs)}, ожидалось: {len(words)}. Ничего не записано.")
        print("Попробуй другие значения: --noise -30 или -40, --pause 0.35 или 0.7.")
        return 1

    out.mkdir(parents=True, exist_ok=True)
    phrases = {}
    for i, ((a, b), w) in enumerate(zip(segs, words), 1):
        name = f"c{i:02d}.mp3"
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", track, "-ss", f"{max(a - 0.03, 0):.3f}",
                        "-to", f"{b + 0.08:.3f}", "-c:a", "libmp3lame", "-q:a", "3", str(out / name)], check=True)
        phrases[w] = name
    (out / "manifest.json").write_text(json.dumps({"phrases": phrases}, ensure_ascii=False, indent=1), encoding="utf-8")
    print(f"Готово: {len(phrases)} клипов в {out}")
    return 0


sys.exit(main(sys.argv))
