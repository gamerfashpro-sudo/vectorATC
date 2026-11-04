# Vector ATC

Локальный авиадиспетчерский симулятор для MSFS 2024 (C# / .NET 8). Цель поставки: один `.exe` без установки.

## Структура

```
VectorAtc.sln
Directory.Build.props              nullable, warnings as errors
src/
  VectorAtc.Core/                  модели, ISimDataSource, разбор запросов (Parsing), интерфейс шаблонов фраз (Phraseology)
  VectorAtc.SimLink/               MockSimDataSource + MockScenarios (SimConnect позже)
  VectorAtc.Localization/          ресурсы RU/EN (Strings, Phrases), ILocalizationService, PhraseBook
  VectorAtc.Speech/ Ground/ Traffic/   заготовки
tests/
  VectorAtc.Tests/                 xunit
```

## Запуск (Windows, .NET 8 SDK)

```
dotnet build
dotnet test
```

## План

| Этап | Что | Статус |
|------|-----|--------|
| 1a-1 | Каркас, локализация RU/EN | готово |
| 1a-2 | ISimDataSource, мок, модели самолёта и трафика | готово |
| 1b | Разбор запросов пилота (позывной, намерение) и шаблоны фраз RU/EN, ICAO/US | готово |
| 1c | Автомат Ground/Tower, журнал, read-back | далее |
| 2 | Реальный SimConnect, граф руления | |
| 3 | Речь: PTT, распознавание, синтез, радиоэффект | |
| 4 | Наземное обслуживание (текстовая симуляция, затем интеграция) | |
| 5 | ИИ-трафик | |
| 6 | Карта: свой самолёт и трафик с ICAO авиакомпании, типом, откуда-куда; таблица рейсов | |
| 7 | Объявления в салоне по фазам полёта (снижение, посадка и т.д.), Delivery/Departure/Approach, настройки | |
| 8 | Публикация: single-file exe, лицензии, атрибуция | |

## Проверено / не проверено

- Проверено: 33 теста (локализация, мок, разбор запросов, шаблоны фраз) выполнены через минимальную xunit-совместимую заглушку, все проходят. Заглушка падает на заведомо неверном тесте.
- Не проверено: формулировки шаблонов (особенно русские и US-вариант) не сверены с ICAO Doc 4444, FAA JO 7110.65 и российскими правилами. Это этап 0.
- Не проверено: настоящий `dotnet test` с xunit (пакеты NuGet недоступны в среде разработки), сборка под Windows.
- Не создано: проект App (WPF), публикация в один .exe.
