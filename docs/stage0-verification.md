# Этап 0: сверка допущений по первоисточникам

Дата проверки: 4 ноября 2026. Статусы: **подтверждено** (прочитано в источнике), **частично**, **не подтверждено** (источника не нашли или не смогли открыть), **опровергнуто**.
Юридические выводы ниже не являются юридической консультацией.

## Сводка

| # | Вопрос | Статус | Что делать |
|---|--------|--------|------------|
| 1 | Управляемая обёртка SimConnect под .NET 8 | частично | Проверить загрузку на ПК с MSFS; запасной путь: P/Invoke к SimConnect.dll |
| 2 | Рулёжные дорожки через Facilities API | подтверждено по документации | Запустить тест на реальных аэропортах |
| 3 | Создание ИИ-бортов в MSFS 2024 | частично | Использовать EX1-версии; прежние помечены как legacy |
| 4 | Ливреи и авиакомпании ИИ | частично | EX1 принимает имя или папку ливреи; авиакомпании на странице EX1 не упомянуты |
| 5 | Состояние PTT в SimVar | не подтверждено | PTT ловим сами глобальной клавишей, от симулятора не зависим |
| 6 | Ветер и QNH | не проверялось | Проверить при реализации этапа 2 |
| 7–9 | Интерфейс GSX | частично | Нужна документация из папки установки GSX (см. ниже) |
| 10 | Совместимость GSX с MSFS 2024 | не подтверждено | Источник закрыт для чтения |
| 11 | Whisper.net или Vosk для русского | не проверялось | Только практический тест на реальных фразах |
| 12 | Piper: голоса и лицензии | опровергнуто частично | Лицензия у каждого голоса своя; русских голосов найден один |
| 13 | Лицензии и ADS-B | частично | См. раздел ниже |
| 14 | Номера разделов ICAO и FAA | FAA подтверждено, ICAO не подтверждено | В приложении номера разделов не цитируем |

## 1. Управляемая обёртка SimConnect

Документация SDK (раздел про управляемый код) называет сборку `Microsoft.FlightSimulator.SimConnect`, лежит в `SimConnect SDK\lib\managed`, при установке SDK попадает в GAC, отдельная копия `SimConnect.dll` поставляется специально для этого. Требования в тексте: **.NET Framework 4.7** и Visual Studio 2017 или новее. Про .NET 8 и про x64/x86 страница не говорит.
Вывод: совместимость с .NET 8 **не подтверждена**. Нужен короткий тест на машине с SDK. Запасной вариант: P/Invoke к нативной `SimConnect.dll`.
Источник: [Programming SimConnect clients using managed code](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/programming-simconnect-clients-using-managed-code/index.md).

## 2. Рулёжные дорожки

Страница `SimConnect_AddToFacilityDefinition` для MSFS 2024 перечисляет структуры аэропорта:
- `TAXI_POINT`: `TYPE`, `ORIENTATION`, `BIAS_X`, `BIAS_Z`
- `TAXI_PATH`: `TYPE`, `WIDTH`, `RUNWAY_NUMBER`, `RUNWAY_DESIGNATOR`, `START`, `END`, `NAME_INDEX` и др.
- `TAXI_NAME`: `NAME`
- `TAXI_PARKING`: `TYPE`, `NAME`, `SUFFIX`, `NUMBER`, `HEADING`, `RADIUS`, `BIAS_X`, `BIAS_Z`, `AIRLINE` (список авиакомпаний), `N_AIRLINES`
- `RUNWAY`: координаты, курс, длина, порог, номера концов и др.

