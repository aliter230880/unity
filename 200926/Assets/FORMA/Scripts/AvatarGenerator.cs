using UnityEngine;

namespace Forma
{
    public class AvatarGenerator : MonoBehaviour
    {
        public AvatarParams Parameters = AvatarDefaults.Create(Sex.Female);

        Transform _root;
        MeshFilter _bodyFilter;
        MeshRenderer _bodyRend;
        MeshFilter _hairFilter, _beardFilter, _upperFilter, _lowerFilter, _fullFilter;
        MeshRenderer _hairRend, _beardRend, _upperRend, _lowerRend, _fullRend;
        Transform _leftEye, _rightEye, _lashes;
        BuiltBody _built;
        AvatarMaps _maps;
        Material _skinMat, _hairMat, _clothU, _clothL, _cardMat;
        Texture2D _iris, _skinTex, _bumpTex;
        string _skinKey, _hairKey, _clothKey, _irisKey, _lashKey;
        Sex _builtSex;

        public void EnsureBuilt()
        {
            if (_root != null) return;
            _maps = AvatarMapsLoader.Load();
            _root = new GameObject("AvatarRoot").transform;
            _root.SetParent(transform, false);

            _skinMat = MakeMat("FORMA/LitVertexColor", true);
            _hairMat = MakeMat("FORMA/LitVertexColor", false);
            _clothU = MakeMat("FORMA/LitVertexColor", false);
            _clothL = MakeMat("FORMA/LitVertexColor", false);
            _cardMat = MakeMat("FORMA/LitVertexColor", false);
            if (_cardMat != null)
            {
                _cardMat.SetFloat("_Cutoff", 0.22f);
                _cardMat.EnableKeyword("_ALPHATEST_ON");
            }

            _bodyFilter = ChildMesh("Body", _skinMat, out _bodyRend);
            _hairFilter = ChildMesh("Hair", _hairMat, out _hairRend);
            _beardFilter = ChildMesh("Beard", _hairMat, out _beardRend);
            _upperFilter = ChildMesh("Upper", _clothU, out _upperRend);
            _lowerFilter = ChildMesh("Lower", _clothL, out _lowerRend);
            _fullFilter = ChildMesh("Full", _clothU, out _fullRend);

            _leftEye = MakeEye("LeftEye");
            _rightEye = MakeEye("RightEye");
            _lashes = new GameObject("Lashes").transform;
            _lashes.SetParent(_root, false);

            RebuildBody(true);
            Apply(Parameters);
        }

