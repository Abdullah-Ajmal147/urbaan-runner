using UnityEditor;
using UnityEngine;

public class ProjectExporterBatchmode
{
    private static ProjectExportChecker projectExportChecker = new ProjectExportChecker();

    // Functions to export from the command line using Unity batchmode.
    //
    // It is advised to run an export from the Unity Editor first, to make sure your project settings are correct.
    // Checks that open dialogs in the Unity Editor will simply fail on the command line.
    //
    // Make sure to use -quit in your command, to ensure the headless Unity engine always terminates in the end.
    //
    // Unity documentation https://docs.unity3d.com/2022.3/Documentation/Manual/EditorCommandLineArguments.html





    // Export command for Android & iOS. The specific platform is based on `-buildTarget`.
    // -buildTarget Android
    // -buildTarget iOS
    //
    // <unity path> -projectPath <unity project path> -batchmode -buildTarget Android -executeMethod ProjectExporterBatchmode.ExportProject -exportPath <output path> -quit

    public static void ExportProject()
    {
        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

        if (target == BuildTarget.Android)
        {
            ExportProjectAndroid();
        } 
        else if ( target == BuildTarget.iOS)
        {
            ExportProjectIos();
        }
        else
        {
            throw new System.Exception("Invalid buildtarget during batchmode export.");
        }
    }


    public static void ExportProjectAndroid()
    {
        if (!Application.isBatchMode)
        {
            return;
        }

        Debug.Log("Exporting Android project in batchmode.");
      
        ProjectExportCheckerResult result = projectExportChecker.PreCheckAndroid();

#if UNITY_ANDROID
        if (result.IsSuccessful)
        {
            new ProjectExporterAndroid().Export(result.BuildPlayerOptions, result.PrecheckWarnings);
        } else
        {
            throw new System.Exception("Android PreBuid checks failed.");
        }
#else
        throw new System.Exception("Build platform is not Android.");
#endif

    }

    // public so this function can be called directly from the command line.
    public static void ExportProjectIos()
    {

        if (!Application.isBatchMode)
        {
            return;
        }

        Debug.Log("Exporting iOS project in batchmode.");

        
        // Using UNITY_IOS preprocessor because 'using UnityEditor.iOS.Xcode' is only available with iOS build tools
        ProjectExportCheckerResult result = projectExportChecker.PreCheckIos();
#if UNITY_IOS
        if(result.IsSuccessful) {
            new ProjectExporterIos().Export(result.BuildPlayerOptions, result.PrecheckWarnings);
        } else
        {
            throw new System.Exception("iOS PreBuid checks failed.");
        }
#else
        throw new System.Exception("Build platform is not iOS.");
#endif
        
    }


    // Get the build destination (unityLibrary directory) from the comand line argument -exportPath
    public static string GetExportPath()
    {
        return GetArg("-exportPath");
    }

    private static string GetArg(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == name && args.Length > i + 1)
            {
                return args[i + 1];
            }
        }
        return null;
    }
}
