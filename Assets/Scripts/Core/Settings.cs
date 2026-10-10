using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 플레이어 설정(음량, 화면 흔들림, 조작법 안내). PlayerPrefs 에 저장되어 저장 파일(진행 상황)과 따로 남는다.
    /// 전체 화면 여부는 Unity 가 스스로 기억한다.
    /// </summary>
    public static class Settings
    {
        const string MusicKey = "settings.music", SfxKey = "settings.sfx", ShakeKey = "settings.shake", HelpKey = "settings.help";

        /// <summary>배경음악 음량 0~1.</summary>
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.7f);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        }

        /// <summary>효과음 음량 0~1.</summary>
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SfxKey, 0.8f);
            set => PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value));
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set => PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
        }

        /// <summary>게임을 시작할 때 조작법 안내를 보여 줄지.</summary>
        public static bool ShowHelp
        {
            get => PlayerPrefs.GetInt(HelpKey, 1) == 1;
            set => PlayerPrefs.SetInt(HelpKey, value ? 1 : 0);
        }

        public static bool Fullscreen
        {
            get => Screen.fullScreen;
            set
            {
                if (value == Screen.fullScreen) return;
                if (value) Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
                else Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            }
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
