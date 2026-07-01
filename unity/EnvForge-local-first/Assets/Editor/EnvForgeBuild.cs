using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EnvForge.Editor
{
    public static class EnvForgeBuild
    {
        private const string BuildOutputArgument = "-envforgeBuildOutput";
        private const string DefaultBuildOutputPath = "artifacts/builds/windows/EnvForge.exe";

        public static void BuildWindows()
        {
            var outputPath = GetArgumentValue(BuildOutputArgument) ?? DefaultBuildOutputPath;
            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                throw new InvalidOperationException("No enabled scenes are configured in EditorBuildSettings.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"EnvForge Windows build failed with result {summary.result} and {summary.totalErrors} errors.");
            }

            Debug.Log(
                $"EnvForge Windows build succeeded: {outputPath} ({summary.totalSize} bytes, {summary.totalTime}).");
        }

        private static string GetArgumentValue(string argumentName)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], argumentName, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }
    }
}
