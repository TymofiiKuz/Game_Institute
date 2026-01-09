# Event Generator CLI

Небольшая консольная утилита, которая отправляет запрос в GPT (OpenAI совместимый API) или Google Gemini и получает готовые события в формате `GameEvent`.

## Быстрый старт

1. Установите .NET 8 SDK (https://dotnet.microsoft.com/download).
2. В корне проекта выполните:
   ```bash
   dotnet restore Tools/EventGenerator/EventGenerator.csproj
   ```
3. Задайте API ключ:
   * GPT: `export OPENAI_API_KEY="..."` (Linux/Mac) или `setx OPENAI_API_KEY "..."`
   * Google: `export GOOGLE_API_KEY="..."`.
4. Запустите генерацию:
   ```bash
   dotnet run --project Tools/EventGenerator -- --provider gpt --count 5 --output Assets/StreamingAssets/events.generated.json
   ```

По умолчанию утилита использует модель `gpt-4o-mini`. Для Google — `gemini-1.5-flash`. Измените модель опцией `--model`.

## Основные опции

| Опция | Описание |
|-------|----------|
| `--provider gpt|google` | Выбор API (по умолчанию `gpt`). |
| `--count N` | Сколько событий запросить. |
| `--prompt "..."` | Дополнительное творческое направление (например, тематика). |
| `--systemPrompt "..."` | Полная замена системного промпта (для продвинутой настройки). |
| `--apiKey KEY` | Передать ключ напрямую (если не хотите использовать переменные окружения). |
| `--output PATH` | Куда сохранить результат (по умолчанию `events.generated.json`). |
| `--dryRun` | Только показать сформированный запрос без отправки на сервер. |

## Формат ответа

Модель должна вернуть JSON-массив объектов `GameEvent`. Структуры совпадают с кодом из `Assets/Scripts/GameEvent.cs`:

```json
[
  {
    "id": 101,
    "description": "Пример события...",
    "scope": "Local",
    "isBadEvent": false,
    "options": [
      {
        "text": "Опция 1",
        "sanityRequired": 5,
        "moneyRequired": 10,
        "artifactsRequired": 0,
        "sanityChange": 0,
        "moneyChange": -10,
        "artifactsChange": 0,
        "influenceChange": 2,
        "stabilityChange": -1,
        "developmentChange": 0,
        "minInfluenceRequired": 3,
        "minDevelopmentRequired": 0,
        "modifiers": []
      }
    ]
  }
]
```

## Подключение результата в игру

* Скопируйте сгенерированный файл в `Assets/StreamingAssets/events.json` (или объедините данные вручную).
* Игра автоматически загрузит файл при старте через `EventManager`.
* Следите за уникальностью `id` и правдоподобностью чисел.

## Советы

1. Используйте `--prompt` для ограничения тем или задания определённого стиля (`--prompt "в стиле постапокалипсиса"`).
2. Перед заливкой в основной файл прогоните `--dryRun`, чтобы увидеть запрос и при необходимости подправить шаблон.
3. При генерации глобальных событий (`scope: "Global"`) проверяйте, что изменения небольшие, чтобы не ломать баланс.
