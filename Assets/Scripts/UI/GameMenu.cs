using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 타이틀 화면, 일시정지 메뉴(Esc), 설정, 화면 전환 페이드.
    /// 처음 실행하면 타이틀이 마을 위에 겹쳐 뜬다. 메뉴가 열려 있는 동안 게임은 멈춘다.
    /// 마우스(클릭·끌기) 또는 키보드(W/S·방향키로 고르기, A/D로 값 바꾸기, Enter·Space·E로 결정)로 조작한다.
    /// </summary>
    public class GameMenu : MonoBehaviour
    {
        public static GameMenu I { get; private set; }
        public static bool IsOpen => I != null && I.page != Page.None;
        public static bool TitleOpen => I != null && (I.page == Page.Title || I.page == Page.ConfirmNew || (I.page == Page.Settings && I.settingsBack == Page.Title));
        /// <summary>메뉴가 닫힌 프레임(같은 키 입력이 게임 조작으로 이어지지 않게 막는다).</summary>
        public static int LastClosedFrame { get; private set; } = -1;
        /// <summary>자동 플레이테스트처럼 타이틀 없이 바로 시작할 때 true.</summary>
        public static bool SkipTitle;
        static bool titleShownOnce;

        enum Page { None, Title, Pause, Settings, ConfirmNew }

        struct Item
        {
            public string label;
            public Action activate;
            /// <summary>값 항목(음량): 현재 값과 바꾸는 함수. null 이면 버튼.</summary>
            public Func<float> value;
            public Action<float> setValue;
        }

        Page page = Page.None;
        Page settingsBack;
        int selected;
        readonly List<Item> items = new List<Item>();
        GUIStyle bigTitle, subTitle, itemStyle, hintStyle;
        float fadeAlpha;
        bool fading;
        int draggingSlider = -1;

        void Awake() => I = this;

        void Start()
        {
            if (!SkipTitle && !titleShownOnce)
            {
                titleShownOnce = true;
                Open(Page.Title);
            }
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ 열고 닫기

        void Open(Page p)
        {
            page = p;
            selected = 0;
            draggingSlider = -1;
            Time.timeScale = 0f;
            BuildItems();
        }

        void Close()
        {
            page = Page.None;
            LastClosedFrame = Time.frameCount;
            Settings.Save();
            Time.timeScale = HUD.MapOpen || HUD.BagOpen ? 0f : 1f;
        }

        static bool HasProgress => GameState.Night > 0;

        // 자동 플레이테스트의 스크린샷용
        public static void ShowTitle() => I?.Open(Page.Title);
        public static void ShowPause() => I?.Open(Page.Pause);
        public static void ShowSettings()
        {
            if (I == null) return;
            I.settingsBack = Page.Pause;
            I.Open(Page.Settings);
        }
        public static void Hide() => I?.Close();

        void BuildItems()
        {
            items.Clear();
            switch (page)
            {
                case Page.Title:
                    if (HasProgress) items.Add(Button("이어하기", Close));
                    items.Add(Button(HasProgress ? "새 게임" : "시작하기", () =>
                    {
                        if (HasProgress) Open(Page.ConfirmNew);
                        else Close();
                    }));
                    items.Add(Button("설정", () => OpenSettings(Page.Title)));
                    items.Add(Button("종료", Quit));
                    break;
                case Page.Pause:
                    items.Add(Button("계속하기", Close));
                    items.Add(Button("설정", () => OpenSettings(Page.Pause)));
                    items.Add(Button("타이틀로", () =>
                    {
                        GameState.Save();
                        Open(Page.Title);
                    }));
                    items.Add(Button("저장하고 종료", Quit));
                    break;
                case Page.Settings:
                    items.Add(Slider("배경음악", () => Settings.MusicVolume, v => Settings.MusicVolume = v));
                    items.Add(Slider("효과음", () => Settings.SfxVolume, v =>
                    {
                        Settings.SfxVolume = v;
                        Sound.Play("Talk", 0.8f);
                    }));
                    items.Add(Toggle("화면 흔들림", () => Settings.ScreenShake, v => Settings.ScreenShake = v));
                    items.Add(Toggle("조작법 안내", () => Settings.ShowHelp, v =>
                    {
                        Settings.ShowHelp = v;
                        HUD.SetHelp(v);
                    }));
                    items.Add(Toggle("전체 화면", () => Settings.Fullscreen, v => Settings.Fullscreen = v));
                    items.Add(Button("돌아가기", () => Open(settingsBack)));
                    break;
                case Page.ConfirmNew:
                    items.Add(Button("새로 시작하기", () =>
                    {
                        Close();
                        GameBootstrap.RestartNewGame();
                    }));
                    items.Add(Button("취소", () => Open(Page.Title)));
                    break;
            }
        }

        void OpenSettings(Page back)
        {
            settingsBack = back;
            Open(Page.Settings);
        }

        static Item Button(string label, Action activate) => new Item { label = label, activate = activate };

        static Item Slider(string label, Func<float> get, Action<float> set) => new Item { label = label, value = get, setValue = set };

        static Item Toggle(string label, Func<bool> get, Action<bool> set) =>
            new Item { label = label, activate = () => set(!get()), value = () => get() ? 1f : 0f, setValue = v => set(v > 0.5f) };

        static void Quit()
        {
            GameState.Save();
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ 키보드

        void Update()
        {
            if (fading || UpgradeMenu.IsOpen) return;
            if (page == Page.None)
            {
                if (!GameInput.PausePressed) return;
                // Esc: 지도·가방이 열려 있으면 그것부터 닫고, 아니면 일시정지 메뉴.
                if (HUD.MapOpen || HUD.BagOpen) HUD.CloseOverlays();
                else Open(Page.Pause);
                return;
            }

            if (GameInput.PausePressed)
            {
                if (page == Page.Pause) Close();
                else if (page == Page.Settings) Open(settingsBack);
                else if (page == Page.ConfirmNew) Open(Page.Title);
                return;
            }
            if (items.Count == 0) return;
            if (GameInput.MenuUp) Select(selected - 1);
            if (GameInput.MenuDown) Select(selected + 1);
            var item = items[selected];
            if (item.setValue != null && item.activate == null)
            {
                if (GameInput.MenuLeft) item.setValue(item.value() - 0.1f);
                if (GameInput.MenuRight) item.setValue(item.value() + 0.1f);
            }
            else if (item.setValue != null && (GameInput.MenuLeft || GameInput.MenuRight)) item.activate();
            if (GameInput.MenuConfirm && item.activate != null) Activate(item);
        }

        void Select(int i)
        {
            int next = (i + items.Count) % items.Count;
            if (next != selected) Sound.Play("Talk", 0.3f);
            selected = next;
        }

        void Activate(Item item)
        {
            Sound.Play("Clue", 0.5f);
            item.activate();
        }

        // ------------------------------------------------------------------ 화면 전환 페이드

        /// <summary>화면을 어둡게 했다가 mid 를 실행하고 다시 밝힌다(빠른 이동 등).</summary>
        public static void FadeThrough(Action mid)
        {
            if (I == null)
            {
                mid?.Invoke();
                return;
            }
            I.StartCoroutine(I.Fade(mid));
        }

        IEnumerator Fade(Action mid)
        {
            fading = true;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.35f)
            {
                fadeAlpha = t;
                yield return null;
            }
            fadeAlpha = 1f;
            mid?.Invoke();
            yield return null;
            for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.45f)
            {
                fadeAlpha = t;
                yield return null;
            }
            fadeAlpha = 0f;
            fading = false;
        }

        // ------------------------------------------------------------------ 그리기

        void EnsureStyles()
        {
            if (bigTitle != null && bigTitle.fontSize == Ui.Px(64)) return;
            bigTitle = new GUIStyle(Ui.Title) { fontSize = Ui.Px(64), alignment = TextAnchor.MiddleCenter };
            subTitle = new GUIStyle(Ui.Small) { fontSize = Ui.Px(20), alignment = TextAnchor.MiddleCenter, wordWrap = false };
            subTitle.normal.textColor = new Color(0.8f, 0.85f, 1f, 0.85f);
            itemStyle = new GUIStyle(Ui.Text) { alignment = TextAnchor.MiddleCenter, wordWrap = false, fontSize = Ui.Px(22) };
            hintStyle = new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, wordWrap = false };
            hintStyle.normal.textColor = new Color(0.7f, 0.74f, 0.9f, 0.8f);
        }

        void OnGUI()
        {
            GUI.depth = -100; // 다른 화면(HUD·대화창)보다 위에 그린다
            if (page != Page.None)
            {
                Ui.Ensure();
                EnsureStyles();
                DrawMenu();
            }
            if (fadeAlpha > 0f) Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.01f, 0.04f, fadeAlpha));
        }

        void DrawMenu()
        {
            float s = Ui.S;
            bool title = TitleOpen;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.01f, 0.02f, 0.06f, title ? 0.55f : 0.7f));

            float cx = Screen.width * 0.5f;
            float top;
            if (title)
            {
                // 달과 제목
                var moon = Art.Glow != null ? Art.Glow.texture : null;
                if (moon != null)
                {
                    var old = GUI.color;
                    GUI.color = new Color(1f, 0.92f, 0.65f, 0.35f);
                    GUI.DrawTexture(new Rect(cx - 220 * s, 40 * s, 440 * s, 300 * s), moon);
                    GUI.color = new Color(1f, 0.96f, 0.82f, 0.9f);
                    GUI.DrawTexture(new Rect(cx - 36 * s, 96 * s, 72 * s, 72 * s), moon);
                    GUI.color = old;
                }
                Ui.Shadow(new Rect(0, 150 * s, Screen.width, 90 * s), "달빛 우체국", bigTitle);
                GUI.Label(new Rect(0, 236 * s, Screen.width, 30 * s), "Moonlight Post Office", subTitle);
                top = 310 * s;
            }
            else
            {
                Ui.Shadow(new Rect(0, 90 * s, Screen.width, 60 * s), page == Page.Settings ? "설정" : "일시정지", new GUIStyle(bigTitle) { fontSize = Ui.Px(40) });
                top = 180 * s;
            }

            if (page == Page.ConfirmNew)
            {
                GUI.Label(new Rect(0, top - 8 * s, Screen.width, 34 * s), "저장된 진행 상황이 지워집니다. 처음부터 시작할까요?", itemStyle);
                top += 44 * s;
            }

            float w = (page == Page.Settings ? 460 : 300) * s, h = 46 * s, gap = 10 * s;
            var e = Event.current;
            for (int i = 0; i < items.Count; i++)
            {
                var r = new Rect(cx - w * 0.5f, top + i * (h + gap), w, h);
                var item = items[i];
                bool isSlider = item.setValue != null && item.activate == null;

                if (e.type == EventType.MouseMove && r.Contains(e.mousePosition)) selected = i;
                if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
                {
                    selected = i;
                    if (isSlider) draggingSlider = i;
                    else Activate(item);
                    e.Use();
                }

                Ui.Panel(r);
                if (i == selected) Ui.Fill(new Rect(r.x + 3 * s, r.y + 3 * s, r.width - 6 * s, r.height - 6 * s), new Color(1f, 0.85f, 0.5f, 0.16f));

                if (item.setValue == null)
                {
                    GUI.Label(r, (i == selected ? "▶  " : "") + item.label, itemStyle);
                    continue;
                }

                // 값 항목: 왼쪽 이름, 오른쪽 막대(음량) 또는 켬/끔
                var labelStyle = new GUIStyle(itemStyle) { alignment = TextAnchor.MiddleLeft };
                GUI.Label(new Rect(r.x + 18 * s, r.y, 160 * s, r.height), item.label, labelStyle);
                var right = new Rect(r.x + 180 * s, r.y + 12 * s, r.width - 200 * s, r.height - 24 * s);
                if (isSlider)
                {
                    var bar = new Rect(right.x, right.y + 6 * s, right.width - 60 * s, right.height - 12 * s);
                    if (draggingSlider == i && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                    {
                        item.setValue(Mathf.InverseLerp(bar.xMin, bar.xMax, e.mousePosition.x));
                        e.Use();
                    }
                    float v = item.value();
                    Ui.Fill(bar, new Color(1f, 1f, 1f, 0.12f));
                    Ui.Fill(new Rect(bar.x, bar.y, bar.width * v, bar.height), new Color(1f, 0.82f, 0.45f, 0.9f));
                    GUI.Label(new Rect(bar.xMax + 8 * s, r.y, 56 * s, r.height), Mathf.RoundToInt(v * 100f) + "%", labelStyle);
                }
                else
                {
                    bool on = item.value() > 0.5f;
                    GUI.Label(new Rect(right.x, r.y, right.width, r.height), on ? "<color=#ffd27a>켬</color>   끔" : "켬   <color=#ffd27a>끔</color>",
                        new GUIStyle(labelStyle) { alignment = TextAnchor.MiddleRight });
                }
            }
            if (e.type == EventType.MouseUp) draggingSlider = -1;

            float hintY = top + items.Count * (h + gap) + 16 * s;
            string hint = page == Page.Settings ? "W/S 고르기 · A/D 바꾸기 · Esc 돌아가기" : page == Page.Pause ? "Esc 계속하기" : "W/S 고르기 · Enter 결정";
            GUI.Label(new Rect(0, hintY, Screen.width, 26 * s), hint, hintStyle);
        }
    }
}
