using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Arena.Unity.Editor
{
    /// <summary>Reproducible editor authoring: scene, passive view prefabs and geometric sprite assets.</summary>
    public static class ArenaSceneBuilder
    {
        public const string ScenePath = "Assets/game/arena/scenes/ArenaDemo.unity";
        private const string VisualFolder = "Assets/game/arena/visuals";

        [MenuItem("Tools/Arena/Prepare UI Assets")]
        public static void EnsureUiAssets()
        {
            const string folder = "Assets/game/arena/ui/Resources";
            const string path = folder + "/ArenaPanelSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<PanelSettings>(path) != null) return;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("An unrelated asset occupies " + path);
            ThemeStyleSheet theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(folder + "/ArenaTheme.tss");
            if (theme == null) throw new InvalidOperationException("Import ArenaTheme.tss before preparing the UI assets.");
            EnsureFolder(folder);
            PanelSettings settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Arena Panel Settings";
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1280, 800);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0;
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/Arena/Create Demo Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before creating the Arena scene.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Save open scenes before creating the Arena scene.");
            EnsureUiAssets();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorSceneManager.OpenScene(ScenePath);
                ConfigureBuildScenes();
                Debug.Log("Arena scene already exists; opened it without overwriting authored changes.");
                return;
            }

            EnsureFolder("Assets/game/arena/scenes");
            EnsureFolder(VisualFolder);
            Material material = SpriteMaterial();
            Sprite square = Shape("Square", ShapeKind.Square);
            Sprite disc = Shape("Disc", ShapeKind.Disc);
            Sprite ring = Shape("Ring", ShapeKind.Ring);
            Sprite diamond = Shape("Diamond", ShapeKind.Diamond);
            GameObject player = ActorPrefab("PlayerView", false, square, disc, ring, diamond, material);
            GameObject enemy = ActorPrefab("EnemyView", true, square, disc, ring, diamond, material);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera camera = new GameObject("Main Camera / observation only").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 5.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.022f, .033f, .049f);
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 50;
            camera.transform.position = new Vector3(.65f, .4f, -10);
            camera.gameObject.AddComponent<AudioListener>();

            Transform grid = new GameObject("Arena / non-authoritative reference grid").transform;
            SpriteObject("Floor", square, material, grid, Vector2.zero, new Vector2(80, 80), new Color(.025f, .039f, .056f), -30);
            for (int index = -24; index <= 24; index++)
            {
                bool major = index % 4 == 0;
                Color color = major ? new Color(.075f, .13f, .16f) : new Color(.046f, .079f, .104f);
                float thickness = major ? .018f : .009f;
                SpriteObject("Grid X " + index, square, material, grid, new Vector2(index, 0), new Vector2(thickness, 48), color, -20);
                SpriteObject("Grid Y " + index, square, material, grid, new Vector2(0, index), new Vector2(48, thickness), color, -20);
            }
            Transform origin = new GameObject("Spawn origin / decorative world marker").transform;
            SpriteObject("Origin ring", ring, material, origin, Vector2.zero, new Vector2(4, 4), new Color(.08f, .20f, .24f, .45f), -12);
            SpriteObject("Origin cross X", square, material, origin, Vector2.zero, new Vector2(.23f, .018f), new Color(.22f, .39f, .43f), -10);
            SpriteObject("Origin cross Y", square, material, origin, Vector2.zero, new Vector2(.018f, .23f), new Color(.22f, .39f, .43f), -10);

            ArenaHost host = new GameObject("Arena / Unity composition host").AddComponent<ArenaHost>();
            SerializedObject serialized = new SerializedObject(host);
            serialized.FindProperty("arenaCamera").objectReferenceValue = camera;
            serialized.FindProperty("referenceGrid").objectReferenceValue = grid;
            serialized.FindProperty("playerPrefab").objectReferenceValue = player;
            serialized.FindProperty("enemyPrefab").objectReferenceValue = enemy;
            serialized.FindProperty("ticksPerSecond").intValue = 60;
            serialized.FindProperty("enemyViewCapacity").intValue = 16;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            ConfigureBuildScenes();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = host.gameObject;
            Debug.Log("Created Arena demo: " + ScenePath);
        }

        [MenuItem("Tools/Arena/Build Windows Player")]
        public static void BuildPlayer()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before building the Arena player.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) CreateScene();
            EnsureUiAssets();
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string output = Path.Combine(projectRoot, ".utmp", "ArenaPlayer", "Arena.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 800;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = new string[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Arena player build failed: " + report.summary.result);
            Debug.Log("Built Arena player: " + output);
        }

        private enum ShapeKind { Square, Disc, Ring, Diamond }

        private static void ConfigureBuildScenes()
        {
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static Sprite Shape(string name, ShapeKind kind)
        {
            string path = VisualFolder + "/" + name + ".asset";
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite sprite) return sprite;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                throw new InvalidOperationException("An unrelated visual asset occupies " + path);
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = (x + .5f - size * .5f) / (size * .5f);
                    float py = (y + .5f - size * .5f) / (size * .5f);
                    float radius = Mathf.Sqrt(px * px + py * py);
                    float alpha;
                    switch (kind)
                    {
                        case ShapeKind.Disc: alpha = Mathf.Clamp01((.94f - radius) * 32); break;
                        case ShapeKind.Ring: alpha = Mathf.Clamp01((.96f - radius) * 32) * Mathf.Clamp01((radius - .83f) * 32); break;
                        case ShapeKind.Diamond: alpha = Mathf.Clamp01((.95f - Mathf.Abs(px) - Mathf.Abs(py)) * 32); break;
                        default: alpha = 1; break;
                    }
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            Sprite created = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), size);
            created.name = name;
            AssetDatabase.AddObjectToAsset(created, texture);
            return created;
        }

        private static Material SpriteMaterial()
        {
            string path = VisualFolder + "/ArenaSpriteUnlit.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) throw new InvalidOperationException("No unlit sprite shader is available.");
            Material material = new Material(shader) { name = "Arena Sprite / Unlit" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static GameObject ActorPrefab(string name, bool enemy, Sprite square, Sprite disc, Sprite ring, Sprite diamond, Material material)
        {
            string path = VisualFolder + "/" + name + ".prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            GameObject root = new GameObject(name);
            try
            {
                Color accent = enemy ? new Color(1, .36f, .29f) : new Color(.12f, .87f, .92f);
                SpriteObject("Outer ring", ring, material, root.transform, Vector2.zero, new Vector2(.92f, .92f), new Color(accent.r, accent.g, accent.b, .45f), 0);
                SpriteObject("Body", enemy ? diamond : disc, material, root.transform, Vector2.zero, new Vector2(.67f, .67f), accent, 1);
                SpriteObject("Core", enemy ? diamond : disc, material, root.transform, Vector2.zero, new Vector2(.32f, .32f), new Color(.045f, .075f, .1f), 2);
                SpriteObject("Facing", square, material, root.transform, new Vector2(0, .26f), new Vector2(.12f, .09f), new Color(.86f, .99f, 1), 3);
                // Passive prefab only: no Update, input, rigidbody or gameplay callback.
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Transform SpriteObject(string name, Sprite sprite, Material material, Transform parent,
            Vector2 position, Vector2 scale, Color color, int order)
        {
            SpriteRenderer renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0);
            renderer.transform.localScale = new Vector3(scale.x, scale.y, 1);
            return renderer.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
