using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UrbanRunner.Editor;
using UrbanRunner.Game;

namespace Tests.EditMode
{
    public class ProjectSetupTests
    {
        /// <summary>Regression: the first generated Run scene had null GameAssets/ChunkLibrary references,
        /// which crashed GameRoot.Awake on load.</summary>
        [Test]
        public void RunScene_GameRootReferencesAreSet()
        {
            EditorSceneManager.OpenScene(UrbanRunnerProjectSetup.RunScenePath, OpenSceneMode.Single);
            var root = Object.FindAnyObjectByType<GameRoot>();
            Assert.IsNotNull(root);
            var so = new SerializedObject(root);
            foreach (var prop in new[] { "assets", "chunks", "mainCamera" })
                Assert.IsNotNull(so.FindProperty(prop).objectReferenceValue, prop);
            Assert.IsNotNull(Object.FindAnyObjectByType<GameFactory.Core.Bridge.FlutterBridge>());
            Assert.IsNotNull(Object.FindAnyObjectByType<StandaloneHost>());
        }

        [Test]
        public void GameAssets_AllMaterialsAndFontAssigned()
        {
            var a = AssetDatabase.LoadAssetAtPath<GameAssets>(UrbanRunnerProjectSetup.GameAssetsPath);
            Assert.IsNotNull(a);
            var fields = typeof(GameAssets).GetFields().Where(f => typeof(Object).IsAssignableFrom(f.FieldType));
            foreach (var f in fields) Assert.IsNotNull(f.GetValue(a) as Object, f.Name);
        }

        [Test]
        public void BuildSettings_ContainOnlyTheRunScene()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            CollectionAssert.AreEqual(new[] { UrbanRunnerProjectSetup.RunScenePath }, scenes);
        }

        [Test]
        public void AndroidSettings_MatchHandover()
        {
            var android = UnityEditor.Build.NamedBuildTarget.Android;
            Assert.AreEqual(ScriptingImplementation.IL2CPP, PlayerSettings.GetScriptingBackend(android));
            Assert.AreEqual(36, (int)PlayerSettings.Android.targetSdkVersion);
            Assert.GreaterOrEqual((int)PlayerSettings.Android.minSdkVersion, 23);
            Assert.IsTrue(PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARM64));
            Assert.IsTrue(PlayerSettings.Android.targetArchitectures.HasFlag(AndroidArchitecture.ARMv7));
            CollectionAssert.AreEqual(new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 }, PlayerSettings.GetGraphicsAPIs(BuildTarget.Android));
            Assert.AreEqual(UIOrientation.Portrait, PlayerSettings.defaultInterfaceOrientation);
            Assert.AreEqual("0.1.0", PlayerSettings.bundleVersion);
        }

        /// <summary>Regression (negative test N1, D30): with Unity's predictive-back support on, the embedded Unity
        /// view swallowed the Back gesture on Android 16 and neither side paused the run.</summary>
        [Test]
        public void PredictiveBack_IsLeftToFlutter()
        {
            Assert.IsFalse(PlayerSettings.Android.predictiveBackSupport);
        }

        /// <summary>D33: the owner saw Unity's splash (with the app's spinner on top of it) on a real phone.</summary>
        [Test]
        public void UnitySplash_IsOff()
        {
            Assert.IsFalse(PlayerSettings.SplashScreen.show);
        }
    }
}
