using System.IO;
using GameFactory.Core.Bridge;
using GameFactory.Core.Input;
using GameFactory.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UrbanRunner.Spike;

namespace UrbanRunner.Editor
{
    /// <summary>Urban Runner's values for the shared configurator, plus generated scenes.</summary>
    public sealed class UrbanRunnerProjectSetup : IProjectSetup
    {
        public const string SpikeScenePath = "Assets/Games/UrbanRunner/Scenes/Spike.unity";
        const string MaterialDir = "Assets/SharedAssets/Materials";

        public GameProjectConfig Config { get; } = new GameProjectConfig
        {
            CompanyName = "GameFactory",
            ProductName = "Urban Runner",
            ApplicationId = "com.gamefactory.urbanrunner", // placeholder until owner task 4
            Scenes = new[] { SpikeScenePath },
        };

        [MenuItem("GameFactory/Apply Project Settings")]
        static void MenuApply() => AndroidProjectConfigurator.ApplyAll();

        public void GenerateAssets()
        {
            if (!File.Exists(SpikeScenePath)) CreateSpikeScene();
        }

        static Material LitMaterial(string name, Color color)
        {
            Directory.CreateDirectory(MaterialDir);
            var path = $"{MaterialDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void CreateSpikeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0x0E, 0x12, 0x30, 0xFF);
            camGo.transform.position = new Vector3(0, 0.5f, -5f);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(0x1A, 0x20, 0x50, 0xFF);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "SpikeCube";
            cube.transform.position = new Vector3(0, 0.8f, 0);
            cube.transform.localScale = Vector3.one * 1.5f;
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = LitMaterial("SpikeCube", new Color32(0xFF, 0x3D, 0xA6, 0xFF));
            Object.DestroyImmediate(cube.GetComponent<Collider>());

            new GameObject(FlutterBridge.ObjectName).AddComponent<FlutterBridge>();

            var spikeGo = new GameObject("Spike");
            var swipes = spikeGo.AddComponent<PointerSwipeSource>();
            var spike = spikeGo.AddComponent<SpikeController>();
            var so = new SerializedObject(spike);
            so.FindProperty("cube").objectReferenceValue = cube.transform;
            so.FindProperty("cubeRenderer").objectReferenceValue = renderer;
            so.FindProperty("swipes").objectReferenceValue = swipes;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(SpikeScenePath));
            EditorSceneManager.SaveScene(scene, SpikeScenePath);
        }
    }
}
