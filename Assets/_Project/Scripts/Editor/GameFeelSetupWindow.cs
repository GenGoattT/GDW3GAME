using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Project.Effects;
using Project.Player;

namespace Project.EditorTools
{
    public class GameFeelSetupWindow : EditorWindow
    {
        private const string OutputFolder = "Assets/_Project/Art/Effects";

        private PlayerCamera playerCamera;
        private PlayerMovement player;

        private float snowIntensity = 1f;
        private float windStrength = 1.4f;
        private float fogDensity = 0.01f;

        private float dashFov = 114f;
        private float slideFov = 106f;
        private float wallJumpFov = 110f;

        private Vector2 scroll;
        private string status = "";
        private MessageType statusType = MessageType.Info;

        private Material snowDotMaterial;
        private Material snowFlakeMaterial;
        private Material speedLineMaterial;

        [MenuItem("Tools/Game Feel Setup")]
        private static void Open()
        {
            GetWindow<GameFeelSetupWindow>("Game Feel Setup");
        }

        private void OnEnable()
        {
            RefreshReferences();
        }

        private void RefreshReferences()
        {
            playerCamera = FindFirstObjectByType<PlayerCamera>();
            player = FindFirstObjectByType<PlayerMovement>();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("References", EditorStyles.boldLabel);
            playerCamera = (PlayerCamera)EditorGUILayout.ObjectField("Player Camera", playerCamera, typeof(PlayerCamera), true);
            player = (PlayerMovement)EditorGUILayout.ObjectField("Player Movement", player, typeof(PlayerMovement), true);
            if (GUILayout.Button("Find In Scene")) RefreshReferences();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Snow", EditorStyles.boldLabel);
            snowIntensity = EditorGUILayout.Slider("Intensity", snowIntensity, 0f, 2f);
            windStrength = EditorGUILayout.Slider("Wind Strength", windStrength, 0f, 6f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Environment", EditorStyles.boldLabel);
            fogDensity = EditorGUILayout.Slider("Fog Density", fogDensity, 0f, 0.05f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("FOV Punch", EditorStyles.boldLabel);
            dashFov = EditorGUILayout.Slider("Dash Peak FOV", dashFov, 90f, 140f);
            slideFov = EditorGUILayout.Slider("Slide Peak FOV", slideFov, 90f, 140f);
            wallJumpFov = EditorGUILayout.Slider("Wall Jump Peak FOV", wallJumpFov, 90f, 140f);

            EditorGUILayout.Space();
            if (GUILayout.Button("Set Up Everything", GUILayout.Height(34f))) Run(SetupAll);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Individual Steps", EditorStyles.boldLabel);
            if (GUILayout.Button("Generate Textures And Materials")) Run(EnsureAssets);
            if (GUILayout.Button("Add Speed Lines")) Run(AddSpeedLines);
            if (GUILayout.Button("Add Snowfall")) Run(AddSnowfall);
            if (GUILayout.Button("Add Winter Environment")) Run(AddEnvironment);
            if (GUILayout.Button("Apply FOV Punch Values")) Run(ApplyFov);

            if (!string.IsNullOrEmpty(status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(status, statusType);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Run(System.Action action)
        {
            RefreshIfMissing();
            action();
        }

        private void RefreshIfMissing()
        {
            if (playerCamera == null || player == null) RefreshReferences();
        }

        private void Report(string message)
        {
            status = message;
            statusType = MessageType.Info;
            Repaint();
        }

        private void Fail(string message)
        {
            status = message;
            statusType = MessageType.Warning;
            Repaint();
        }

        private void SetupAll()
        {
            if (playerCamera == null)
            {
                Fail("No PlayerCamera found in the open scene.");
                return;
            }

            EnsureAssets();
            AddSpeedLines();
            AddSnowfall();
            AddEnvironment();
            ApplyFov();
            Report("Everything is set up. Press Play to test.");
        }

        private static void EnsureFolder()
        {
            string absolute = Path.Combine(Application.dataPath, "_Project/Art/Effects");
            if (!Directory.Exists(absolute))
            {
                Directory.CreateDirectory(absolute);
                AssetDatabase.Refresh();
            }
        }

        private static Texture2D EnsureTexture(string fileName, System.Func<Texture2D> factory)
        {
            string path = OutputFolder + "/" + fileName + ".png";
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;

            Texture2D generated = factory();
            File.WriteAllBytes(path, generated.EncodeToPNG());
            DestroyImmediate(generated);
            AssetDatabase.ImportAsset(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material EnsureMaterial(string fileName, Texture2D texture, bool additive)
        {
            string path = OutputFolder + "/" + fileName + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Material material = VFXMaterials.CreateParticleMaterial(texture, additive, fileName);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private void EnsureAssets()
        {
            EnsureFolder();

            Texture2D dot = EnsureTexture("SnowDot", () => VFXTextures.CreateSoftDot(64, 0.02f, 1.6f));
            Texture2D flake = EnsureTexture("SnowFlake", () => VFXTextures.CreateSnowflake(128));
            Texture2D line = EnsureTexture("SpeedLine", () => VFXTextures.CreateSoftDot(64, 0.05f, 1.5f));

            snowDotMaterial = EnsureMaterial("SnowDotMat", dot, false);
            snowFlakeMaterial = EnsureMaterial("SnowFlakeMat", flake, false);
            speedLineMaterial = EnsureMaterial("SpeedLineMat", line, true);

            AssetDatabase.SaveAssets();
            Report("Textures and materials are ready in " + OutputFolder + ".");
        }

        private void EnsureMaterialsLoaded()
        {
            if (snowDotMaterial == null || snowFlakeMaterial == null || speedLineMaterial == null) EnsureAssets();
        }

        private static void SetReference(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = value;
            so.ApplyModifiedProperties();
        }

        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null) return;
            property.floatValue = value;
            so.ApplyModifiedProperties();
        }

        private static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        private void AddSpeedLines()
        {
            if (playerCamera == null)
            {
                Fail("No PlayerCamera found in the open scene.");
                return;
            }

            EnsureMaterialsLoaded();

            SpeedLines lines = playerCamera.GetComponent<SpeedLines>();
            if (lines == null) lines = Undo.AddComponent<SpeedLines>(playerCamera.gameObject);

            SetReference(lines, "player", player);
            SetReference(lines, "lineMaterial", speedLineMaterial);

            EditorUtility.SetDirty(lines);
            MarkSceneDirty();
            Report("Speed lines added to " + playerCamera.name + ".");
        }

        private void AddSnowfall()
        {
            EnsureMaterialsLoaded();

            SnowfallController snow = FindFirstObjectByType<SnowfallController>();
            if (snow == null)
            {
                GameObject go = new GameObject("Snowfall");
                Undo.RegisterCreatedObjectUndo(go, "Create Snowfall");
                snow = go.AddComponent<SnowfallController>();
            }

            Transform anchor = player != null ? player.transform : (playerCamera != null ? playerCamera.transform : null);
            SetReference(snow, "follow", anchor);
            SetReference(snow, "dotMaterial", snowDotMaterial);
            SetReference(snow, "flakeMaterial", snowFlakeMaterial);
            SetFloat(snow, "intensity", snowIntensity);
            SetFloat(snow, "windStrength", windStrength);

            EditorUtility.SetDirty(snow);
            MarkSceneDirty();
            Selection.activeObject = snow.gameObject;
            Report("Snowfall added and following the player.");
        }

        private void AddEnvironment()
        {
            WinterEnvironment environment = FindFirstObjectByType<WinterEnvironment>();
            if (environment == null)
            {
                GameObject go = new GameObject("WinterEnvironment");
                Undo.RegisterCreatedObjectUndo(go, "Create Winter Environment");
                environment = go.AddComponent<WinterEnvironment>();
            }

            Camera cam = playerCamera != null ? playerCamera.GetComponent<Camera>() : Camera.main;
            SetReference(environment, "targetCamera", cam);
            SetReference(environment, "sun", FindSun());
            SetFloat(environment, "fogDensity", fogDensity);

            environment.Apply();

            EditorUtility.SetDirty(environment);
            MarkSceneDirty();
            Report("Winter environment applied: fog, ambient light, camera background, and sun tint.");
        }

        private static Light FindSun()
        {
            if (RenderSettings.sun != null) return RenderSettings.sun;

            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                if (lights[i].type == LightType.Directional) return lights[i];
            }

            return null;
        }

        private void ApplyFov()
        {
            if (playerCamera == null)
            {
                Fail("No PlayerCamera found in the open scene.");
                return;
            }

            SetFloat(playerCamera, "dashPunchFov", dashFov);
            SetFloat(playerCamera, "slidePunchFov", slideFov);
            SetFloat(playerCamera, "wallJumpPunchFov", wallJumpFov);

            EditorUtility.SetDirty(playerCamera);
            MarkSceneDirty();
            Report("FOV punch values applied to the PlayerCamera.");
        }
    }
}
