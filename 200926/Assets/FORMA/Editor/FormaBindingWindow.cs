using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forma.Editor
{
    /// <summary>
    /// Калибровка таблицы FormaMacBinding по ЖИВОМУ MAC-аватару (окно работает в Play).
    /// FORMA → Calibrate MAC Binding:
    ///  - список строк биндинга: слайдер параметра → веса форм на аватаре (live);
    ///  - «Захватить конец кривой» — читает текущий средний вес форм и ставит его
    ///    конечной точкой кривой отклика (это и есть ручная калибровка);
    ///  - «Линейная» — сброс кривой строки;
    ///  - «Capture(): аватар → параметры» — обратное чтение текущего аватара;
    ///  - «Сохранить ассет» — пишет биндинг в Assets/FORMA/Resources/FORMA/FormaMacBinding.asset,
    ///    после чего он используется вместо программного дефолта.
    /// </summary>
    public sealed class FormaBindingWindow : EditorWindow
    {
        MacAvatarAdapter _adapter;
        FormaMacBinding _binding;
        Vector2 _scroll;
        int _selected = -1;

        [MenuItem("FORMA/Calibrate MAC Binding")]
        static void Open() => GetWindow<FormaBindingWindow>("FORMA MAC Binding");

        void OnEnable() => Refresh();

        void Refresh()
        {
            _adapter = Object.FindFirstObjectByType<MacAvatarAdapter>();
            if (_binding == null)
            {
                _binding = Resources.Load<FormaMacBinding>("FORMA/FormaMacBinding");
                if (_binding == null && _adapter != null)
                {
                    var f = typeof(MacAvatarAdapter)
                        .GetField("_binding", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    _binding = f != null ? f.GetValue(_adapter) as FormaMacBinding : null;
                }
                if (_binding == null) _binding = FormaMacBinding.CreateDefault();
            }
        }

        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Войдите в Play со сценой FORMA_Studio (режим MAC) — окно калибрует живой аватар.", MessageType.Info);
                return;
            }
            if (_adapter == null || GUILayout.Button("Обновить")) Refresh();
            if (_adapter == null || !_adapter.IsReady)
            {
                EditorGUILayout.HelpBox("MAC-аватар не готов: включите режим «Аватары: MAC» в панели FORMA.", MessageType.Warning);
                return;
            }
            if (_binding == null || _binding.rows == null || _binding.rows.Length == 0)
            {
                EditorGUILayout.HelpBox("Биндинг пуст.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Строк биндинга:", _binding.rows.Length.ToString());
            if (GUILayout.Button("Capture(): аватар → параметры (в панель)"))
            {
                var fs = Object.FindFirstObjectByType<FormaStudio>();
                if (fs != null && fs.useMacAvatars)
                {
                    var p = _adapter.Capture();
                    var fld = typeof(FormaStudio).GetField("Parameters");
                    if (fld != null) fld.SetValue(fs, p);
                    Debug.Log("[FORMA/Calibrate] параметры панели обновлены из аватара");
                }
            }

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < _binding.rows.Length; i++)
            {
                var row = _binding.rows[i];
                if (row == null || string.IsNullOrEmpty(row.paramId)) continue;
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(row.paramId, GUILayout.Width(120));

                var fs = Object.FindFirstObjectByType<FormaStudio>();
                var p = fs != null && fs.useMacAvatars ? fs.Parameters : null;
                if (p != null)
                {
                    float v = AvatarCatalog.GetFloat(p, row.paramId);
                    float nv = GUILayout.HorizontalSlider(v, 0f, 1f);
                    if (!Mathf.Approximately(nv, v))
                    {
                        AvatarCatalog.SetFloat(p, row.paramId, nv);
                        _adapter.Apply(p); // живое применение
                    }
                    GUILayout.Label(((int)(v * 100)).ToString("D3"), GUILayout.Width(36));
                }

                if (i == _selected) GUI.color = new Color(1f, .95f, .8f);
                if (GUILayout.Button(i == _selected ? "●" : "○", GUILayout.Width(24))) _selected = i == _selected ? -1 : i;
                GUI.color = Color.white;
                GUILayout.EndHorizontal();

                if (i != _selected) continue;

                EditorGUILayout.BeginVertical("helpbox");
                var shapes = row.templateLadder != null && row.templateLadder.Length > 0 ? row.templateLadder : row.shapeNames;
                EditorGUILayout.LabelField("Формы:", shapes != null ? string.Join(", ", shapes) : "—");
                EditorGUILayout.CurveField("Кривая отклика", row.response, Color.cyan, new Rect(0, 0, 1, 100));
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Захватить конец кривой с аватара"))
                {
                    float w = ReadCurrentWeight(row);
                    var keys = row.response.keys;
                    if (keys.Length > 0)
                    {
                        keys[^1].value = w;
                        row.response.keys = keys;
                        EditorUtility.SetDirty(_binding);
                        Debug.Log($"[FORMA/Calibrate] {row.paramId}: конец кривой = {w:F1}");
                    }
                }
                if (GUILayout.Button("Линейная"))
                {
                    row.response = new AnimationCurve(
                        new Keyframe(0f, 0f, 0f, 100f), new Keyframe(1f, 100f, 100f, 0f));
                    EditorUtility.SetDirty(_binding);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            if (GUILayout.Button("Сохранить ассет (Resources/FORMA/FormaMacBinding)"))
            {
                const string dir = "Assets/FORMA/Resources/FORMA";
                if (!AssetDatabase.IsValidFolder(dir))
                    AssetDatabase.CreateFolder("Assets/FORMA/Resources", "FORMA");
                AssetDatabase.CreateAsset(_binding, dir + "/FormaMacBinding.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("[FORMA/Calibrate] биндинг сохранён: " + dir + "/FormaMacBinding.asset");
            }
        }

        float ReadCurrentWeight(FormaMacBinding.Row row)
        {
            var f = typeof(MacAvatarAdapter)
                .GetField("_av", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var av = f != null ? f.GetValue(_adapter) as MagicAvatarCreator.AvatarObject : null;
            var smr = av != null ? av.rootSkinnedMeshRenderer : null;
            var mesh = smr != null ? smr.sharedMesh : null;
            if (mesh == null) return 100f;
            var names = row.templateLadder != null && row.templateLadder.Length > 0 ? row.templateLadder : row.shapeNames;
            if (names == null || names.Length == 0) return 100f;
            float sum = 0f; int n = 0;
            foreach (var nm in names)
            {
                var idx = mesh.GetBlendShapeIndex(nm);
                if (idx < 0) { idx = mesh.GetBlendShapeIndex(nm + "_L"); }
                if (idx < 0) { idx = mesh.GetBlendShapeIndex(nm + "_R"); }
                if (idx < 0) continue;
                sum += smr.GetBlendShapeWeight(idx); n++;
            }
            return n > 0 ? sum / n : 100f;
        }
    }
}
