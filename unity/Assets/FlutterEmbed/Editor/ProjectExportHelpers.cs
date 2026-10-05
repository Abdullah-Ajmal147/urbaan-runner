using System.IO;
using UnityEditor;
using UnityEngine;

internal class ProjectExportHelpers
{
    static internal void ShowErrorMessage(string errorMessage)
    {
        if (!Application.isBatchMode) {
            EditorUtility.DisplayDialog(
                            "Export incomplete",
                            errorMessage,
                            "Okay");
        } else {
            // We don't have any UI to help the user, abort the export.
            throw new System.Exception(errorMessage);
        }
    }

    static internal void MoveContentsOfDirectory(DirectoryInfo from, DirectoryInfo to)
    {
        Directory.CreateDirectory(to.FullName);

        // Copy each file into the new directory.
        foreach (FileInfo fi in from.GetFiles())
        {
            fi.MoveTo(Path.Combine(to.FullName, fi.Name));
        }

        // Copy each subdirectory using recursion.
        foreach (DirectoryInfo diSourceSubDir in from.GetDirectories())
        {
            DirectoryInfo nextTargetSubDir =
                to.CreateSubdirectory(diSourceSubDir.Name);
            MoveContentsOfDirectory(diSourceSubDir, nextTargetSubDir);
        }
    }
}