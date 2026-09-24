# TOOLS.md - Local Notes

Skills define _how_ tools work. This file is for _your_ specifics - the stuff that's unique to your setup.

## What Goes Here

Things like:

- Camera names and locations
- SSH hosts and aliases
- Preferred voices for TTS
- Speaker/room names
- Device nicknames
- Anything environment-specific

## Examples

```markdown
### Cameras

- living-room - Main area, 180 deg wide angle
- front-door - Entrance, motion-triggered

### SSH

- home-server - 192.168.1.100, user: admin

### TTS

- Preferred voice: "Nova" (warm, slightly British)
- Default speaker: Kitchen HomePod
```

## Why Separate?

Skills are shared. Your setup is yours. Keeping them apart means you can update skills without losing your notes, and share skills without leaking your infrastructure.

---

Add whatever helps you do your job. This is your cheat sheet.

## Related

- [Agent workspace](/concepts/agent-workspace)

---

# Unity: проект MAC_avatars

## Мост к редактору (AIBridge)

Unity открыт с проектом; мост живой, если `AI/state/status.txt` обновляется (там же
`scene`, `playing`, `compiling`).

- Команда — файл `AI/inbox/<id>.json`:
  `{"id":"<id>","op":"<op>","args":["k=v",...]}`.
  Мост берёт по одному файлу, удаляет вход и пишет `AI/outbox/<id>.txt` (заголовок + тело).
  Писать **атомарно** (temp + rename) и не оставлять больше одной команды в inbox.
- Операции: `status`, `hierarchy` (arg `depth`), `find`, `inspect`, `create`, `delete`,
  `rename`, `reparent`, `transform`, `add_component`, `remove_component`, `set_property`,
  `set_color`, `select`, `save_scene`, `open_scene`, `new_scene`, `list_assets`,
  `instantiate`, `create_prefab`, `screenshot`, `play`, `stop`, `pause`, `refresh`, `menu`,
  `clear_console`, `run_method`, `help`.
  Адрес объекта — `path=/Parent/Child` (от корня сцены; уникальное имя можно коротко).
  `inspect` показывает компоненты и их поля; у GameObject — `pos/rot/scale/children`.
- `play` (через `EditorApplication.delayCall`) **не срабатывает**. Вход/выход:
  `run_method` + `type=UnityEditor.EditorApplication` + `method=EnterPlaymode` / `ExitPlaymode`.
  Play часто стартует **на паузе** — снимать операцией `pause`.
- `screenshot` рендерит через `Camera.Render` и **не видит ScreenSpace-Overlay** — а панель
  MAC именно overlay. Для снимков использовать хелпер ниже.
- Редактор не всегда подхватывает изменённый .cs: после правки послать `refresh`
  или `menu item=Assets/Refresh`, затем дождаться записи в `AI/state/compile.log`.
- `run_method` вызывает **только статические** методы и только с 0 или 1 аргументом (string).
  Приватные — можно (флаг NonPublic).

## Хелпер проверки MAC_AI_Shot (`Assets/Editor/MAC_AI_Shot.cs`)

Вызов: `run_method` + `type=MAC_AI_Shot` + `method=<метод>` + `arg=<значение>`.

| Метод | Что делает |
|---|---|
| `Shot` / `ShotBig` | снимок Game View (видит overlay-UI); `arg=<абсолютный путь>`, ShotBig — ×3 |
| `Labels` | дамп всех подписей TMP + счётчики slider / toggle / button |
| `Goto` | `arg=<tab>:<section>` — переключить вкладку/секцию без мыши |
| `ClickLabel` | `arg=<точный текст кнопки>` — нажать видимую кнопку по подписи |
| `Probe` | внутреннее состояние панели: `_tab`, `_sec`, `_ctrls`, `_secPages`, `_railBtns` |
| `RowInfo` | ширины подписи / значения / дорожки в первой видимой строке + геометрия бегунка |
| `CamInfo` | позиция камеры, rect, фокус, `_dist`, `_distTarget`, `_orbitDir` |
| `SnapCam` | эмулирует «кликовый» перескок камеры нативным `UIManager` (проверка, что мы перебиваем) |
| `ZoomTo` | `arg=<метры>` — прогон зум-конвейера (то же поле, куда пишет колесо) |
| `Canvases` | режим/порядок/активность всех канвасов |

Типовые прогоны:

```powershell
& $aib -Op run_method -Args 'type=MAC_AI_Shot','method=Goto','arg=3:0'
& $aib -Op run_method -Args 'type=MAC_AI_Shot','method=ClickLabel','arg=Short 01'
& $aib -Op run_method -Args 'type=MAC_AI_Shot','method=RowInfo'
& $aib -Op run_method -Args 'type=MAC_AI_Shot','method=CamInfo'
```

## Мой враппер команд

PowerShell: `C:\Users\USER\.openclaw-autoclaw\agents\auto-coder\workspace\.openclaw\tmp\aib.ps1`
Вызов — по **абсолютному** пути (относительный `.ps1` из exec не находится):

```powershell
& 'C:\Users\USER\.openclaw-autoclaw\agents\auto-coder\workspace\.openclaw\tmp\aib.ps1' -Op status
& '...\aib.ps1' -Op run_method -Args 'type=MAC_AI_Shot','method=Goto','arg=2:0'
```

## Бэкапы проекта

Перед любой правкой копировать в `D:\Work\Unity\Projects\MAC_avatars\AI\backup-<дата>-<тема>\`
(сцена + версии скрипта `.pre-<шаг>`). Схема уже используется в проекте.
Текущий набор: `AI\backup-20260916-messfix\` — сцена, MACCustomizationUI.cs.bak,
`.pre-redesign`, `.pre-visual`, `.pre-visual2`, `.pre-visual3`, `.pre-bugfix`, `.pre-camera`.

## Кодировки и вывод в консоль

- Проверено 16.09.2026: `CONTEXT.md` и `MACCustomizationUI.cs` — **UTF-8** (в .cs кириллица
  только в комментариях). Не верить «кракозябрам» в консоли PowerShell — она портит кириллицу
  на выводе, а не в файле. Проверять байты: `0x98` не существует в cp1251 — это маркер UTF-8.
- Python запускать с `PYTHONIOENCODING=utf-8`, иначе падает на cp1251 при печати.
- Патчить файлы байт-в-байт (round-trip `.decode('latin-1')` → `.encode('latin-1')`) —
  безопасно для любой кодировки, если якоря ASCII. Многострочные якоря нормализовать по CRLF.
- Запись файлов проекта — через `[System.IO.File]::WriteAllText` с `UTF8Encoding($false)`
  (без BOM); `Set-Content -Encoding UTF8` в PowerShell 5.1 добавляет BOM.
- Правило, которое стоило мне испорченного файла: **перед перекодировкой целого файла делать
  копию**. `AI/backup-20260913-111919/CONTEXT.md` спас ситуацию.
