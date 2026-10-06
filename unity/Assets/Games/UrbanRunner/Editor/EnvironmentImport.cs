using UnityEditor;

namespace UrbanRunner.Editor
{
    /// <summary>
    /// Kenney city models: readable meshes (needed to combine each street segment into static batches at load),
    /// no imported materials (the toon material is assigned in code), no animation.
    /// </summary>
    public sealed class EnvironmentImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Games/UrbanRunner/Art/Environment/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var m = (ModelImporter)assetImporter;
            m.isReadable = true;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
        }
    }
}
