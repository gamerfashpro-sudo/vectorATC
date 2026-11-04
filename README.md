# Vector ATC

Локальный авиадиспетчерский симулятор для MSFS 2024 (C# / .NET 8). Цель поставки: один `.exe` без установки.

## Структура

```
VectorAtc.sln
Directory.Build.props              nullable, warnings as errors
src/
  VectorAtc.Core/                  модели (AircraftState, TrafficTarget) и интерфейс ISimDataSource
  VectorAtc.SimLink/               MockSimDataSource + MockScenarios (SimConnect позже)
  VectorAtc.Localization/          ресурсы RU/EN, ILocalizationService
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
| 1b | Разбор запросов и шаблоны фраз | далее |
| 1c | Автомат Ground/Tower, журнал, read-back | |
| 2 | Реальный SimConnect, граф руления | |
| 3 | Речь: PTT, распознавание, синтез, радиоэффект | |
| 4 | Наземное обслуживание (текстовая симуляция, затем интеграция) | |
| 5 | ИИ-трафик | |
| 6 | Карта: свой самолёт и трафик с ICAO авиакомпании, типом, откуда-куда; таблица рейсов | |
| 7 | Объявления в салоне по фазам полёта (снижение, посадка и т.д.), Delivery/Departure/Approach, настройки | |
| 8 | Публикация: single-file exe, лицензии, атрибуция | |

## Проверено / не проверено

- Проверено: 13 тестов (локализация и мок) выполнены через минимальную xunit-совместимую заглушку, все проходят.
- Не проверено: настоящий `dotnet test` с xunit (пакеты NuGet недоступны в среде разработки), сборка под Windows.
- Не создано: проект App (WPF), публикация в один .exe.