Это достаточно, чтобы собрать граф: точки, рёбра с названиями дорожек, стоянки с привязкой к авиакомпаниям.
Не подтверждено: у точек нет собственных широты и долготы, только смещения `BIAS_X/BIAS_Z`; от чего именно отсчитываются смещения, в прочитанном тексте не сказано (предположительно от опорной точки аэропорта). Полнота данных на реальных аэропортах не проверялась.
Источник: [AddToFacilityDefinition (MSFS 2024)](https://docs.flightsimulator.com/msfs2024/flighting/programming-apis/simconnect/api-reference/facilities/simconnect_addtofacilitydefinition/index.md).

## 3–4. ИИ-трафик

- `SimConnect_AICreateEnrouteATCAircraft`: на странице MSFS 2024 помечена как legacy, «only works with non-modular aircraft»; для 2024 рекомендуется `..._EX1` (для legacy и modular). Параметры EX1 для этой функции не прочитаны.
- `SimConnect_AICreateNonATCAircraft`: описание страницы называет функцию legacy, «non-modular aircraft only», и советует EX1.
- `SimConnect_AICreateNonATCAircraft_EX1`: параметры `szContainerTitle`, `szLivery`, `szTailNumber`, `InitPos`, `RequestID`. Для modular-самолётов ливрея задаётся именем из `livery.cfg` или именем папки; сначала проверяется папка, затем имя; иначе берётся ливрея по умолчанию. Авиакомпании на странице не упомянуты.
- Список установленных самолётов и ливрей: `SimConnect_EnumerateSimObjectsAndLiveries` (упомянута на странице Enroute).
- Известная проблема: в бета-версии SU4 вызов `AICreateEnrouteATCAircraft` для самолётов в воздухе падал с `CREATE_OBJECT_FAILED`. Участники темы пишут, что исправлено в сборках 1.6.11 и 1.6.13; Asobo подтвердила баг, но тема без дат и не официальное заявление о версии. Там же сказано, что хаотичное руление ИИ к стоянкам и полосам признано проблемой и стоит в бэклоге.

Вывод: утверждение «функции не устарели» верно лишь отчасти. Функции есть, но помечены legacy и работают не со всеми самолётами. Слой ИИ строим на EX1, прежние вызовы только как запасной вариант.
Источники: [Enroute](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/ai-object/simconnect_aicreateenrouteatcaircraft/index.md), [NonATC](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/ai-object/simconnect_aicreatenonatcaircraft/), [NonATC EX1](https://docs.flightsimulator.com/msfs2024/retail/programming-apis/simconnect/api-reference/ai-object/simconnect_aicreatenonatcaircraft_ex1/index.md), [тема об ошибке в SU4](https://devsupport.flightsimulator.com/t/msfs2024-su4-beta-aicreateenrouteatcaircraft-no-longer-injects-aircraft-in-the-air/16661).

## 5–6. PTT и погода

По PTT и по переменным погоды источников не открывали. Решение по PTT не зависит от ответа: клавишу отслеживаем сами.

## 7–10. GSX

Что подтверждено:
- Сообщение на форуме Microsoft, цитирующее пользователя Umberto67 (принадлежность к команде FSDreamTeam страницей не подтверждена): «GSX Pro Change: Remote API updates for third-party developers», удалённые клиенты теперь могут и *записывать* те же свойства, что обработчик-скрипт, и «Both features are fully documented in the included Remote API guide». [Пост 3729](https://forums.flightsimulator.com/t/fsdreamteam-gsx-for-msfs/287195/3729).
- Релиз 3.3.5 от 8 февраля 2025: добавлена папка SDK с документацией «GSX Remote Control (C++ sample)», расположение `Addon Manager\couatl\GSX`. [Пост 2483](https://forums.flightsimulator.com/t/fsdreamteam-gsx-for-msfs/287195/2483).

Вывод: официальный интерфейс удалённого управления существует, документация поставляется вместе с GSX. Но механизм доступа (L-переменные, файлы, сокет) в прочитанных источниках не назван, а пример на C++.

Не подтверждено: конкретные имена переменных вроде `FSDT_GSX_GATE_SELECTED`, `FSDT_GSX_NUMPASSENGERS_BOARDING`, список читаемых состояний, совместимость «с версии 3.2.8». Форум и страница продукта FSDreamTeam закрыты для автоматического чтения. Страница интеграции FlyByWire не называет ни переменных, ни механизма.

Что нужно от вас: папка `Addon Manager\couatl\GSX` из вашей установки GSX (или текст руководства Remote API). Без него адаптер GSX не пишем. Пока работает встроенная текстовая симуляция наземного обслуживания.

## 11. Распознавание речи

- Whisper.net: лицензия MIT (по карточке проекта на ecosyste.ms). Лицензия весов Whisper не проверялась.
- Vosk: русская модель `vosk-model-ru` помечена `license: apache-2.0` (по заголовку результата поиска; страницу целиком не читали).
- Качество на русском и задержка на CPU не проверялись: нужен тест на реальных фразах.

## 12. Piper (синтез речи)

- Репозиторий Piper (зеркало `piper1-gpl`): **GPL-3.0**. README про лицензии голосов и про использование из других программ ничего не говорит. [Зеркало](https://gitee.com/vhow/piper1-gpl).
- Голоса: лицензия **у каждого голоса своя**, берётся из его MODEL_CARD; репозиторий-зеркало своей лицензии не добавляет. Утверждение «голоса все MIT» неверно. В списке зеркала русский голос один, `denis` (средний, один диктор), лицензия CC0. Источник не официальный. [Список голосов](https://huggingface.co/mijuanlo/piper-voices).
- Следствие: русских голосов для диспетчера, наземной службы и ИИ-пилотов может не хватить; запасной вариант: системные голоса Windows.
- Про GPL: запуск `piper.exe` отдельным процессом считается распространённой практикой, но если мы кладём `piper.exe` внутрь поставки, нужно соблюсти условия GPL для самого Piper (текст лицензии и исходный код). Проверить до публикации.

## 13. Лицензии и ADS-B

- adsb.lol, страница API для фидеров: лицензия **ODbL 1.0** (CC0 на странице не упомянута); доступ только с IP станции фидера и по ключу фидера, лимит на «всемирный» снимок: дважды в минуту. Требований к указанию источника на странице нет. [Страница](https://www.adsb.lol/docs/feeders-only/re-api/).
- Публичный API `api.adsb.lol`: README указывает лицензию BSD-3-Clause (по всей видимости, на код), лимиты динамические, в будущем будет нужен ключ, который выдают фидерам. Условия использования данных не найдены. [README](https://github.com/adsblol/api).
- Вывод: функцию ADS-B оставляем опциональной и выключенной по умолчанию; перед включением прочитать `api.adsb.lol/docs` и учесть возможный будущий ключ. Указание источника данных в интерфейсе добавляем в любом случае.

## 14. Фразеология

FAA JO 7110.65, подтверждено:
- Глава 3, раздел 7 «Taxi and Ground Movement Procedures»: 3-7-1 Ground Traffic Movement, **3-7-2 Taxi and Ground Movement Operations** (маршрут, формулировки, пересечение ВПП и hold short), 3-7-3 Ground Operations, **3-7-4 Runway Proximity**. Пункт про «progressive taxi» в заголовках не найден. [Раздел 7](https://www.faa.gov/air_traffic/publications/ATpubs/ATC/atc0307.html).
- Глава 3, раздел 9 «Departure Procedures and Separation»: **3-9-4 Line Up and Wait (LUAW)**, 3-9-7 Wake Turbulence Separation for Intersection Departures, **3-9-10 Takeoff Clearance**, 3-9-11 Cancellation of Takeoff Clearance. [Раздел 9](https://www.faa.gov/air_traffic/publications//atpubs/atc_html/chap3_section_9.html).

ICAO Doc 4444: первоисточник не открыт, номера разделов `12.3.4.2` и `12.3.4.7` из вашего списка **не подтверждены**. В приложении номера разделов не цитируем, формулировки шаблонов пишем сами.

## Что это меняет в плане

1. Этап 2 (SimConnect и граф руления): сначала короткий тест на вашем ПК. Он выгружает данные `TAXI_*` для нескольких аэропортов, по нему решаем, хватает ли данных. Заодно проверяет загрузку управляемой сборки под .NET 8.
2. Слой ИИ-трафика: вызовы EX1, раскраски по имени или папке, авиакомпании по полю `AIRLINE` у стоянок.
3. GSX: ждём документацию из вашей установки; до этого только встроенная симуляция.
4. Речь: проверить русские голоса и качество распознавания практическим тестом.
5. Публикация: решить вопрос лицензий (Piper, голоса, ADS-B) до выкладки.
