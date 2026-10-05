using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal abstract class ProjectExporter {
    internal void Export(BuildPlayerOptions buildPlayerOptions, List<string> precheckWarnings)
    {        
        // This executes the build:
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result != BuildResult.Succeeded) {
            Debug.LogError("Building project for Flutter failed");
            
            if (Application.isBatchMode) {

                // throwing an exception shows an error on the command line, exit(1) doesn't.
                throw new System.Exception("Building project for Flutter failed");
                // EditorApplication.Exit(1);
            }
        }
        else {
            TransformExportedProject(buildPlayerOptions.locationPathName);

            // Debug.Log doesn't work until after BuildPipeline.BuildPlayer has executed
            foreach(var log in precheckWarnings) {
                Debug.LogWarning(log);
            }
            Debug.Log($"Building project for Flutter succeeded");

            if (Application.isBatchMode) {
                EditorApplication.Exit(0);
            }
        }
    }

    protected abstract void TransformExportedProject(string exportPath);
}