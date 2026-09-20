FORMA — Unity package / Reference Realism Update
Identity Lab / procedural avatar studio

ОБНОВЛЕНИЕ
Стартовые женский и мужской аватары заново настроены по приложенным
референсам. Повышена плотность сетки тела и лица, улучшены пропорции,
кожа, микрорельеф, освещение, волосы и стартовые материалы.

REALISTIC GLB EDITION
Сцена FORMA_Studio теперь использует две приложенные rigged GLB-модели:
FemaleReference.glb и MaleReference.glb. Старые процедурные заглушки
автоматически отключаются. Кнопки «Женский» и «Мужской» переключают
реальные модели. При первом импорте редактор автоматически устанавливает
официальный пакет Unity glTFast (com.unity.cloud.gltfast); дождитесь
завершения Package Manager и затем откройте сцену повторно.

Важно: модели содержат скелет, материалы, текстуры и анимацию, но не
содержат morph targets/blend shapes. Поэтому слайдеры геометрии FORMA
для этих двух моделей отключены по смыслу; переключение моделей, камера,
свет, материалы и исходная анимация работают.

ЧТО ЭТО
Порт веб-студии FORMA (процедурное тело, лицо, волосы, одежда)
в полностью редактируемые C#-скрипты Unity.

КАК ОТКРЫТЬ
1. Unity 2021.3 LTS / 2022.3 / 2023 / Unity 6.
   Built-in RP, URP и HDRP — шейдер FORMA/LitVertexColor работает везде.
2. Assets → Import Package → Custom Package… → FORMA.unitypackage
3. Откройте сцену Assets/FORMA/Scenes/FORMA_Studio.unity
   (или меню FORMA → Open Studio Scene)
4. Нажмите Play.

В ДЕФОЛТНОЙ СЦЕНЕ УЖЕ ЕСТЬ
  FORMA Studio     — FormaStudio + AvatarGenerator
  FORMA Camera     — Main Camera + OrbitCamera
  FORMA Key/Fill/Rim — студийный свет
  FORMA Floor      — подиум
  RenderSettings   — туман и ambient как в студии

МОЖНО РЕДАКТИРОВАТЬ
Assets/FORMA/Scripts/
  AvatarTypes.cs      — параметры аватара (слайдеры, enum'ы)
  AvatarDefaults.cs   — женский / мужской дефолт, палитры
  AvatarCatalog.cs    — вкладки и подписи панели
  AvatarPresets.cs    — готовые образы
  MeshBuilder.cs      — tube / ellipsoid / loft
  BodyBuilder.cs      — построение тела и морфы
  HairBuilder.cs      — пряди и борода
  GarmentBuilder.cs   — одежда
  TextureFactory.cs   — радужка, веснушки
  AvatarGenerator.cs  — сборка мешей, материалы, глаза
  FormaStudio.cs      — сцена, свет, IMGUI-панель
  OrbitCamera.cs      — орбита камеры
Assets/FORMA/Shaders/LitVertexColor.shader
Assets/FORMA/Resources/FORMA/  — референс-фото для кожи и волос
Assets/FORMA/Scenes/FORMA_Studio.unity
Assets/FORMA/Editor/FormaMenu.cs

Все меши строятся в рантайме. Нет FBX — меняйте формулы в C# и жмите Play.

УПРАВЛЕНИЕ
ЛКМ — орбита, колесо — зум.
Слева — категории ТЕЛО / ЛИЦО / КОЖА / ВОЛОСЫ / ОДЕЖДА / ОБРАЗ.
Экспорт PNG сохраняет скрин в папку проекта (forma-avatar.png).

Inspector: компонент FormaStudio на объекте «FORMA Studio».
Можно править AvatarParams прямо в инспекторе (в Play).

LOD SYSTEM
Оба GLB-аватара получают LODGroup после завершения glTFast-импорта:
LOD0 100%, LOD1 60%, LOD2 30%, LOD3 12%, дальний порог 3.5%.
На дальних уровнях автоматически отключаются волосы, одежда, украшения,
ресницы и прочие мелкие части, сохраняя тело и лицо читаемыми.
