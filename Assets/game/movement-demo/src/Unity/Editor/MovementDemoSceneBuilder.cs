using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MovementDemo.Unity.Editor
{
    public static class MovementDemoSceneBuilder
    {
        public const string ScenePath = "Assets/game/movement-demo/scenes/CharacterMovementDemo.unity";

        [MenuItem("Tools/Movement Demo/Create Demo Scene")]
        public static void CreateScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode first.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
                throw new InvalidOperationException("Demo scene already exists; open it instead of overwriting it.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save your open scenes before creating the demo.");

            EnsureFolder("Assets/game/movement-demo", "scenes");
            EnsureFolder("Assets/game/movement-demo", "visuals");
            const string spritePath = "Assets/game/movement-demo/visuals/UnitSquare.asset";
            Sprite sprite = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(spritePath))
                if (asset is Sprite existing) sprite = existing;
            if (sprite == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(spritePath) != null)
                    throw new InvalidOperationException("Unexpected existing visual asset.");
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "UnitSquare", filterMode = FilterMode.Point };
                texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
                texture.Apply();
                AssetDatabase.CreateAsset(texture, spritePath);
                sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
                sprite.name = "UnitSquare";
                AssetDatabase.AddObjectToAsset(sprite, texture);
                AssetDatabase.SaveAssets();
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.orthographicSize = 6;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .055f, .085f);
            camera.transform.position = new Vector3(0, 0, -10);

            var grid = new GameObject("Reference Grid").transform;
            var gridColor = new Color(.09f, .15f, .20f);
            for (int i = -24; i <= 24; i++)
                Square(sprite, "Vertical " + i, grid, new Vector2(i, 0), new Vector2(.018f, 32), gridColor, -10);
            for (int i = -16; i <= 16; i++)
                Square(sprite, "Horizontal " + i, grid, new Vector2(0, i), new Vector2(48, .018f), gridColor, -10);
            Square(sprite, "Origin", null, Vector2.zero, new Vector2(.9f, .9f), new Color(.18f, .27f, .34f), -5);
            var player = Square(sprite, "Character View", null, Vector2.zero, new Vector2(.65f, .65f), new Color(.2f, .85f, .9f), 0);
            Square(sprite, "Facing Marker", player, new Vector2(0, .24f), new Vector2(.28f, .1f), Color.white, 1);
            var host = new GameObject("Movement Composition Root").AddComponent<MovementDemoHost>();
            var serialized = new SerializedObject(host);
            serialized.FindProperty("characterView").objectReferenceValue = player;
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("grid").objectReferenceValue = grid;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = host.gameObject;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        private static Transform Square(Sprite sprite, string name, Transform parent, Vector2 position, Vector2 scale, Color color, int order)
        {
            var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            renderer.transform.SetParent(parent, false);
            renderer.transform.localPosition = new Vector3(position.x, position.y, 0);
            renderer.transform.localScale = new Vector3(scale.x, scale.y, 1);
            return renderer.transform;
        }
    }
}
