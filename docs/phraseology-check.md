# Сверка фразеологии

Дата: 2026-11-08. Сверялись английские шаблоны из `Phrases.resx` с открытыми источниками. **Первоисточники ICAO (Doc 4444, Doc 9432, Annex 10) прочитать не удалось**, поэтому ICAO-вариант сверен с британским руководством CAP 413 (оно опирается на стандарты ICAO, но содержит и британские особенности) и вторичным источником (вики IVAO). Русские фразы по российским правилам (ФАП) **не сверялись вообще**: источников у меня нет.

## Источники

- UK CAA, [CAP 413 Radiotelephony Manual, Edition 23 Corr (июнь 2020)](https://www.caa.co.uk/publication/download/17978): главы 2, 4, 8, 11. Прочитан почти весь текст.
- FAA, [JO 7110.65, 3-7 Taxi and ground movement](https://www.faa.gov/air_traffic/publications/ATPubs/ATC/atc0307.html) и [3-9 Departure procedures](https://www.faa.gov/air_traffic/publications/ATPubs/ATC/atc0309.html).
- [IVAO, Aerodrome Phraseology](https://wiki.ivao.aero/en/home/training/documentation/Aerodrome_Phraseology): вторичный источник, общий список ICAO-фраз без привязки к пунктам документа.
- [ICAO Phraseology Reference Guide](https://skybrary.aero/sites/default/files/115.pdf) (краткая памятка, издатель и год не указаны): вторичный источник.
- AIM, глава 4: страница недоступна из среды, не прочитана.

## Результат по фразам

| Фраза (ICAO-вариант) | Было | Итог | Основание |
|---|---|---|---|
| Подтверждение read-back | `readback correct` | **изменено на `{callsign}, correct`** | CAP 413: «BIGJET 347, correct.»; фразы «readback correct» в тексте нет |
| Ответ на MAYDAY | `roger mayday, say intentions` | **изменено на `roger mayday`** | CAP 413, 8.13: «Roger MAYDAY (any pertinent information)»; запроса «say intentions» в руководстве нет |
| Неверный read-back | `negative, I say again` + клиренс повторяется | без изменений, подтверждено | CAP 413: контролёр говорит NEGATIVE и повторяет правильный вариант; «negative I say again» при повторе неверных read-back |
| «Say again» | `say again` | без изменений, подтверждено | CAP 413, таблица 8: «Repeat all, or the following part of your last transmission» |
| Line up and wait | `line up and wait runway 27` | без изменений, подтверждено | CAP 413, 4.32: «line-up and wait Runway 26»; порядок «глагол, потом полоса» |
| Передача на Tower | `contact tower 118.1` | без изменений, подтверждено | CAP 413, 4.19: «contact Tower 118.9»; название подразделения произносится |
| Руление | `taxi to holding point runway 27 via Alpha, Bravo` | оставлено, **неполное** | CAP 413, 4.12: «taxi holding point G2 runway 24 via taxiway Charlie»: в реальной фразе называется обозначение места ожидания (G2), у нас его нет в данных |
| Буксировка | `pushback approved` | оставлено, подтверждено частично | IVAO: «PUSHBACK APPROVED»; CAP 413, 4.11: «stand 27 pushback/powerback approved» (со стоянкой) |
| Проверка связи | `readability five` | оставлено, **неполное** | CAP 413, 2.95: «G-ABCD, Borton Tower, readability 5»: в ответе называется и станция |
| «Unable» | `unable` | оставлено | CAP 413, таблица 8: обычно с причиной («unable cross ... due weight»); у нас без причины |
| Неизвестная станция | `Station calling, say again` | **не проверено** | в прочитанных частях нет |

| Фраза (US-вариант) | Итог | Основание |
|---|---|---|
| `runway 27, taxi via Alpha, Bravo` | подтверждено | FAA 3-7-2: «Runway (number), taxi via (route)» |
| `runway 27, line up and wait` | подтверждено | FAA 3-9-4: «Runway (number), line up and wait» |
| `loud and clear`, `readback correct`, `pushback at your discretion`, `contact tower on ...`, `roger mayday, say intentions` | **не проверено** | AIM недоступен, в 7110.65 (разделы 3-7 и 3-9) этих фраз нет; оставлены как были |

## Что осталось и почему не сделано

1. Обозначение места ожидания в команде руления: нужны данные аэродрома (имя точки), в тестовом аэродроме его нет. Добавлять выдуманное обозначение не буду.
2. Название станции в ответах («Borton Tower, ...»): требует отдельного значения в шаблонах; можно сделать, подтверждено CAP 413.
3. Русские фразы: нужен документ по российской радиотелефонии (ФАП); пока оставлены как есть.
4. ICAO Doc 4444 глава 12 и Doc 9432 напрямую: если у вас есть доступ к PDF, пришлите нужные страницы, тогда сверю первоисточник.
