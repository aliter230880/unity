// Vibe Unity Developer
// Install: Assets/Editor/VibeUnityDeveloper.cs
// Menu: Window > Vibe Coding > Fullstack Developer

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace VibeUnityDeveloper
{
    [Serializable]
    public class ProjectInfo
    {
        public string name;
        public string path;
        public string unityVersion;
    }

    [Serializable]
    public class SceneInfo
    {
        public string name;
        public string hierarchy;
    }

    [Serializable]
    public class FileRecord
    {
        public string path;
        public string type;
        public bool isText;
        public long size;
        public string content;
    }

    [Serializable]
    public class SyncPayload
    {
        public ProjectInfo project;
        public SceneInfo scene;
        public List<FileRecord> files = new List<FileRecord>();
    }

    [Serializable]
    public class CommandEnvelope
    {
        public CommandItem[] commands;
    }

    [Serializable]
    public class CommandItem
    {
        public string id;
        public string type;
        public string path;
        public string content;
        public string name;
        public string primitive;
        public string components;
        public string position;
        public string rotation;
        public string scale;
        public string parent;
        public string color;
        public string component;
        public string target;
        public string message;
    }

    [Serializable]
    public class CommandResult
    {
        public string id;
        public bool ok;
        public string result;
    }

    public class VibeUnityDeveloperWindow : EditorWindow
    {
        private const string PrefServerUrl = "VibeUnityDeveloper_ServerUrl";
        private const string PrefPolling = "VibeUnityDeveloper_Polling";
        private const int MaxTextBytes = 350 * 1024;

        private string serverUrl = "http://localhost:17861";
        private bool polling;
        private bool requestBusy;
        private string status = "Ready.";
        private string lastSync = "Never";
        private int lastFileCount;
        private Vector2 scroll;
        private double lastPoll;
        private readonly List<string> log = new List<string>();

        private static readonly HashSet<string> ExcludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", "Library", "Temp", "Logs", "Obj", "obj", "Build", "Builds",
            "UserSettings", "MemoryCaptures", "Recordings", "node_modules", ".vibe-backups"
        };

        private static readonly HashSet<string> TextExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".asmdef", ".asmref", ".json", ".txt", ".md", ".xml", ".yaml", ".yml",
            ".unity", ".prefab", ".mat", ".asset", ".controller", ".overridecontroller", ".anim",
            ".shader", ".compute", ".hlsl", ".cginc", ".uss", ".uxml", ".inputactions", ".csproj", ".sln"
        };

        [MenuItem("Window/Vibe Coding/Fullstack Developer")]
        public static void ShowWindow()
        {
            var window = GetWindow<VibeUnityDeveloperWindow>("Vibe Developer");
            window.minSize = new Vector2(460, 560);
        }

        private void OnEnable()
        {
            serverUrl = EditorPrefs.GetString(PrefServerUrl, serverUrl);
            polling = EditorPrefs.GetBool(PrefPolling, false);
            EditorApplication.update += OnEditorUpdate;
        }

        private void OnDisable()
        {
            EditorPrefs.SetString(PrefServerUrl, serverUrl);
            EditorPrefs.SetBool(PrefPolling, polling);
            EditorApplication.update -= OnEditorUpdate;
        }

        private void OnEditorUpdate()
        {
            if (!polling || requestBusy) return;
            double now = EditorApplication.timeSinceStartup;
            if (now - lastPoll < 2.0) return;
            lastPoll = now;
            PollCommands();
        }

        private void OnGUI()
        {
            GUILayout.Space(8);
            GUILayout.Label("Vibe Unity Developer", EditorStyles.boldLabel);
            GUILayout.Label("Sync files, then chat in the browser. This plugin applies approved commands.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(8);

            EditorGUI.BeginChangeCheck();
            serverUrl = EditorGUILayout.TextField("Local server", serverUrl);
            if (EditorGUI.EndChangeCheck())
                EditorPrefs.SetString(PrefServerUrl, serverUrl);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Web UI", GUILayout.Height(28)))
                Application.OpenURL(serverUrl);
            if (GUILayout.Button("Sync All Files", GUILayout.Height(28)))
                SyncAllFiles();
            GUILayout.EndHorizontal();

            bool nextPolling = GUILayout.Toggle(polling, "Poll Commands from server", "Button", GUILayout.Height(30));
            if (nextPolling != polling)
            {
                polling = nextPolling;
                EditorPrefs.SetBool(PrefPolling, polling);
                AddLog(polling ? "Polling enabled." : "Polling disabled.");
            }

            GUILayout.Space(8);
            EditorGUILayout.HelpBox(status, MessageType.Info);
            GUILayout.Label("Last sync: " + lastSync + " | files: " + lastFileCount, EditorStyles.miniLabel);

            GUILayout.Space(8);
            GUILayout.Label("Activity", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = Math.Max(0, log.Count - 80); i < log.Count; i++)
                GUILayout.Label(log[i], EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void SyncAllFiles()
        {
            if (requestBusy) return;
            try
            {
                status = "Scanning Unity project...";
                Repaint();

                var payload = BuildSyncPayload();
                string json = JsonUtility.ToJson(payload);
                lastFileCount = payload.files.Count;
                requestBusy = true;
                EditorCoroutine.Start(PostJson(CombineUrl("/api/unity/sync"), json, (ok, text) =>
                {
                    requestBusy = false;
                    if (ok)
                    {
                        lastSync = DateTime.Now.ToString("HH:mm:ss");
                        status = "Synced " + lastFileCount + " files. You can chat in the browser now.";
                        AddLog("Synced project: " + lastFileCount + " files.");
                    }
                    else
                    {
                        status = "Sync failed: " + text;
                        AddLog(status);
                    }
                    Repaint();
                }));
            }
            catch (Exception ex)
            {
                requestBusy = false;
                status = "Sync failed: " + ex.Message;
                AddLog(status);
                Repaint();
            }
        }

        private SyncPayload BuildSyncPayload()
        {
            string root = ProjectRoot();
            var payload = new SyncPayload();
            payload.project = new ProjectInfo
            {
                name = Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                path = root,
                unityVersion = Application.unityVersion
            };
            payload.scene = new SceneInfo
            {
                name = EditorSceneManager.GetActiveScene().name,
                hierarchy = BuildSceneHierarchy()
            };

            foreach (string file in EnumerateUnityFiles(root))
            {
                var info = new FileInfo(file);
                string rel = ToProjectRelative(root, file);
                bool isText = IsTextFile(file);
                string content = "";
                if (isText && info.Length <= MaxTextBytes)
                {
                    try { content = File.ReadAllText(file, Encoding.UTF8); }
                    catch { content = ""; }
                }

                payload.files.Add(new FileRecord
                {
                    path = rel,
                    type = Classify(rel),
                    isText = isText,
                    size = info.Length,
                    content = content
                });
            }

            return payload;
        }

        private IEnumerable<string> EnumerateUnityFiles(string root)
        {
            string[] mainDirs = { "Assets", "Packages", "ProjectSettings" };
            foreach (string dir in mainDirs)
            {
                string full = Path.Combine(root, dir);
                if (Directory.Exists(full))
                {
                    foreach (string file in WalkDirectory(full))
                        yield return file;
                }
            }

            foreach (string file in Directory.GetFiles(root))
            {
                string ext = Path.GetExtension(file);
                if (TextExtensions.Contains(ext))
                    yield return file;
            }
        }

        private IEnumerable<string> WalkDirectory(string dir)
        {
            string name = Path.GetFileName(dir);
            if (ExcludedDirs.Contains(name)) yield break;

            string[] files = new string[0];
            try { files = Directory.GetFiles(dir); } catch { }
            foreach (string file in files)
            {
                if (Path.GetFileName(file).EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                yield return file;
            }

            string[] dirs = new string[0];
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (string child in dirs)
            {
                foreach (string file in WalkDirectory(child))
                    yield return file;
            }
        }

        private string BuildSceneHierarchy()
        {
            try
            {
                Scene scene = EditorSceneManager.GetActiveScene();
                if (!scene.IsValid()) return "";
                var sb = new StringBuilder();
                sb.AppendLine("Scene: " + scene.name);
                GameObject[] roots = scene.GetRootGameObjects();
                int count = 0;
                foreach (GameObject root in roots)
                {
                    AppendObject(sb, root, 0, ref count);
                    if (count > 500 || sb.Length > 24000)
                    {
                        sb.AppendLine("... clipped");
                        break;
                    }
                }
                return sb.ToString();
            }
            catch
            {
                return "";
            }
        }

        private void AppendObject(StringBuilder sb, GameObject go, int depth, ref int count)
        {
            if (go == null || depth > 8 || count > 500 || sb.Length > 24000) return;
            count++;
            sb.Append(new string(' ', depth * 2));
            sb.Append(go.name);
            sb.Append(" pos=");
            sb.Append(VectorToString(go.transform.position));
            Component[] components = go.GetComponents<Component>();
            var names = new List<string>();
            foreach (Component component in components)
            {
                if (component == null || component is Transform) continue;
                names.Add(component.GetType().Name);
            }
            if (names.Count > 0)
            {
                sb.Append(" components=");
                sb.Append(string.Join(",", names.ToArray()));
            }
            if (!go.activeSelf) sb.Append(" disabled");
            sb.AppendLine();
            for (int i = 0; i < go.transform.childCount; i++)
                AppendObject(sb, go.transform.GetChild(i).gameObject, depth + 1, ref count);
        }

        private void PollCommands()
        {
            requestBusy = true;
            EditorCoroutine.Start(GetJson(CombineUrl("/api/unity/commands"), (ok, text) =>
            {
                requestBusy = false;
                if (!ok)
                {
                    status = "Command poll failed: " + text;
                    Repaint();
                    return;
                }
                ExecuteCommandEnvelope(text);
                Repaint();
            }));
        }

        private void ExecuteCommandEnvelope(string json)
        {
            CommandEnvelope envelope = JsonUtility.FromJson<CommandEnvelope>(json);
            if (envelope == null || envelope.commands == null || envelope.commands.Length == 0) return;
            foreach (CommandItem command in envelope.commands)
            {
                bool ok = false;
                string result = "";
                try
                {
                    ok = ExecuteCommand(command, out result);
                }
                catch (Exception ex)
                {
                    ok = false;
                    result = ex.Message;
                }
                AddLog((ok ? "OK " : "FAIL ") + command.type + " " + result);
                ReportCommand(command.id, ok, result);
            }
            AssetDatabase.Refresh();
            SyncAllFiles();
        }

        private bool ExecuteCommand(CommandItem command, out string result)
        {
            result = "";
            string type = command.type ?? "";
            if (type == "write_file" || type == "create_script")
            {
                return WriteProjectFile(command.path, command.content, out result);
            }
            if (type == "create_gameobject")
            {
                return CreateGameObject(command, out result);
            }
            if (type == "add_component")
            {
                return AddComponentCommand(command.target, command.component, out result);
            }
            if (type == "set_transform")
            {
                return SetTransformCommand(command, out result);
            }
            if (type == "refresh")
            {
                AssetDatabase.Refresh();
                result = "AssetDatabase refreshed.";
                return true;
            }
            if (type == "save_scene")
            {
                EditorSceneManager.SaveOpenScenes();
                result = "Open scenes saved.";
                return true;
            }
            result = "Unknown command type: " + type;
            return false;
        }

        private bool WriteProjectFile(string relPath, string content, out string result)
        {
            result = "";
            if (!IsSafeEditablePath(relPath))
            {
                result = "Unsafe path: " + relPath;
                return false;
            }
            string root = ProjectRoot();
            string full = Path.GetFullPath(Path.Combine(root, NormalizeRel(relPath)));
            if (!IsInside(root, full))
            {
                result = "Path escapes project: " + relPath;
                return false;
            }
            string directory = Path.GetDirectoryName(full);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

            if (File.Exists(full))
                BackupFile(root, full);

            File.WriteAllText(full, content ?? "", Encoding.UTF8);
            AssetDatabase.Refresh();
            result = relPath;
            return true;
        }

        private bool CreateGameObject(CommandItem command, out string result)
        {
            string primitive = (command.primitive ?? "").ToLowerInvariant();
            GameObject go;
            if (primitive == "cube") go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            else if (primitive == "sphere") go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            else if (primitive == "capsule") go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            else if (primitive == "cylinder") go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            else if (primitive == "plane") go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            else if (primitive == "quad") go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            else go = new GameObject();

            go.name = string.IsNullOrEmpty(command.name) ? "VibeObject" : command.name;
            Undo.RegisterCreatedObjectUndo(go, "Vibe create object");
            go.transform.position = ParseVector(command.position, Vector3.zero);
            go.transform.rotation = Quaternion.Euler(ParseVector(command.rotation, Vector3.zero));
            go.transform.localScale = ParseVector(command.scale, Vector3.one);

            if (!string.IsNullOrEmpty(command.parent))
            {
                GameObject parent = FindObject(command.parent);
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            ApplyColor(go, command.color);
            AddComponents(go, command.components);
            Selection.activeGameObject = go;
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            result = go.name;
            return true;
        }

        private bool AddComponentCommand(string target, string componentName, out string result)
        {
            GameObject go = FindObject(target);
            if (go == null)
            {
                result = "Object not found: " + target;
                return false;
            }
            bool ok = AddComponentByName(go, componentName);
            result = ok ? componentName : "Component not found: " + componentName;
            return ok;
        }

        private bool SetTransformCommand(CommandItem command, out string result)
        {
            GameObject go = FindObject(command.target);
            if (go == null)
            {
                result = "Object not found: " + command.target;
                return false;
            }
            Undo.RecordObject(go.transform, "Vibe set transform");
            if (!string.IsNullOrEmpty(command.position)) go.transform.position = ParseVector(command.position, go.transform.position);
            if (!string.IsNullOrEmpty(command.rotation)) go.transform.rotation = Quaternion.Euler(ParseVector(command.rotation, go.transform.rotation.eulerAngles));
            if (!string.IsNullOrEmpty(command.scale)) go.transform.localScale = ParseVector(command.scale, go.transform.localScale);
            EditorUtility.SetDirty(go);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            result = go.name;
            return true;
        }

        private void AddComponents(GameObject go, string components)
        {
            if (string.IsNullOrEmpty(components)) return;
            string[] parts = components.Split(',');
            foreach (string part in parts)
            {
                string name = part.Trim();
                if (name.Length > 0) AddComponentByName(go, name);
            }
        }

        private bool AddComponentByName(GameObject go, string componentName)
        {
            if (go == null || string.IsNullOrEmpty(componentName)) return false;
            string name = componentName.Trim();
            if (name == "Rigidbody") { Undo.AddComponent<Rigidbody>(go); return true; }
            if (name == "Rigidbody2D") { Undo.AddComponent<Rigidbody2D>(go); return true; }
            if (name == "BoxCollider") { if (go.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(go); return true; }
            if (name == "SphereCollider") { if (go.GetComponent<Collider>() == null) Undo.AddComponent<SphereCollider>(go); return true; }
            if (name == "CapsuleCollider") { if (go.GetComponent<Collider>() == null) Undo.AddComponent<CapsuleCollider>(go); return true; }
            if (name == "AudioSource") { Undo.AddComponent<AudioSource>(go); return true; }
            if (name == "Light") { Undo.AddComponent<Light>(go); return true; }
            if (name == "Camera") { Undo.AddComponent<Camera>(go); return true; }

            Type type = FindComponentType(name);
            if (type != null && typeof(Component).IsAssignableFrom(type))
            {
                Undo.AddComponent(go, type);
                return true;
            }
            return false;
        }

        private Type FindComponentType(string name)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(name);
                if (type != null) return type;
                Type[] types;
                try { types = assembly.GetTypes(); } catch { continue; }
                foreach (Type t in types)
                    if (t.Name == name) return t;
            }
            return null;
        }

        private GameObject FindObject(string target)
        {
            if (string.IsNullOrEmpty(target)) return null;
            GameObject direct = GameObject.Find(target);
            if (direct != null) return direct;
            string[] parts = target.Split('/');
            GameObject current = GameObject.Find(parts[0]);
            for (int i = 1; current != null && i < parts.Length; i++)
            {
                Transform child = current.transform.Find(parts[i]);
                current = child != null ? child.gameObject : null;
            }
            return current;
        }

        private void ApplyColor(GameObject go, string color)
        {
            if (go == null || string.IsNullOrEmpty(color)) return;
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Color c;
            if (!ColorUtility.TryParseHtmlString(color, out c))
            {
                string lower = color.ToLowerInvariant();
                if (lower == "red") c = Color.red;
                else if (lower == "green") c = Color.green;
                else if (lower == "blue") c = Color.blue;
                else if (lower == "yellow") c = Color.yellow;
                else if (lower == "black") c = Color.black;
                else if (lower == "gray" || lower == "grey") c = Color.gray;
                else c = Color.white;
            }
            var mat = new Material(Shader.Find("Standard"));
            mat.color = c;
            renderer.sharedMaterial = mat;
        }

        private Vector3 ParseVector(string text, Vector3 fallback)
        {
            if (string.IsNullOrEmpty(text)) return fallback;
            var matches = Regex.Matches(text, @"-?\d+(?:[\.,]\d+)?");
            if (matches.Count < 3) return fallback;
            return new Vector3(ParseFloat(matches[0].Value), ParseFloat(matches[1].Value), ParseFloat(matches[2].Value));
        }

        private float ParseFloat(string value)
        {
            float result;
            if (float.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result))
                return result;
            return 0f;
        }

        private void ReportCommand(string commandId, bool ok, string result)
        {
            var payload = new CommandResult { id = commandId, ok = ok, result = result ?? "" };
            EditorCoroutine.Start(PostJson(CombineUrl("/api/unity/commands/result"), JsonUtility.ToJson(payload), null));
        }

        private IEnumerator GetJson(string url, Action<bool, string> callback)
        {
            var req = UnityWebRequest.Get(url);
            req.timeout = 30;
            yield return req.SendWebRequest();
            bool ok = RequestOk(req);
            string text = ok ? req.downloadHandler.text : RequestError(req);
            req.Dispose();
            if (callback != null) callback(ok, text);
        }

        private IEnumerator PostJson(string url, string json, Action<bool, string> callback)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json ?? "{}");
            var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 120;
            yield return req.SendWebRequest();
            bool ok = RequestOk(req);
            string text = ok ? req.downloadHandler.text : RequestError(req);
            req.Dispose();
            if (callback != null) callback(ok, text);
        }

        private bool RequestOk(UnityWebRequest req)
        {
#if UNITY_2020_1_OR_NEWER
            return req.result == UnityWebRequest.Result.Success;
#else
            return !req.isNetworkError && !req.isHttpError;
#endif
        }

        private string RequestError(UnityWebRequest req)
        {
            return "HTTP " + req.responseCode + " " + req.error + " " + (req.downloadHandler != null ? req.downloadHandler.text : "");
        }

        private string CombineUrl(string path)
        {
            return serverUrl.TrimEnd('/') + path;
        }

        private string ProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private string ToProjectRelative(string root, string full)
        {
            string rel = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return rel.Replace('\\', '/');
        }

        private string NormalizeRel(string rel)
        {
            return (rel ?? "").Replace('\\', '/').TrimStart('/');
        }

        private bool IsInside(string root, string full)
        {
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string f = Path.GetFullPath(full);
            return f.StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsSafeEditablePath(string rel)
        {
            string p = NormalizeRel(rel);
            if (p.Length == 0 || p.Contains("..") || Path.IsPathRooted(p)) return false;
            return p.StartsWith("Assets/") || p.StartsWith("Packages/") || p.StartsWith("ProjectSettings/");
        }

        private void BackupFile(string root, string full)
        {
            string rel = ToProjectRelative(root, full);
            string backup = Path.Combine(root, ".vibe-backups", DateTime.Now.ToString("yyyyMMdd-HHmmss"), rel.Replace('/', Path.DirectorySeparatorChar));
            string dir = Path.GetDirectoryName(backup);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.Copy(full, backup, true);
        }

        private bool IsTextFile(string file)
        {
            return TextExtensions.Contains(Path.GetExtension(file));
        }

        private string Classify(string rel)
        {
            string ext = Path.GetExtension(rel).ToLowerInvariant();
            if (ext == ".cs") return "script";
            if (ext == ".unity") return "scene";
            if (ext == ".prefab") return "prefab";
            if (ext == ".mat") return "material";
            if (ext == ".shader" || ext == ".hlsl" || ext == ".compute" || ext == ".cginc") return "shader";
            if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".tga" || ext == ".psd") return "image";
            if (ext == ".wav" || ext == ".mp3" || ext == ".ogg") return "audio";
            if (ext == ".fbx" || ext == ".obj" || ext == ".glb") return "model";
            if (TextExtensions.Contains(ext)) return "config";
            return "other";
        }

        private string VectorToString(Vector3 v)
        {
            return v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2");
        }

        private void AddLog(string message)
        {
            log.Add(DateTime.Now.ToString("HH:mm:ss") + " " + message);
            if (log.Count > 200) log.RemoveRange(0, log.Count - 200);
        }
    }

    public class EditorCoroutine
    {
        private readonly IEnumerator routine;
        private object current;

        private EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }

        public static EditorCoroutine Start(IEnumerator routine)
        {
            var coroutine = new EditorCoroutine(routine);
            if (!coroutine.routine.MoveNext()) return coroutine;
            coroutine.current = coroutine.routine.Current;
            EditorApplication.update += coroutine.Tick;
            return coroutine;
        }

        private void Tick()
        {
            var asyncOperation = current as AsyncOperation;
            if (asyncOperation != null && !asyncOperation.isDone) return;
            if (!routine.MoveNext())
            {
                EditorApplication.update -= Tick;
                return;
            }
            current = routine.Current;
        }
    }
}

