using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlightPost.EditorTools
{
    /// <summary>
    /// 자동 플레이테스트용 Windows 빌드. 명령줄에서:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod MoonlightPost.EditorTools.PlaytestBuild.Build
    /// 빌드: Build/Playtest/MoonlightPost.exe → 실행: MoonlightPost.exe -playtest 결과폴더
    /// 씬은 빈 씬 하나면 된다(GameBootstrap 이 월드를 코드로 만든다). 임시 씬은 빌드 후 지운다.
    /// </summary>
    public static class PlaytestBuild
    {
        const string TempDir = "Assets/_PlaytestTemp";
        const string ScenePath = TempDir + "/Playtest.unity";

        [MenuItem("달빛 우체국/자동 플레이테스트 빌드")]
        public static void Build()
        {
            Directory.CreateDirectory(TempDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = "Build/Playtest/MoonlightPost.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development,
                });
                Debug.Log("[playtest] 빌드 결과: " + report.summary.result + ", 오류 " + report.summary.totalErrors);
                if (Application.isBatchMode && report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    EditorApplication.Exit(1);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempDir);
            }
        }
    }
}
