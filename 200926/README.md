# FORMA + MAC — материалы для веб-ИИ (GitHub)

Распакованное содержимое unitypackage `FORMA_Editable_Realistic_NO_GLTFast_v10.unitypackage`
в его **текущем исправленном** виде + минимальный скелет Unity-проекта + текст ошибок консоли.

## Структура

```
для ВЭБ_ИИ/
├── Assets/
│   ├── FORMA/                  ← сам пакет FORMA v10 (со всеми .meta, GUID сохранены)
│   │   ├── Scripts/            ← 15 скриптов (см. ниже) — ГЛАВНОЕ для ревью
│   │   │   ├── Backends/       ← архитектура IAvatarBackend (4 новых файла)
│   │   │   ├── FormaStudio.cs          — панель FORMA (IMGUI), роутинг бэкендов
│   │   │   ├── MacAvatarAdapter.cs     — MAC-бэкенд: биндинг-таблица, Capture, слепки
│   │   │   ├── ReferenceAvatarLoader.cs / ReferenceAvatarCustomizer.cs — GLB/OBJ путь
│   │   │   └── ...                      — генератор/билдеры процедурного аватара
│   │   ├── Editor/             ← FormaGlbAnimBaker (пекарь контроллеров),
│   │   │                          FormaBindingWindow (калибровка), FormaMenu, TextureFix
│   │   ├── Scenes/FORMA_Studio.unity   — сцена (YAML; ссылки: MAC Avatar Adapter +
│   │   │                                   префабы MaleAvatarDefault/FemaleAvatarDefault)
│   │   ├── Resources/FORMA/    ← OBJ-референсы + текстуры + Bases/*.asset
│   │   ├── Sources/            ← MaleAnimated.glb / FemaleAnimated.glb (БИНАРЬ ~23МБ,
│   │   │                          ригованные персонажи RigModels + анимации;
│   │   │                          для проверки компиляции НЕ нужны, можно не грузить)
│   │   ├── Materials/, Shaders/
│   ├── MAC_Scripts/            ← Assets/Magic Avatar Creator/Scripts из SDK MAC
│   │                              (нужно для компиляции: namespace MagicAvatarCreator:
│   │                              MagicAvatarManager, AvatarObject, AvatarData,
│   │                              AvatarMaterialsManager, ClothElement, ...)
│   ├── MAC_Prefabs_Avatars/    ← 4 префаба-основ MAC (YAML; их GUID-ы стоят в сцене;
│   │                              ссылаются на FBX-меши, которых здесь НЕТ —
│   │                              на компиляцию не влияет, будут missing mesh)
│   └── Editor/                 ← MAC_Forma_Probe.cs / MAC_Forma_Setup.cs —
│                                  ВАШИ файлы, сейчас с ошибками компиляции (см. errors.txt)
├── Packages/manifest.json      ← зависимости: URP 17.3.0 + com.unity.cloud.gltfast 6.10.0
├── ProjectSettings/ProjectVersion.txt ← Unity 6000.3.9f1
└── UnityConsole_errors.txt     ← реальные ошибки консоли + история + диагноз
```

## Ключевая архитектура (что уже сделано, не ломайте)

- `IAvatarBackend` (Apply/Capture/SetSex/Hide) — единый контракт источника аватаров.
- `MacAvatarAdapter` — MAC-бэкенд на таблице-данных `FormaMacBinding` (ScriptableObject:
  paramId + shapeNames + AnimationCurve response + templateLadder; зеркальные _L/_R
  пары пишутся синхронно; шаблоны Ears_T1..T7 интерполируются лестницей; кейворды — фолбэк).
  Плюс Capture() (аватар→параметры) и DumpShapes/ApplyDump (100% round-trip образов).
- `MacContentCatalog` — таблицы «стиль FORMA → CC-префаб» для волос/одежды.
- `FormaStudio` — MAC-режим по умолчанию, тумблер «Аватары: MAC», образы с macShapeDump.
- Известные грабли unitypackage: НЕ включайте в пакет файлы из
  `Assets/Magic Avatar Creator/**` (однажды это перезаписало AvatarData.cs кривым
  diff-патчем — каскад CS0246 по всему SDK; детали в errors.txt).

## Что просится в ваш PR

Исправить два своих файла (`Assets/Editor/MAC_Forma_Probe.cs`, `MAC_Forma_Setup.cs`) —
точные ошибки и диагноз в `UnityConsole_errors.txt`. Больше в проекте ошибок компиляции нет
(проверено Roslyn по всем скриптам, кроме этих двух).
