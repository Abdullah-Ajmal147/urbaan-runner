using System;
using System.IO;
using System.Linq;
using GameFactory.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Batchmode entry points used by tools/build_all (HANDOVER §8.3). Each exits non-zero on failure.
///   -executeMethod BuildScripts.Configure
///   -executeMethod BuildScripts.BuildStandaloneEmulatorApk [-outputPath build/standalone/x.apk]
///   -executeMethod BuildScripts.ExportForFlutter -exportPath app/android/unityLibrary [-emulator]
/// </summary>
public static class BuildScripts
{
    public static void Configure() => Run(() => AndroidProjectConfigurator.ApplyAll());

    public static void BuildStandaloneEmulatorApk() => Run(() =>
    {
        AndroidProjectConfigurator.ApplyAll();
        AndroidProjectConfigurator.SetArchitectures(emulator: true);
        EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        EditorUserBuildSettings.buildAppBundle = false;
        var output = Arg("-outputPath") ?? "../build/standalone/UrbanRunner-standalone-emulator.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            target = BuildTarget.Android,
            locationPathName = output,
            options = HasFlag("-development") ? BuildOptions.Development : BuildOptions.None,
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("standalone build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
        Debug.Log("UR_GAME standalone apk: " + Path.GetFullPath(output));
    });

    /// <summary>Exports unityLibrary for the Flutter app via the flutter_embed_unity exporter.</summary>
    public static void ExportForFlutter() => Run(() =>
    {
        if (Arg("-exportPath") == null) throw new Exception("-exportPath is required");
        AndroidProjectConfigurator.ApplyAll();
        AndroidProjectConfigurator.SetArchitectures(emulator: HasFlag("-emulator"));
        EditorUserBuildSettings.exportAsGoogleAndroidProject = true;
        EditorUserBuildSettings.development = HasFlag("-development");
        ProjectExporterBatchmode.ExportProjectAndroid();
        var exported = Path.Combine(Arg("-exportPath"), "build.gradle");
        if (!File.Exists(exported)) throw new Exception("export produced no " + exported);
    });

    static void Run(Action action)
    {
        try
        {
            action();
            AssetDatabase.SaveAssets();
            // tools/env.sh reads this marker: Unity sometimes hangs during shutdown after this point.
            Debug.Log("UR_BATCH_EXIT 0");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogError("UR_ERROR build step failed: " + e);
            Debug.Log("UR_BATCH_EXIT 1");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    static bool HasFlag(string name) => Environment.GetCommandLineArgs().Contains(name);
}
