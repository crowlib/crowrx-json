using System;
using System.Linq;
using CrowRx.Json.UnitySmoke;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UnitySmokeBuild
{
    public static void BuildMono() => Build(ScriptingImplementation.Mono2x);
    public static void BuildIl2Cpp() => Build(ScriptingImplementation.IL2CPP);

    private static void Build(ScriptingImplementation backend)
    {
        string output = GetArgument("-smokeOutput");
        if (string.IsNullOrEmpty(output))
        {
            throw new ArgumentException("Missing -smokeOutput argument.");
        }

        NamedBuildTarget target = NamedBuildTarget.Standalone;
        PlayerSettings.SetScriptingBackend(target, backend);
        PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
        PlayerSettings.SetApiCompatibilityLevel(target, ApiCompatibilityLevel.NET_Standard);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("CrowRx.Json Unity Smoke").AddComponent<UnitySmokeBehaviour>();
        const string scenePath = "Assets/GeneratedUnitySmoke.unity";
        EditorSceneManager.SaveScene(scene, scenePath);

        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.CleanBuildCache,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException($"Unity {backend} smoke build failed: {report.summary.result}");
        }

        Debug.Log($"CROWRX_JSON_UNITY_BUILD_OK backend={backend} size={report.summary.totalSize}");
    }

    private static string GetArgument(string name)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, name);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : string.Empty;
    }
}
