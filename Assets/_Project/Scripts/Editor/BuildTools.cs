using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.Linq;

namespace ElementalSiege.Editor
{
    /// <summary>
    /// Build automation menu items for Elemental Siege.
    /// Provides one-click builds for iOS, Mac, and All platforms.
    /// </summary>
    public static class BuildTools
    {
        private const string CompanyName = "ElementalSiegeStudio";
        private const string ProductName = "ElementalSiege";
        private const string BundleIdentifier = "com.elementalsiege.game";
        private const string BuildRoot = "Builds";

        // ── Menu items ───────────────────────────────────────────────

        [MenuItem("Elemental Siege/Build/iOS", false, 100)]
        public static void BuildiOS()
        {
            ConfigureCommonSettings();
            ConfigureiOS();

            string outputPath = System.IO.Path.Combine(BuildRoot, "iOS");
            EnsureDirectory(outputPath);

            var report = PerformBuild(BuildTarget.iOS, outputPath);
            PostBuild(report, outputPath);
        }

        [MenuItem("Elemental Siege/Build/Mac", false, 101)]
        public static void BuildMac()
        {
            ConfigureCommonSettings();
            ConfigureMac();

            string outputPath = System.IO.Path.Combine(BuildRoot, "Mac",
                ProductName + ".app");
            EnsureDirectory(System.IO.Path.Combine(BuildRoot, "Mac"));

            var report = PerformBuild(BuildTarget.StandaloneOSX, outputPath);
            PostBuild(report, System.IO.Path.Combine(BuildRoot, "Mac"));
        }

        [MenuItem("Elemental Siege/Build/All", false, 200)]
        public static void BuildAll()
        {
            Debug.Log("[BuildTools] Starting full build (iOS + Mac)...");
            BuildiOS();
            BuildMac();
            Debug.Log("[BuildTools] All builds complete.");
        }

        // ── Configuration ────────────────────────────────────────────

        private static void ConfigureCommonSettings()
        {
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.productName = ProductName;

#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.iOS, BundleIdentifier);
            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Standalone, BundleIdentifier);
#else
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.iOS, BundleIdentifier);
            PlayerSettings.SetApplicationIdentifier(
                BuildTargetGroup.Standalone, BundleIdentifier);
#endif
        }

        private static void ConfigureiOS()
        {
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.iOS,
                ScriptingImplementation.IL2CPP);
#else
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.iOS,
                ScriptingImplementation.IL2CPP);
#endif
            PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
            PlayerSettings.iOS.targetOSVersionString = "15.0";
        }

        private static void ConfigureMac()
        {
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Standalone,
                ScriptingImplementation.Mono2x);
            PlayerSettings.SetArchitecture(
                UnityEditor.Build.NamedBuildTarget.Standalone, 2); // Universal
#else
            PlayerSettings.SetScriptingBackend(
                BuildTargetGroup.Standalone,
                ScriptingImplementation.Mono2x);
#endif
        }

        // ── Build execution ──────────────────────────────────────────

        private static BuildReport PerformBuild(BuildTarget target, string outputPath)
        {
            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                Debug.LogError("[BuildTools] No scenes in Build Settings! " +
                    "Add scenes via File > Build Profiles.");
                return null;
            }

            Debug.Log($"[BuildTools] Building {target} to '{outputPath}' " +
                      $"with {scenes.Length} scene(s)...");

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            return report;
        }

        // ── Post-build ───────────────────────────────────────────────

        private static void PostBuild(BuildReport report, string outputFolder)
        {
            if (report == null)
                return;

            if (report.summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[BuildTools] Build succeeded! " +
                    $"Size: {report.summary.totalSize / (1024 * 1024):F1} MB, " +
                    $"Time: {report.summary.totalTime.TotalSeconds:F1}s");

                // Open output folder
                string fullPath = System.IO.Path.GetFullPath(outputFolder);
                EditorUtility.RevealInFinder(fullPath);
            }
            else
            {
                Debug.LogError($"[BuildTools] Build FAILED: {report.summary.result}. " +
                    $"Errors: {report.summary.totalErrors}");
            }
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static void EnsureDirectory(string path)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
        }
    }
}