        MeshFilter ChildMesh(string name, Material mat, out MeshRenderer rend)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root, false);
            var mf = go.AddComponent<MeshFilter>();
            rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return mf;
        }

        static Material MakeMat(string shaderName, bool vertexColor)
        {
            var sh = Shader.Find(shaderName)
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("HDRP/Lit")
                     ?? Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "FORMA.Mat" };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.42f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.42f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            if (vertexColor) m.EnableKeyword("_VERTEXCOLOR");
            return m;
        }

        Transform MakeEye(string name)
        {
            var g = new GameObject(name);
            g.transform.SetParent(_root, false);
            var sclera = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sclera.name = "Sclera";
            sclera.transform.SetParent(g.transform, false);
            sclera.transform.localScale = Vector3.one;
            Object.Destroy(sclera.GetComponent<Collider>());
            var sm = MakeMat("FORMA/LitVertexColor", false);
            sm.color = new Color(0.97f, 0.95f, 0.93f);
            sclera.GetComponent<MeshRenderer>().sharedMaterial = sm;

            var iris = GameObject.CreatePrimitive(PrimitiveType.Quad);
            iris.name = "Iris";
            iris.transform.SetParent(g.transform, false);
            iris.transform.localPosition = new Vector3(0, 0, -0.86f);
            iris.transform.localScale = Vector3.one * 0.96f;
            Object.Destroy(iris.GetComponent<Collider>());

            var pupil = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pupil.name = "Pupil";
            pupil.transform.SetParent(g.transform, false);
            pupil.transform.localPosition = new Vector3(0, 0, -0.885f);
            pupil.transform.localScale = Vector3.one * 0.34f;
            Object.Destroy(pupil.GetComponent<Collider>());
            var pm = new Material(Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
            pm.color = new Color(0.035f, 0.03f, 0.027f);
            pupil.GetComponent<MeshRenderer>().sharedMaterial = pm;
            return g.transform;
        }

        void RebuildBody(bool force)
        {
            if (!force && _built != null && _builtSex == Parameters.sex) return;
            if (_built != null && _built.Mesh != null) Destroy(_built.Mesh);
            _built = BodyBuilder.Build(Parameters.sex);
            _builtSex = Parameters.sex;
            _bodyFilter.sharedMesh = _built.Mesh;
            _hairKey = _clothKey = "";
        }

        public void Apply(AvatarParams p)
        {
            Parameters = p;
            EnsureBuilt();
            bool sexChanged = _built == null || _builtSex != p.sex;
            RebuildBody(sexChanged);
            BodyBuilder.ApplyMorphs(_built, p, _maps);
            UpdateSkin(p);
            UpdateEyes(p);
            UpdateHair(p);
            UpdateClothes(p);
        }

        void UpdateSkin(AvatarParams p)
        {
            string key = p.skin + "|" + p.freckles + "|" + p.freckleScale + "|" + p.freckleColor + "|" + p.smoothness;
            if (key != _skinKey)
            {
                _skinKey = key;
                if (_skinTex != null) Destroy(_skinTex);
                if (_bumpTex != null) Destroy(_bumpTex);
                _skinTex = TextureFactory.MakeSkin(p);
                _bumpTex = TextureFactory.MakeBump(p);
                if (_skinMat.HasProperty("_MainTex")) _skinMat.SetTexture("_MainTex", _skinTex);
                if (_skinMat.HasProperty("_BaseMap")) _skinMat.SetTexture("_BaseMap", _skinTex);
                if (_skinMat.HasProperty("_BumpMap")) _skinMat.SetTexture("_BumpMap", _bumpTex);
            }
            float gloss = MathUtil.Lerp(0.42f, 0.72f, p.smoothness);
            if (_skinMat.HasProperty("_Glossiness")) _skinMat.SetFloat("_Glossiness", gloss);
            if (_skinMat.HasProperty("_Smoothness")) _skinMat.SetFloat("_Smoothness", gloss);
        }

        void UpdateEyes(AvatarParams p)
        {
            if (_irisKey != p.eyeColor)
            {
                _irisKey = p.eyeColor;
                if (_iris != null) Destroy(_iris);
                _iris = TextureFactory.MakeIris(p.eyeColor);
                ApplyIris(_leftEye, _iris);
                ApplyIris(_rightEye, _iris);
            }
            BodyBuilder.EyeAnchors(p, out var left, out var right, out float scale);
            _leftEye.localPosition = left;
            _rightEye.localPosition = right;
            _leftEye.localScale = Vector3.one * scale;
            _rightEye.localScale = Vector3.one * scale;
            _leftEye.LookAt(_leftEye.position + new Vector3(-0.015f, 0, -1f));
            _rightEye.LookAt(_rightEye.position + new Vector3(0.015f, 0, -1f));
            string lashKey = left.x.ToString("F3") + left.y.ToString("F3") + scale.ToString("F3") + p.browArch + p.hairColor + p.eyeSize;
            if (lashKey != _lashKey)
            {
                _lashKey = lashKey;
                RebuildLashes(left, right, scale, p);
            }
        }

        void ApplyIris(Transform eye, Texture2D map)
        {
            var iris = eye.Find("Iris");
            if (iris == null) return;
            var r = iris.GetComponent<MeshRenderer>();
            var m = r.sharedMaterial;
            if (m == null || m.shader == null) m = MakeMat("FORMA/LitVertexColor", false);
            else m = new Material(m);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", map);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", map);
            m.color = Color.white;
            r.sharedMaterial = m;
        }

        void RebuildLashes(Vector3 left, Vector3 right, float scale, AvatarParams p)
        {
            for (int i = _lashes.childCount - 1; i >= 0; i--)
                Destroy(_lashes.GetChild(i).gameObject);
            var lashMat = MakeMat("FORMA/LitVertexColor", false);
            lashMat.color = new Color(0.1f, 0.07f, 0.055f);
            foreach (var eye in new[] { left, right })
            {
                for (int i = 0; i < 12; i++)
                {
                    float t = i / 11f;
                    float a = MathUtil.Lerp(-0.58f, 0.58f, t);
                    var lash = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    Object.Destroy(lash.GetComponent<Collider>());
                    lash.transform.SetParent(_lashes, false);
                    lash.transform.localScale = new Vector3(0.003f, 0.0075f, 0.003f) * MathUtil.Lerp(0.9f, 1.18f, p.eyeSize);
                    lash.transform.position = new Vector3(
                        eye.x + Mathf.Sin(a) * scale * 0.92f,
                        eye.y + scale * 0.74f,
                        eye.z - Mathf.Cos(a) * scale * 0.18f);
                    lash.transform.rotation = Quaternion.Euler(-40f, 0, a * Mathf.Rad2Deg * 0.5f);
                    lash.GetComponent<MeshRenderer>().sharedMaterial = lashMat;
                }
            }
            var browMat = MakeMat("FORMA/LitVertexColor", false);
            browMat.color = MathUtil.Hex(p.hairColor);
            float space = Mathf.Abs(left.x);
            float y = left.y + scale * 1.55f;
            float z = left.z - 0.002f;
            for (int s = -1; s <= 1; s += 2)
            {
                var pts = new[]
                {
                    new Vector3(s * (space - 0.016f), y - 0.004f, -z),
                    new Vector3(s * space, y + MathUtil.Lerp(-0.002f, 0.01f, p.browArch), -(z + 0.006f)),
                    new Vector3(s * (space + 0.024f), y - 0.006f, -z)
                };
                var mesh = MeshBuilder.TubeFromPoints(pts, 0.0034f);
                var go = new GameObject("Brow");
                go.transform.SetParent(_lashes, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = browMat;
            }
        }

        void UpdateHair(AvatarParams p)
        {
            string key = p.hairStyle + "|" + p.hairLength + "|" + p.hairVolume + "|" + p.sex + "|" + p.height + "|" + p.headWidth + "|" + p.facialHair;
            if (key != _hairKey)
            {
                _hairKey = key;
                SwapMesh(_hairFilter, _hairRend, HairBuilder.StrandMesh(p));
                SwapMesh(_beardFilter, _beardRend, HairBuilder.BeardMesh(p));
                BuildHairCards(p);
            }
            var col = MathUtil.Hex(p.hairColor);
            _hairMat.color = col;
            float gloss = MathUtil.Lerp(0.48f, 0.8f, p.hairShine);
            if (_hairMat.HasProperty("_Glossiness")) _hairMat.SetFloat("_Glossiness", gloss);
            if (_hairMat.HasProperty("_Smoothness")) _hairMat.SetFloat("_Smoothness", gloss);
        }

        Transform _cards;

        void BuildHairCards(AvatarParams p)
        {
            if (_cards != null) Destroy(_cards.gameObject);
            if (p.hairStyle == HairStyle.None) return;
            Texture2D tex = p.hairStyle == HairStyle.Short || p.hairStyle == HairStyle.Buzz || p.hairStyle == HairStyle.Pixie
                ? _maps?.HairShort : _maps?.HairBlonde;
            if (tex == null) return;
            _cards = new GameObject("HairCards").transform;
            _cards.SetParent(_root, false);
            var center = BodyBuilder.ScalpCenter(p);
            bool longH = p.hairStyle == HairStyle.Waves || p.hairStyle == HairStyle.Long || p.hairStyle == HairStyle.Curls;
            int n = longH ? 10 : 6;
            var mat = new Material(_cardMat);
            mat.mainTexture = tex;
            mat.color = MathUtil.Hex(p.hairColor);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float ang = t * Mathf.PI * 1.6f - 0.8f;
                var plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.Destroy(plane.GetComponent<Collider>());
                plane.transform.SetParent(_cards, false);
                float hgt = longH ? MathUtil.Lerp(0.28f, 0.55f, p.hairLength) : 0.14f;
                float wid = longH ? 0.18f : 0.12f;
                plane.transform.localScale = new Vector3(wid, hgt, 1);
                plane.transform.position = new Vector3(
                    center.x + Mathf.Sin(ang) * 0.08f,
                    center.y - (longH ? 0.12f : 0.02f),
                    center.z - (Mathf.Cos(ang) * 0.04f - 0.02f));
                plane.transform.rotation = Quaternion.Euler(longH ? 8f : 22f, ang * Mathf.Rad2Deg, 0);
                plane.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
        }

        void UpdateClothes(AvatarParams p)
        {
            string key = string.Join("|", p.upper, p.lower, p.full, p.upperColor, p.lowerColor, p.height, p.build, p.chest, p.waist, p.hips, p.sex, p.shoulders, p.arms, p.thighs, p.fat);
            if (key == _clothKey) return;
            _clothKey = key;
            SwapMesh(_upperFilter, _upperRend, GarmentBuilder.BuildUpper(p));
            SwapMesh(_lowerFilter, _lowerRend, GarmentBuilder.BuildLower(p));
            SwapMesh(_fullFilter, _fullRend, GarmentBuilder.BuildFull(p));
            _clothU.color = MathUtil.Hex(p.upperColor);
            _clothL.color = MathUtil.Hex(p.lowerColor);
        }

        void SwapMesh(MeshFilter filter, MeshRenderer rend, Mesh mesh)
        {
            if (filter.sharedMesh != null && filter.sharedMesh != _built?.Mesh)
                Destroy(filter.sharedMesh);
            filter.sharedMesh = mesh;
            rend.enabled = mesh != null;
        }

        void OnDestroy()
        {
            if (_built?.Mesh != null) Destroy(_built.Mesh);
            if (_skinTex != null) Destroy(_skinTex);
            if (_bumpTex != null) Destroy(_bumpTex);
            if (_iris != null) Destroy(_iris);
            if (_skinMat != null) Destroy(_skinMat);
            if (_hairMat != null) Destroy(_hairMat);
            if (_clothU != null) Destroy(_clothU);
            if (_clothL != null) Destroy(_clothL);
            if (_cardMat != null) Destroy(_cardMat);
        }
    }
}
