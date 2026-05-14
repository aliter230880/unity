export interface Skill {
  id: string;
  title: string;
  description: string;
  icon: string;
  steps: string[];
  command?: string;
  tag: string;
}

export const SKILLS: Skill[] = [
  {
    id: 'unity-mcp-bridge',
    title: 'Unity MCP Bridge',
    description: 'Установить двунаправленное соединение между AI-агентом и Unity Editor через Docker + Cloudflare tunnel.',
    icon: '🌉',
    tag: 'core',
    steps: [
      '1. Запустить Docker контейнер unity-mcp-server с токеном',
      '2. Запустить cloudflared quick-tunnel (получить *.trycloudflare.com URL)',
      '3. Открыть Unity → Window → AI Game Developer → вставить URL',
      '4. Скопировать Bearer токен и передать агенту',
      '5. Перезапустить сервер с реальным токеном',
      '6. Проверить: плагин зелёный, вызвать scene-list-opened',
    ],
    command: './tools/start_unity_mcp_server.sh <TOKEN>\n./tools/start_tunnel.sh 443',
  },
  {
    id: 'unity-scene-analysis',
    title: 'Scene Analysis',
    description: 'Собрать полный снимок Unity-проекта и сцены через MCP: иерархия, компоненты, ассеты.',
    icon: '🔍',
    tag: 'analysis',
    steps: [
      '1. Вызвать scene-list-opened для получения открытых сцен',
      '2. Вызвать scene-get-data с includeRootGameObjects=true',
      '3. Вызвать assets-find для поиска нужных ассетов',
      '4. Вызвать gameobject-find для поиска конкретных объектов',
      '5. Вызвать gameobject-get-data для детального инспектирования',
    ],
    command: './tools/mcp_call.sh scene-list-opened \'{}\'\n./tools/mcp_call.sh scene-get-data \'{"includeRootGameObjects":true}\'',
  },
  {
    id: 'unity-script-execute',
    title: 'Script Execute (Roslyn)',
    description: 'Выполнять произвольный C# код прямо в Unity Editor через Roslyn — основной "escape hatch" для любых операций.',
    icon: '⚡',
    tag: 'power',
    steps: [
      '1. Подготовить C# код (body-only или полный класс)',
      '2. Вызвать script-execute с isMethodBody=true для простого кода',
      '3. Передать параметры: GameObjectRef, ComponentRef и т.д.',
      '4. Получить результат выполнения',
      '5. Проверить console-get-logs при ошибках',
    ],
    command: './tools/mcp_call.sh script-execute \'{"csharpCode":"Debug.Log(\\"Hello from agent!\\");","isMethodBody":true}\'',
  },
  {
    id: 'img-to-3d-triposr',
    title: 'Image → 3D (TripoSR)',
    description: 'Локальная генерация 3D-меша из 2D-изображения через TripoSR на CPU. Без API-ключей, бесплатно, ~45 сек.',
    icon: '🎲',
    tag: '3d',
    steps: [
      '1. Запустить patch_triposr_cpu.sh (один раз)',
      '2. Подготовить входное изображение PNG/JPG',
      '3. Запустить img_to_3d.sh <input.png> [outdir] [resolution]',
      '4. Получить /0/mesh.glb с vertex-цветами',
      '5. Импортировать в Unity через glb-import-to-unity',
    ],
    command: './tools/patch_triposr_cpu.sh\n./tools/img_to_3d.sh photo.png /tmp/out 256',
  },
  {
    id: 'glb-import-to-unity',
    title: 'GLB Import to Unity',
    description: 'Доставить бинарный ассет (.glb/.png/любой) в Unity Assets/ без прямого файлового доступа к машине.',
    icon: '📦',
    tag: '3d',
    steps: [
      '1. Установить пакет com.unity.cloud.gltfast через package-add',
      '2. Запустить serve_file.sh для раздачи файла',
      '3. Опубликовать порт через cloudflared / expose',
      '4. Выполнить C# через script-execute: UnityWebRequest.Get(url) → AssetDatabase',
      '5. Вызвать assets-refresh для обновления базы',
    ],
    command: './tools/serve_file.sh mesh.glb 8765\n./tools/start_tunnel.sh 8765',
  },
  {
    id: 'mesh-orient-scale',
    title: 'Mesh Orient & Scale',
    description: 'Автоматически развернуть сгенерированный меш "ногами вниз" и нормализовать масштаб после TripoSR.',
    icon: '📐',
    tag: '3d',
    steps: [
      '1. Получить bounds объекта через script-execute + Renderer.bounds',
      '2. Вычислить нужный поворот (Y-up → Z-up конверсия)',
      '3. Нормализовать scale к целевым размерам',
      '4. Установить pivot через probuilder-set-pivot (если ProBuilder)',
      '5. Сохранить сцену через scene-save',
    ],
    command: './tools/mcp_call.sh probuilder-set-pivot \'{"gameObjectRef":{"path":"MyMesh"},"pivotLocation":"Center"}\'',
  },
];
