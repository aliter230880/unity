using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MagicAvatarCreator
{
    [ExecuteAlways]
    public class HairPhysicsManager : MonoBehaviour
    {
        struct InterpData
        {
            public int guideA;
            public int guideB;
            public float weight;
        }

        [Header("References")]
        public ComputeShader hairCompute;
        public ComputeShader sdfCompute;
        public ComputeShader interpolationCS;
        public Transform hairRootsParent;
        public Transform[] rootMarkers;
        public Transform headBone;
        
        [Header("Sphere Colliders")]
        public Transform[] sphereColliders;      // центры в мировых координатах
        public float[] sphereRadii;              // радиусы (метры)
        private ComputeBuffer sphereCentersBuffer;
        private ComputeBuffer sphereRadiiBuffer;
        
        public Material strandMaterial;
        public int targetSegmentsPerStrand = 32;
        [Range(1, 20000)] public int visibleHairCount = 2000;
        private ComputeBuffer interpolationBuffer;
        private ComputeBuffer finalParticleBuffer;
        private GraphicsBuffer indexBuffer;
        private ComputeBuffer rootNormalsBuffer;
        private ComputeBuffer atlasIndicesBuffer;
        private ComputeBuffer guideLengthsBuffer;
        private ComputeBuffer rootDirectionsBuffer;
        private ComputeBuffer localHeadOffsetsBuffer;
        private ComputeBuffer guideAtlasIndicesBuffer;
        private MaterialPropertyBlock propBlock;
        private int interpolateKernel;
        private int totalFinalParticles;
        private float[] guideLengths;
        private Vector3[] rootDirections;
        private Vector3[] restSegmentDirs;
        private Vector3[] localHeadOffsets;
        private int[] guideAtlasIndices;
        
        [Header("Texture Atlas")]
        public int atlasColumns = 2;
        public int atlasRows = 1;
        

        [Header("Hair Parameters")] 
        [Range(0.001f, 0.5f)]
        public float hairLength = 0.3f;
        [Range(3, 30)] public int segmentsPerHair = 12;
        [Header("Volume Retention")]
        [Range(0f, 1f)] 
        public float strandDirectionStrength = 0.8f;
        [Range(-1f, 1f)] 
        public float tipDirectionStrength = 0.2f;
        [Header("Collision")]
        [Range(0f, 1f)] public float restitution = 0.05f; // упругость (0 – полное гашение)
        [Range(0f, 1f)] public float friction = 0.3f;      // трение
        [Range(0.001f, 0.2f)] public float particleRadius = 0.02f; // радиус частицы (для коллизий)
        [Range(0.8f, 0.999f)] public float damping = 0.95f; // затухание скорости (важно!)
        [Range(0.01f, 5f)] public float stiffness = 0.5f; // жёсткость ограничений расстояний
        [Range(1, 16)] public int constraintIterations = 4; // итераций солвера (чем больше, тем жёстче)
        [Range(0f, 1f)] public float tangentFriction = 0.95f; // Трение скольжения (не используется в текущей версии)

        [Header("Gravity")] 
        public Vector3 gravity = new Vector3(0, -9.81f, 0);

        [Header("SDF Settings")] 
        public GameObject sourceGameObject;
        public Mesh sourceMesh;
        public int sdfResolution = 16;
        
        [Header("SDF Bounds")]
        public Vector3 boundsCenter = Vector3.zero;
        public Vector3 boundsSize = new Vector3(0.3f, 0.3f, 0.3f);

        [Header("Debug")] public bool debugDraw = false; // disabled by default; prevents guide particles/gizmos in the user view
        [Header("Runtime presentation")] public bool hideDebugObjectsAtRuntime = true;
        public Color debugColor = Color.green;
        public float guideSphereRadius = 0.008f;
        public float rootSphereRadius = 0.008f;
        
        // GPU buffers
        private ComputeBuffer particleBuffer; // текущие и предыдущие позиции
        private ComputeBuffer rootBuffer; // позиции корней (обновляются каждый кадр)
        private ComputeBuffer paramsBuffer; // параметры каждого волоса
        private ComputeBuffer surfaceNormalBuffer; // нормали поверхности для каждого сегмента

        [SerializeField] 
        private Texture3D sdfTexture;
        private Vector3 sdfBoundsSizeLocal;
        private Vector3 sdfBoundsCenterLocal;
        private int kernelIndex;
        private int generateSDFKernelIndex;
        private int hairCount;
        private int totalParticles;

        struct Particle
        {
            public Vector3 position;
            public Vector3 previousPosition;
            public float isInside;
            public float arcLength;
        }

        struct HairParams
        {
            public float segmentLength;
            public int segmentsCount;
            public float radius;
            public float stiffness;
            public float damping;
            public float strandStrength;
            public float tipStrength;
            public float curlStrength;
            public float curlTurns;
            public float curlDirection; 
        }

        // Буфер для нормалей поверхности (3 float на частицу = 12 байт)
        private Vector3[] surfaceNormals;

        // Кэш для отладки
        private Vector3[] debugPositions;
        private float lastDebugTime;

        private void OnValidate()
        {
            Init();
        }

        private void ReleaseBuffers()
        {
            particleBuffer?.Release(); particleBuffer = null;
            rootBuffer?.Release(); rootBuffer = null;
            paramsBuffer?.Release(); paramsBuffer = null;
            interpolationBuffer?.Release(); interpolationBuffer = null;
            finalParticleBuffer?.Release(); finalParticleBuffer = null;
            indexBuffer?.Dispose(); indexBuffer = null;
            rootNormalsBuffer?.Release(); rootNormalsBuffer = null;
            atlasIndicesBuffer?.Release(); atlasIndicesBuffer = null;
            rootDirectionsBuffer?.Release(); rootDirectionsBuffer = null;
            sphereCentersBuffer?.Release(); sphereCentersBuffer = null;
            sphereRadiiBuffer?.Release(); sphereRadiiBuffer = null;
            localHeadOffsetsBuffer?.Release(); localHeadOffsetsBuffer = null;
            guideAtlasIndicesBuffer?.Release(); guideAtlasIndicesBuffer = null;
            guideLengthsBuffer?.Release(); guideLengthsBuffer = null;
        }

        public void Init()
        {
            ReleaseBuffers(); // prevent GPU memory leak on re-init

            if (rootMarkers.Length == 0)
            {
                return;
            }

            sourceMesh = sourceGameObject.GetComponent<SkinnedMeshRenderer>()?.sharedMesh;
            if (sourceMesh == null)
            {
                Debug.LogError("[HairPhysicsManager] sourceMesh is null — SkinnedMeshRenderer not found on " + sourceGameObject.name);
                return;
            }
            
            if (sphereColliders != null && sphereColliders.Length > 0)
            {
                sphereCentersBuffer = new ComputeBuffer(sphereColliders.Length, sizeof(float) * 3);
                sphereRadiiBuffer = new ComputeBuffer(sphereColliders.Length, sizeof(float));
            }

            hairCount = rootMarkers.Length;
            segmentsPerHair = Mathf.Max(2, segmentsPerHair);
            totalParticles = hairCount * segmentsPerHair;
            
            if (strandMaterial == null) return;
            propBlock = new MaterialPropertyBlock();

            kernelIndex = hairCompute.FindKernel("CSMain");
            generateSDFKernelIndex = sdfCompute.FindKernel("CSMain");

            // Проверка валидности kernel
            if (kernelIndex < 0)
            {
                Debug.LogError("CSMain kernel not found in HairSimulation.compute shader!");
                return;
            }

            if (generateSDFKernelIndex < 0)
            {
                Debug.LogError("GenerateSDF kernel not found in HairSimulation.compute shader!");
                return;
            }

            // Буфер частиц (3+3 float'а = 24 байта на частицу)
            particleBuffer = new ComputeBuffer(totalParticles, sizeof(float) * 8);
            // Буфер корней (3 float)
            rootBuffer = new ComputeBuffer(hairCount, sizeof(float) * 3);
            // Буфер параметров волоса: segmentLength(float), segmentsCount(int), radius(float), stiffness(float), damping(float) = 5*4=20 байт
            paramsBuffer = new ComputeBuffer(hairCount, sizeof(float) * 10);
            
            guideLengths = new float[hairCount];
            guideLengthsBuffer = new ComputeBuffer(hairCount, sizeof(float));
            for (var i = 0; i < hairCount; i++)
            {
                var guide = rootMarkers[i].GetComponent<HairGuide>();
                guideLengths[i] = (guide != null && guide.customLength > 0) ? guide.customLength : hairLength;
            }
            guideLengthsBuffer.SetData(guideLengths);
            
            guideAtlasIndices = new int[hairCount];
            for (int i = 0; i < hairCount; i++)
            {
                HairGuide guide = rootMarkers[i].GetComponent<HairGuide>();
                guideAtlasIndices[i] = guide != null ? Mathf.Clamp(guide.atlasIndex, 0, atlasColumns - 1) : 0;
            }
            guideAtlasIndicesBuffer = new ComputeBuffer(hairCount, sizeof(int));
            guideAtlasIndicesBuffer.SetData(guideAtlasIndices);
            
            rootDirections = new Vector3[hairCount];
            for (int i = 0; i < hairCount; i++)
            {
                HairGuide guide = rootMarkers[i].GetComponent<HairGuide>();
                Vector3 worldDir = guide != null ? guide.transform.up : rootMarkers[i].up;
                rootDirections[i] = worldDir;
            }
            
            rootDirectionsBuffer = new ComputeBuffer(hairCount, sizeof(float) * 3);
            rootDirectionsBuffer.SetData(rootDirections);

            var particles = new List<Particle>();
            
            for (var h = 0; h < hairCount; h++)
            {
                var rootTransform = rootMarkers[h];
                var rootPos = rootTransform.position;
                var guide = rootTransform.GetComponent<HairGuide>();
                var growthDirWorld = rootTransform.up;;
                var hairLen = guideLengths[h];
                var segLen = hairLen / (segmentsPerHair - 1);

                for (var s = 0; s < segmentsPerHair; s++)
                {
                    var t = (float)s / (segmentsPerHair - 1);
                    var pos = rootPos + growthDirWorld * (t * hairLen);
                    particles.Add(new Particle { position = pos, previousPosition = pos });
                }
            }
            
            particleBuffer.SetData(particles.ToArray());
            
            Transform headTransform = sourceGameObject.transform; // или любая кость, к которой привязаны волосы
            localHeadOffsets = new Vector3[totalParticles];
            Particle[] particlesArray = new Particle[totalParticles];
            particleBuffer.GetData(particlesArray);

            for (int i = 0; i < totalParticles; i++)
            {
                Vector3 worldPos = particlesArray[i].position;
                Vector3 localPos = headTransform.InverseTransformPoint(worldPos);
                localHeadOffsets[i] = localPos;
            }
            localHeadOffsetsBuffer = new ComputeBuffer(totalParticles, sizeof(float) * 3);
            localHeadOffsetsBuffer.SetData(localHeadOffsets);

            var hairParamsArray = new HairParams[hairCount];
            
            for (var i = 0; i < hairCount; i++)
            {
                var guide = rootMarkers[i].GetComponent<HairGuide>();
                hairParamsArray[i] = new HairParams
                {
                    segmentLength = guideLengths[i] / (segmentsPerHair - 1),
                    segmentsCount = segmentsPerHair,
                    radius = particleRadius,
                    stiffness = stiffness,
                    damping = damping,
                    strandStrength = guide.strandDirectionStrength,
                    tipStrength = guide.tipDirectionStrength,
                    curlStrength = guide.curlStrength,
                    curlTurns = guide.curlTurns,
                    curlDirection = guide.curlDirection
                };
            }

            paramsBuffer.SetData(hairParamsArray);
            
            UpdateLocalBounds();

            hairCompute.SetBuffer(kernelIndex, "_Particles", particleBuffer);
            hairCompute.SetBuffer(kernelIndex, "_RootPositions", rootBuffer);
            hairCompute.SetBuffer(kernelIndex, "_HairParams", paramsBuffer);
            hairCompute.SetBuffer(kernelIndex, "_GuideLengths", guideLengthsBuffer);
            hairCompute.SetBuffer(kernelIndex, "_RootDirections", rootDirectionsBuffer);
            hairCompute.SetBuffer(kernelIndex, "_LocalHeadOffsets", localHeadOffsetsBuffer);
            hairCompute.SetFloat("_Restitution", restitution);
            hairCompute.SetFloat("_Friction", friction);
            hairCompute.SetFloat("_TangentFriction", tangentFriction);
            hairCompute.SetInt("_ConstraintIterations", constraintIterations);
            hairCompute.SetVector("_Gravity", gravity);
            hairCompute.SetTexture(kernelIndex, "_SDFTexture", sdfTexture);
            hairCompute.SetInt("_Resolution", sdfResolution);
            
            GenerateInterpolationData();
        }

        void GenerateInterpolationData()
        {
            // Получаем позиции корней направляющих (в мировых координатах)
            Vector3[] guideRoots = new Vector3[hairCount];
            for (int i = 0; i < hairCount; i++)
            {
                guideRoots[i] = rootMarkers[i].position;
            }

            InterpData[] interpArray = new InterpData[visibleHairCount];
            for (int i = 0; i < visibleHairCount; i++)
            {
                int guideA = Random.Range(0, hairCount);
                // Ищем ближайший корень к guideA
                int guideB = guideA;
                float minDist = 0.5f;
                for (int j = 0; j < hairCount; j++)
                {
                    if (j == guideA) continue;
                    float dist = Vector3.Distance(guideRoots[guideA], guideRoots[j]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        guideB = j;
                    }
                }

                interpArray[i].guideA = guideA;
                interpArray[i].guideB = guideB;
                interpArray[i].weight = Random.value;
            }

            interpolationBuffer = new ComputeBuffer(visibleHairCount, sizeof(int) * 2 + sizeof(float));
            interpolationBuffer.SetData(interpArray);

            totalFinalParticles = visibleHairCount * targetSegmentsPerStrand;
            finalParticleBuffer = new ComputeBuffer(totalFinalParticles, sizeof(float) * 8);
            rootNormalsBuffer = new ComputeBuffer(visibleHairCount, sizeof(float) * 3);

            atlasIndicesBuffer = new ComputeBuffer(visibleHairCount, sizeof(int));

            // 3. Находим kernel в compute шейдере интерполяции (допустим, он называется HairInterpolation)
            if (interpolationCS != null)
            {
                interpolateKernel = interpolationCS.FindKernel("InterpolateHair");
                interpolationCS.SetBuffer(interpolateKernel, "_GuideParticles", particleBuffer);
                interpolationCS.SetBuffer(interpolateKernel, "_InterpolationData", interpolationBuffer);
                interpolationCS.SetBuffer(interpolateKernel, "_FinalParticles", finalParticleBuffer);
                // interpolationCS.SetBuffer(interpolateKernel, "_RootNormals", rootNormalsBuffer);
                interpolationCS.SetBuffer(interpolateKernel, "_GuideAtlasIndices", guideAtlasIndicesBuffer);
                interpolationCS.SetBuffer(interpolateKernel, "_VisibleAtlasIndices", atlasIndicesBuffer);
                interpolationCS.SetInt("_HairCount", hairCount);
                interpolationCS.SetInt("_GuideSegments", segmentsPerHair);
                interpolationCS.SetInt("_TargetSegments", targetSegmentsPerStrand);
                interpolationCS.SetInt("_VisibleHairCount", visibleHairCount);
            }

            BuildIndexBuffer();
        }

        private void BuildIndexBuffer()
        {
            if (indexBuffer != null) indexBuffer.Dispose();

            int verticesPerHair = targetSegmentsPerStrand * 2; // каждая точка волоса даёт две вершины (левая и правая)
            int segmentsCount = targetSegmentsPerStrand - 1;   // количество прямоугольников между точками
            if (segmentsCount <= 0) return;

            int indicesPerHair = segmentsCount * 6;            // 2 треугольника = 6 индексов на сегмент
            int totalIndices = visibleHairCount * indicesPerHair;

            int[] indices = new int[totalIndices];
            for (int h = 0; h < visibleHairCount; h++)
            {
                int baseVertex = h * verticesPerHair;
                int baseIndex = h * indicesPerHair;
                for (int seg = 0; seg < segmentsCount; seg++)
                {
                    int left0 = baseVertex + seg * 2;
                    int right0 = left0 + 1;
                    int left1 = baseVertex + (seg + 1) * 2;
                    int right1 = left1 + 1;

                    // Треугольник 1
                    indices[baseIndex + seg * 6 + 0] = left0;
                    indices[baseIndex + seg * 6 + 1] = left1;
                    indices[baseIndex + seg * 6 + 2] = right0;
                    // Треугольник 2
                    indices[baseIndex + seg * 6 + 3] = right0;
                    indices[baseIndex + seg * 6 + 4] = left1;
                    indices[baseIndex + seg * 6 + 5] = right1;
                }
            }

            indexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Index, totalIndices, sizeof(int));
            indexBuffer.SetData(indices);
        }
        
        void UpdateLocalBounds()
        {
            // Мировой центр куба = позиция кости + смещение
            Vector3 worldCenter = headBone.position + boundsCenter;
            // Переводим в локальные координаты этого объекта (HairPhysicsManager)
            sdfBoundsCenterLocal = headBone.InverseTransformPoint(worldCenter);
    
            // Пересчёт размера из мирового в локальный с учётом масштаба кости
            Vector3 scale = headBone.lossyScale;
            sdfBoundsSizeLocal = new Vector3(
                boundsSize.x / scale.x,
                boundsSize.y / scale.y,
                boundsSize.z / scale.z
            );
        }

        void Update()
        {
            if (hideDebugObjectsAtRuntime && Application.isPlaying)
                debugDraw = false;

            if (!hairCompute || kernelIndex < 0 || rootMarkers.Length == 0)
                return;

            // 1. Обновление корневых позиций (всегда актуально)
            UpdateRootPositions();

            // 2. Общие параметры симуляции
            SetSimulationParams();

            // 3. Настройка SDF (только если текстура и шейдер доступны)
            UpdateSDFSettings();

            // 4. Запуск симуляции
            int threadGroups = Mathf.CeilToInt(hairCount / 64.0f);
            hairCompute.Dispatch(kernelIndex, threadGroups, 1, 1);

            // 5. Отладка (по таймеру)
            UpdateDebugCache();

            // 6. Интерполяция и рендеринг лент
            
        }

        private void LateUpdate()
        {
            InterpolateAndRender();
        }

        private void UpdateRootPositions()
        {
            Vector3[] roots = new Vector3[hairCount];
            
            for (int i = 0; i < hairCount; i++)
            {
                roots[i] = rootMarkers[i].position;
            }

            rootBuffer.SetData(roots);
        }
        
        private void UpdateSphereBuffers()
        {
            if (sphereColliders == null || sphereColliders.Length == 0) return;
            Vector3[] centers = new Vector3[sphereColliders.Length];
            for (int i = 0; i < sphereColliders.Length; i++)
            {
                centers[i] = sphereColliders[i].position;
            }
            sphereCentersBuffer.SetData(centers);
            sphereRadiiBuffer.SetData(sphereRadii);
            hairCompute.SetBuffer(kernelIndex, "_SphereCenters", sphereCentersBuffer);
            hairCompute.SetBuffer(kernelIndex, "_SphereRadii", sphereRadiiBuffer);
            hairCompute.SetInt("_SphereCount", sphereColliders.Length);
        }

        private void SetSimulationParams()
        {
            UpdateSphereBuffers();
            
            hairCompute.SetFloat("_DeltaTime", Time.fixedDeltaTime);
            hairCompute.SetFloat("_Restitution", restitution);
            hairCompute.SetFloat("_Friction", friction);
            hairCompute.SetInt("_ConstraintIterations", constraintIterations);
            hairCompute.SetFloat("_TangentFriction", tangentFriction);
            hairCompute.SetMatrix("_HeadLocalToWorld", sourceGameObject.transform.localToWorldMatrix);
            hairCompute.SetBuffer(kernelIndex, "_RootPositions", rootBuffer);
            hairCompute.SetBuffer(kernelIndex, "_HairParams", paramsBuffer);
        }

        private void UpdateSDFSettings()
        {
            if (sdfTexture == null) return;

            hairCompute.SetTexture(kernelIndex, "_SDFTexture", sdfTexture);

            // Пересчёт границ каждый кадр (т.к. HairPhysicsManager может двигаться)
            Vector3 minBoundsLocal = sdfBoundsCenterLocal - sdfBoundsSizeLocal * 0.5f;
            Vector3 maxBoundsLocal = sdfBoundsCenterLocal + sdfBoundsSizeLocal * 0.5f;

            // Передаём границы в локальных координатах объекта HairPhysicsManager
            hairCompute.SetVector("_BoundsMin", minBoundsLocal);
            hairCompute.SetVector("_BoundsMax", maxBoundsLocal);

            // Вычисляем максимальный размер для нормализации
            Vector3 boundsSizeVec = maxBoundsLocal - minBoundsLocal;
            float maxDim = Mathf.Max(boundsSizeVec.x, boundsSizeVec.y, boundsSizeVec.z);
            hairCompute.SetFloat("_MaxDim", maxDim);

            // Матрицы преобразования (также могут меняться)
            Matrix4x4 worldToLocal = headBone.worldToLocalMatrix;
            Matrix4x4 localToWorld = headBone.localToWorldMatrix;
            hairCompute.SetMatrix("_WorldToLocal", worldToLocal);
            hairCompute.SetMatrix("_LocalToWorld", localToWorld);

            // Прокидываем SDF данные и в интерполяцию
            if (interpolationCS != null && interpolateKernel >= 0)
            {
                interpolationCS.SetTexture(interpolateKernel, "_SDFTexture", sdfTexture);
                interpolationCS.SetVector("_BoundsMin", minBoundsLocal);
                interpolationCS.SetVector("_BoundsMax", maxBoundsLocal);
                interpolationCS.SetInt("_Resolution", sdfResolution);
                interpolationCS.SetFloat("_MaxDim", maxDim);
                interpolationCS.SetMatrix("_WorldToLocal", worldToLocal);
                interpolationCS.SetMatrix("_LocalToWorld", localToWorld);
                interpolationCS.SetFloat("_ParticleRadius", particleRadius);
            }
        }

        private void UpdateDebugCache()
        {
            if (!debugDraw || Time.time - lastDebugTime <= 0.01f) 
                return;

            Particle[] parts = new Particle[totalParticles];
            particleBuffer.GetData(parts);
            debugPositions = new Vector3[totalParticles];
            for (int i = 0; i < totalParticles; i++)
                debugPositions[i] = parts[i].position;
            lastDebugTime = Time.time;
        }

        private void InterpolateAndRender()
        {
            // 1. Интерполяция
            if (interpolationCS != null && interpolateKernel >= 0 && finalParticleBuffer != null)
            {
                int threadGroups = Mathf.CeilToInt(totalFinalParticles / 64.0f);
                interpolationCS.Dispatch(interpolateKernel, threadGroups, 1, 1);
            }

            // 2. Рендеринг лент (стрендов)
            if (strandMaterial == null || finalParticleBuffer == null) return;

            if (propBlock == null) propBlock = new MaterialPropertyBlock();
            
            propBlock.SetBuffer("_ParticleBuffer", finalParticleBuffer);
            propBlock.SetInt("_SegmentsPerHair", targetSegmentsPerStrand);
            propBlock.SetInt("_VisibleHairCount", visibleHairCount);
            propBlock.SetBuffer("_RootNormals", rootNormalsBuffer);
            propBlock.SetBuffer("_AtlasIndices", atlasIndicesBuffer);
            propBlock.SetInt("_AtlasColumns", atlasColumns);

            int totalIndices = visibleHairCount * (targetSegmentsPerStrand - 1) * 6;

            Graphics.DrawProcedural(
                strandMaterial,
                new Bounds(Vector3.zero, Vector3.one * 1000f),
                MeshTopology.Triangles,
                indexBuffer,
                totalIndices,
                1,
                null,
                propBlock,
                ShadowCastingMode.On,
                false,
                0
            );
        }

        void OnDrawGizmos()
        {
            if (!debugDraw || debugPositions == null) return;
            Gizmos.color = debugColor;
            for (int h = 0; h < hairCount; h++)
            {
                int start = h * segmentsPerHair;
                for (int s = 0; s < segmentsPerHair - 1; s++)
                {
                    Gizmos.DrawLine(debugPositions[start + s], debugPositions[start + s + 1]);
                    if (guideSphereRadius > 0)
                    {
                        Gizmos.DrawWireSphere(debugPositions[start + s], guideSphereRadius);
                        Gizmos.DrawWireSphere(debugPositions[start + s + 1], guideSphereRadius);
                    }
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (!debugDraw) return;
            Vector3 worldCenter = headBone.position + boundsCenter;
    
            Gizmos.color = new Color(1f, 0.65f, 0f, 0.3f);
            Gizmos.DrawWireCube(worldCenter, boundsSize);
            Gizmos.color = new Color(1f, 0.65f, 0f, 0.15f);
            Gizmos.DrawCube(worldCenter, boundsSize);
        }
        
        [ContextMenu("Set Bounds to Transform Position")]
        void SetBoundsToTransformPosition()
        {
            boundsCenter = Vector3.zero; // локальный центр = позиция объекта
            // Если нужен размер по умолчанию
            boundsSize = new Vector3(0.3f, 0.3f, 0.3f);
            Debug.Log($"Bounds centered at {headBone.position} (local zero)");
        }
        
        [ContextMenu("Debug SDF")]
        [ContextMenu("Check SDF When Head Rotates")]
        void DebugSDFRotation()
        {
            if (sdfTexture == null || sourceGameObject == null)
            {
                Debug.Log("SDF texture or source GameObject is null");
                return;
            }
            
            Transform headTransform = sourceGameObject.transform;
            Vector3 center = boundsCenter;
            Vector3 minBounds = boundsCenter - boundsSize * 0.5f;
            Vector3 maxBounds = boundsCenter + boundsSize * 0.5f;
            
            Debug.Log("=== SDF Debug: Testing Head Rotation ===");
            
            // Текущая позиция
            float originalSDF = SampleSDFInternal(center, minBounds, maxBounds);
            Debug.Log($"Original rotation - Center SDF: {originalSDF:F4}");
            
            // Поворачиваем голову на 90 градусов
            Quaternion originalRotation = headTransform.rotation;
            headTransform.rotation = originalRotation * Quaternion.Euler(0, 90, 0);
            
            // Ждём один кадр обновления
            float rotatedSDF = SampleSDFInternal(center, minBounds, maxBounds);
            Debug.Log($"Rotated 90° - Center SDF: {rotatedSDF:F4}");
            
            // Возвращаем обратно
            headTransform.rotation = originalRotation;
            
            // Проверяем снова
            float finalSDF = SampleSDFInternal(center, minBounds, maxBounds);
            Debug.Log($"After return - Center SDF: {finalSDF:F4}");
        }
        
        [ContextMenu("Debug Bounds Alignment")]
        void DebugBoundsAlignment()
        {
            if (sourceGameObject == null) return;
            
            var skinnedRenderer = sourceGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer == null) return;
            
            // Получаем bounds в мировых координатах
            Bounds meshBounds = skinnedRenderer.localBounds;
            Vector3 meshCenterWorld = skinnedRenderer.transform.TransformPoint(meshBounds.center);
            
            // Наш bounds в мировых координатах
            Vector3 ourCenterWorld = headBone.TransformPoint(boundsCenter);
            Vector3 minBoundsWorld = headBone.TransformPoint(boundsCenter - boundsSize * 0.5f);
            Vector3 maxBoundsWorld = headBone.TransformPoint(boundsCenter + boundsSize * 0.5f);
            
            Debug.Log("=== Bounds Alignment Debug ===");
            Debug.Log($"Mesh center (world): {meshCenterWorld}");
            Debug.Log($"Our bounds center (world): {ourCenterWorld}");
            Debug.Log($"Our bounds min (world): {minBoundsWorld}");
            Debug.Log($"Our bounds max (world): {maxBoundsWorld}");
            Debug.Log($"Mesh bounds size: {meshBounds.size}");
            Debug.Log($"Our bounds size: {boundsSize}");
            Debug.Log($"Distance between centers: {Vector3.Distance(meshCenterWorld, ourCenterWorld)}");
        }
        
        [ContextMenu("Auto-set SDF Bounds from Mesh (Head Only)")]
        void AutoSetBoundsHead()
        {
            var smr = sourceGameObject.GetComponent<SkinnedMeshRenderer>();
            if (smr == null) return;
    
            Bounds meshBounds = smr.localBounds;
            // Примерная голова: 45% от общего размера, центр в верхней части
            float totalHeight = meshBounds.size.y;
            Vector3 headCenterInMesh = new Vector3(
                meshBounds.center.x,
                meshBounds.center.y + totalHeight * 0.65f,
                meshBounds.center.z
            );
            Vector3 headSizeInMesh = meshBounds.size * 0.45f;
    
            // Мировые координаты
            Transform meshTransform = smr.transform;
            Vector3 worldHeadCenter = meshTransform.TransformPoint(headCenterInMesh);
            Vector3 worldHeadSize = Vector3.Scale(headSizeInMesh, meshTransform.lossyScale);
    
            // Смещение относительно кости (HairPhysicsManager)
            boundsCenter = worldHeadCenter - headBone.position;
            boundsSize = worldHeadSize;
    
            UpdateLocalBounds();
            Debug.Log($"Auto-set: offset={boundsCenter}, size={boundsSize}");
        }
        
        float SampleSDFInternal(Vector3 worldPos, Vector3 minBounds, Vector3 maxBounds)
        {
            // Для отладки возвращаем просто координаты
            // Реальное сэмплирование в шейдере
            Vector3 localPos = headBone.InverseTransformPoint(worldPos);
            return localPos.y; // Placeholder
        }

        void OnDestroy()
        {
            particleBuffer?.Release();
            rootBuffer?.Release();
            paramsBuffer?.Release();
            interpolationBuffer?.Release();
            finalParticleBuffer?.Release();
            indexBuffer?.Dispose();
            rootNormalsBuffer?.Release();
            atlasIndicesBuffer?.Release();
            rootDirectionsBuffer?.Release();
            sphereCentersBuffer?.Release();
            sphereRadiiBuffer?.Release();
            localHeadOffsetsBuffer?.Release();
            guideAtlasIndicesBuffer?.Release();
        }

        // Этот метод можно вызвать извне для получения текущих позиций частиц (например, для интерполяции hair cards)
        public Vector3[] GetParticlePositions()
        {
            var particles = new Particle[totalParticles];
            particleBuffer.GetData(particles);
            Vector3[] positions = new Vector3[totalParticles];
            for (int i = 0; i < totalParticles; i++)
                positions[i] = particles[i].position;
            return positions;
        }
        
        private static Vector3 ComputeMinExtents(Bounds meshBounds)
        {
            float largestSide = MaxComponent(meshBounds.size);
            float padding = largestSide / 20;
            return meshBounds.center - (Vector3.one * (largestSide * 0.5f + padding));
        }
        private static Vector3 ComputeMaxExtents(Bounds meshBounds)
        {
            float largestSide = MaxComponent(meshBounds.size);
            float padding = largestSide / 20;
            return meshBounds.center + (Vector3.one * (largestSide * 0.5f + padding));
        }
        private static float MaxComponent(Vector3 vector)
        {
            return Mathf.Max(vector.x, vector.y, vector.z);
        }

        public void SetSDFTexture(Texture3D texture)
        {
            sdfTexture = texture;
            if (hairCompute != null && kernelIndex >= 0)
            {
                hairCompute.SetTexture(kernelIndex, "_SDFTexture", sdfTexture);
            }
        }

        public void GenerateSDFInShader()
        {
#if UNITY_EDITOR
            if (sourceMesh == null || hairCompute == null) return;
            
            UpdateLocalBounds();

            // 1. Запекаем меш с учётом скиннинга (если нужны деформации)
            Mesh bakedMesh = new Mesh();
            var skinnedRenderer = sourceGameObject.GetComponent<SkinnedMeshRenderer>();
            if (skinnedRenderer != null)
                skinnedRenderer.BakeMesh(bakedMesh, true);
            else
                bakedMesh = sourceMesh;

            Vector3[] vertices = bakedMesh.vertices;
            int[] triangles = bakedMesh.triangles;
            Vector3[] normals = bakedMesh.normals;

            // 2. Преобразуем вершины в локальные координаты относительно HairPhysicsManager
            // (это будет SDF в локальных координатах головы)
            Transform managerTransform = headBone;
            Transform meshTransform = sourceGameObject.transform;
            
            for (int i = 0; i < vertices.Length; i++)
            {
                // Сначала в мировые координаты
                Vector3 worldPos = meshTransform.TransformPoint(vertices[i]);
                // Потом в локальные координаты менеджера
                vertices[i] = managerTransform.InverseTransformPoint(worldPos);
            }

            // 3. Границы уже в локальных координатах (относительно менеджера)
            Vector3 minBounds = sdfBoundsCenterLocal - sdfBoundsSizeLocal * 0.5f;
            Vector3 maxBounds = sdfBoundsCenterLocal + sdfBoundsSizeLocal * 0.5f;
            
            // Отладка
            Debug.Log($"=== SDF Generation ===");
            Debug.Log($"SdfResolution: {sdfResolution}");
            Debug.Log($"boundsCenter: {boundsCenter}");
            Debug.Log($"boundsSize: {boundsSize}");
            Debug.Log($"minBounds: {minBounds}");
            Debug.Log($"maxBounds: {maxBounds}");
            
            // Проверяем центр объема
            Vector3 centerPos = boundsCenter;
            Debug.Log($"Center position (local): {centerPos}");
            
            // Проверяем точки по границам
            Vector3 corner1 = boundsCenter - boundsSize * 0.5f;
            Vector3 corner2 = boundsCenter + boundsSize * 0.5f;
            Debug.Log($"Corner 1: {corner1}");
            Debug.Log($"Corner 2: {corner2}");

            // 3. Настраиваем буферы
            ComputeBuffer vertexBuffer = new ComputeBuffer(vertices.Length, Marshal.SizeOf(typeof(Vector3)));
            vertexBuffer.SetData(vertices);
            ComputeBuffer normalBuffer = new ComputeBuffer(normals.Length, Marshal.SizeOf(typeof(Vector3)));
            normalBuffer.SetData(normals);
            ComputeBuffer triangleBuffer = new ComputeBuffer(triangles.Length, sizeof(int));
            triangleBuffer.SetData(triangles);

            int kernel = sdfCompute.FindKernel("CSMain");
            int totalVoxels = sdfResolution * sdfResolution * sdfResolution;
            ComputeBuffer outputBuffer = new ComputeBuffer(totalVoxels, sizeof(float));
            ComputeBuffer intermediateBuffer = new ComputeBuffer(totalVoxels, sizeof(float)); // Для промежуточного хранения

            // 4. Передаём данные в шейдер - используем локальные координаты
            sdfCompute.SetInt("SdfResolution", sdfResolution);
            sdfCompute.SetBuffer(kernel, "MeshVerticesBuffer", vertexBuffer);
            sdfCompute.SetBuffer(kernel, "MeshNormalsBuffer", normalBuffer);
            sdfCompute.SetBuffer(kernel, "MeshTrianglesBuffer", triangleBuffer);
            sdfCompute.SetBuffer(kernel, "Output", outputBuffer);
            sdfCompute.SetBuffer(kernel, "IntermediateOutput", intermediateBuffer);
            sdfCompute.SetVector("MinExtents", minBounds);
            sdfCompute.SetVector("MaxExtents", maxBounds);
            
            // Вычисляем максимальный размер для нормализации
            Vector3 boundsSize1 = maxBounds - minBounds;
            float maxDim = Mathf.Max(boundsSize1.x, boundsSize1.y, boundsSize1.z);
            sdfCompute.SetFloat("MaxDim", maxDim);

            // 5. Запуск основного ядра (расчёт SDF)
            int threadGroupSize = Mathf.CeilToInt(sdfResolution / 8.0f);
            sdfCompute.Dispatch(kernel, threadGroupSize, threadGroupSize, threadGroupSize);

            // 6. Запуск ядра гауссова размытия
            int blurKernel = sdfCompute.FindKernel("CSMainBlur");
            sdfCompute.SetBuffer(blurKernel, "Output", outputBuffer);
            sdfCompute.SetBuffer(blurKernel, "IntermediateOutput", intermediateBuffer);
            sdfCompute.SetInt("SdfResolution", sdfResolution);
            sdfCompute.Dispatch(blurKernel, threadGroupSize, threadGroupSize, threadGroupSize);

            // 7. Чтение результата и создание текстуры
            float[] outputData = new float[totalVoxels];
            outputBuffer.GetData(outputData);
            
            // Отладка: проверяем значения SDF для нескольких точек
            Debug.Log($"=== SDF Values Sample ===");
            // Проверяем центр (должен быть внутри головы)
            int centerIdx = (sdfResolution/2) * sdfResolution * sdfResolution + (sdfResolution/2) * sdfResolution + (sdfResolution/2);
            Debug.Log($"Center voxel SDF: {outputData[centerIdx]:F4}");
            
            // Проверяем углы
            int corner1Idx = 0; // Min corner
            int corner2Idx = totalVoxels - 1; // Max corner
            Debug.Log($"Corner 1 (min) SDF: {outputData[corner1Idx]:F4}");
            Debug.Log($"Corner 2 (max) SDF: {outputData[corner2Idx]:F4}");
            
            // Проверяем сколько значений отрицательных (внутри)
            int negativeCount = 0;
            int positiveCount = 0;
            for (int i = 0; i < totalVoxels; i++)
            {
                if (outputData[i] < 0) negativeCount++;
                else positiveCount++;
            }
            Debug.Log($"Negative voxels (inside): {negativeCount}");
            Debug.Log($"Positive voxels (outside): {positiveCount}");

            Texture3D tex = new Texture3D(sdfResolution, sdfResolution, sdfResolution, TextureFormat.RFloat, false);
            tex.filterMode = FilterMode.Trilinear;
            tex.SetPixelData(outputData, 0);
            tex.Apply();

            // 7. Сохранение
            string path = EditorUtility.SaveFilePanelInProject("Save SDF Texture", "SDF_" + sourceMesh.name, "asset", "Save SDF texture");
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.CreateAsset(tex, path);
                sdfTexture = tex;
                EditorUtility.SetDirty(this);
                Debug.Log($"SDF saved to: {path}");
            }
            
            AssetDatabase.Refresh();

            // 8. Освобождение ресурсов
            vertexBuffer.Release();
            normalBuffer.Release();
            triangleBuffer.Release();
            outputBuffer.Release();
            intermediateBuffer.Release();
            // DestroyImmediate(bakedMesh);

            SetSDFTexture(sdfTexture);
#endif
        }
    }
}
