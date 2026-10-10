using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonlightPost.EditorTools
{
    /// <summary>
    /// 배포용 Windows 빌드. 메뉴 「달빛 우체국 > 배포용 빌드 (Windows)」 또는 명령줄에서:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod MoonlightPost.EditorTools.ReleaseBuild.Build
    /// 결과: Build/Release/MoonlightPost/ (실행 파일과 데이터) 와 이를 묶은 Build/MoonlightPost_v버전_Windows.zip.
    /// 개발 빌드가 아니므로 화면 구석의 "Development Build" 표시와 개발용 키(F11·F12)가 빠진다.
    /// </summary>
    public static class ReleaseBuild
    {
        public const string Version = "0.3.0";
        const string TempDir = "Assets/_ReleaseTemp";
        const string ScenePath = TempDir + "/Main.unity";
        const string OutDir = "Build/Release/MoonlightPost";
        const string IconPath = "Assets/Editor/AppIcon.png";

        [MenuItem("달빛 우체국/배포용 빌드 (Windows)")]
        public static void Build()
        {
            ApplyPlayerSettings();

            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            Directory.CreateDirectory(TempDir);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = OutDir + "/MoonlightPost.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                AssetDatabase.DeleteAsset(TempDir);
            }

            bool ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log("[release] 빌드 결과: " + report.summary.result + ", 오류 " + report.summary.totalErrors);
            if (ok) Package();
            if (Application.isBatchMode && !ok) EditorApplication.Exit(1);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "BSH0401";
            PlayerSettings.productName = "달빛 우체국";
            PlayerSettings.bundleVersion = Version;
            // 처음에는 1280x720 창 모드로 열고, 창 크기는 바꿀 수 있다. 전체 화면은 게임 안 설정에서 켠다.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SplashScreen.show = false;

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Standalone, new[] { icon }, IconKind.Application);
        }

        /// <summary>빌드 폴더에 안내 파일을 넣고 zip 으로 묶는다(디버그용 파일은 뺀다).</summary>
        static void Package()
        {
            foreach (var dir in Directory.GetDirectories(OutDir, "*DoNotShip*"))
                Directory.Delete(dir, true);
            File.WriteAllText(Path.Combine(OutDir, "읽어보기.txt"), ReadMe, new System.Text.UTF8Encoding(true));

            string zip = "Build/MoonlightPost_v" + Version + "_Windows.zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(OutDir, zip, System.IO.Compression.CompressionLevel.Optimal, true);
            Debug.Log("[release] 묶음: " + Path.GetFullPath(zip));
        }

        const string ReadMe =
@"달빛 우체국 (Moonlight Post Office) v" + Version + @"

밤마다 달라지는 섬을 탐험하며 편지를 배달하는 2D 탑다운 액션 RPG.

[실행]
MoonlightPost.exe 를 실행한다. 압축을 푼 폴더 안의 다른 파일(MoonlightPost_Data 등)도 함께 두어야 한다.
처음에는 창 모드로 열린다. 전체 화면은 Esc → 설정에서 켠다.

[조작]
WASD / 방향키      이동
마우스 왼쪽 / J     공격 (3번 이어 치면 강타)
Space / Shift       회피
마우스 오른쪽 / K   우편가방 막기 (맞기 직전에 누르면 받아치기)
E                   조사 · 대화 · 배달
Q / C               편지 도구 사용 / 바꾸기
R                   크루아상 먹기 (체력 회복)
I                   가방
Tab / M             지도
F1                  조작법 보이기/숨기기
Esc                 메뉴 (설정 · 타이틀로 · 저장하고 종료)

[저장]
편지를 받거나 배달할 때 자동으로 저장된다.
저장 위치: %USERPROFILE%\AppData\LocalLow\BSH0401\달빛 우체국

[만든 사람과 에셋]
그림·소리: Ninja Adventure Asset Pack — Pixel-boy & AAA (CC0)
글꼴: Noto Sans KR (SIL Open Font License)
";
    }
}
