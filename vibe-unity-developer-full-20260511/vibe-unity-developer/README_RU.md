# Vibe Unity Developer

Простой локальный AI-разработчик для Unity: видит файлы проекта, получает контекст сцены, предлагает изменения и может применять их через Unity Editor.

## Быстрый старт

1. Запусти `start-vibe-dev.cmd`.
2. Откроется `http://localhost:17861`.
3. В браузере укажи AI provider:
   - Base URL: OpenAI-compatible endpoint, например `https://api.openai.com/v1`
   - API key: твой ключ
   - Model: для OpenAI можно оставить `gpt-5.1`, для другого провайдера укажи его модель
4. Установи Unity-плагин:
   - проще всего запусти `install-plugin.cmd`
   - или вручную положи `unity-plugin/VibeUnityDeveloper.cs` в `Assets/Editor/`
5. В Unity открой `Window > Vibe Coding > Fullstack Developer`.
6. Нажми `Sync All Files`.
7. В браузере пиши обычным языком: “создай систему инвентаря”, “найди почему игрок не прыгает”, “добавь дверь в сцену”.

## Как это работает

- Unity-плагин сканирует `Assets`, `Packages`, `ProjectSettings` и корневые файлы проекта.
- Локальный сервер хранит индекс проекта в `data/project-index.json`.
- AI получает карту проекта и релевантные файлы.
- Изменения сначала появляются как proposal.
- Нажимаешь `Apply`, Unity получает команды, делает backup и пишет файлы.

## Безопасность

- Ключи хранятся только локально в `data/settings.json`.
- Сервер слушает только `localhost`.
- Запись разрешена только в `Assets`, `Packages` и `ProjectSettings`.
- Перед изменением файла Unity делает backup в `.vibe-backups`.
- Auto-apply выключен по умолчанию.
