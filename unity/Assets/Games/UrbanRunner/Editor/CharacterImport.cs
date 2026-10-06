using UnityEditor;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Import rules for the courier model (Kenney "Animated Characters: Protagonists", CC0):
    /// one Generic rig, animations taken from the separate run/jump/idle FBX files, no imported materials.
    /// </summary>
    public sealed class CharacterImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Games/UrbanRunner/Art/Characters/Kenney/";
        public const string ModelPath = Folder + "characterMedium.fbx";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var m = (ModelImporter)assetImporter;
            m.animationType = ModelImporterAnimationType.Generic;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            // Same skeleton in every file, so Generic clips bind by bone path; no shared avatar needed.
            m.importAnimation = assetPath.Contains("/Animations/");
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(Folder + "Animations/")) return;
            var m = (ModelImporter)assetImporter;
            // Each file holds a one-frame "Targeting Pose" take plus the real one: keep only the real one.
            var clips = System.Array.FindAll(m.defaultClipAnimations, c => !c.takeName.Contains("Targeting"));
            var loop = !assetPath.EndsWith("jump.fbx");
            for (var i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                clips[i].name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            }
            m.clipAnimations = clips;
        }
    }
}
