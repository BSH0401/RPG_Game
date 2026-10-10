using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 강화 화면. 왼쪽은 가진 장비·편지 도구 목록, 오른쪽은 고른 것의 강화 정보.
    /// 강화를 누르면 잠깐 게이지가 차오른 뒤 성공(금빛 번쩍) / 실패(다음 확률 +10%)가 크게 뜬다.
    /// W/S 고르기 · Enter/Space/E 강화 · Esc 닫기, 또는 마우스.
    /// </summary>
    public class UpgradeMenu : MonoBehaviour
    {
        public static UpgradeMenu I { get; private set; }
        public static bool IsOpen => I != null && I.open;
        public static int LastClosedFrame { get; private set; } = -1;

        enum Phase { Choose, Rolling, Result }

        bool open;
        int openedFrame;
        int selected;
        float scroll;
        Phase phase;
        float phaseStart;
        bool lastSuccess;
        string resultText;
        float flash;
        readonly List<ItemDef> list = new List<ItemDef>();
        GUIStyle big, right;

        const float RollTime = 0.9f, ResultTime = 1.4f;

        void Awake() => I = this;
        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Open()
        {
            if (I == null) return;
            I.open = true;
            I.openedFrame = Time.frameCount;
            I.phase = Phase.Choose;
            I.Refresh();
            I.selected = Mathf.Clamp(I.selected, 0, Mathf.Max(0, I.list.Count - 1));
            Time.timeScale = 0f;
            Sound.Play("Talk", 0.5f);
        }

        public static void Close()
        {
            if (I == null || !I.open) return;
            I.open = false;
            LastClosedFrame = Time.frameCount;
            Time.timeScale = HUD.MapOpen || HUD.BagOpen || GameMenu.IsOpen ? 0f : 1f;
        }

        void Refresh()
        {
            list.Clear();
            foreach (var s in GameState.Items)
            {
                var d = GameData.GetItem(s.id);
                if (Upgrade.CanUpgrade(d)) list.Add(d);
            }
            // 전설 → 강화 단계 높은 순 → 원래 순서
            list.Sort((a, b) =>
            {
                int la = Upgrade.IsLegendary(a) ? 1 : 0, lb = Upgrade.IsLegendary(b) ? 1 : 0;
                if (la != lb) return lb - la;
                return Upgrade.Level(b) - Upgrade.Level(a);
            });
        }

        ItemDef Selected => selected >= 0 && selected < list.Count ? list[selected] : null;

        /// <summary>자동 플레이테스트: 결과 연출 없이 바로 시도한다.</summary>
        public static bool TryNow(ItemDef d, out bool success) => Upgrade.Try(d, out success);

        void Update()
        {
            if (!open || Time.frameCount == openedFrame) return;
            if (phase == Phase.Rolling)
            {
                if (Time.unscaledTime - phaseStart >= RollTime) Resolve();
                return;
            }
            if (phase == Phase.Result)
            {
                if (Time.unscaledTime - phaseStart >= ResultTime || GameInput.MenuConfirm) phase = Phase.Choose;
                return;
            }
            if (GameInput.PausePressed || GameInput.InventoryPressed)
            {
                Close();
                return;
            }
            if (list.Count == 0) return;
            if (GameInput.MenuUp) Select(selected - 1);
            if (GameInput.MenuDown) Select(selected + 1);
            if (GameInput.MenuConfirm) StartRoll();
        }

        void Select(int i)
        {
            int next = (i + list.Count) % list.Count;
            if (next != selected) Sound.Play("Talk", 0.3f);
            selected = next;
        }

        void StartRoll()
        {
            var d = Selected;
            if (d == null) return;
            if (Upgrade.IsMax(d))
            {
                Sound.Play("EnemyHit", 0.4f);
                return;
            }
            if (!Upgrade.Affordable(d))
            {
                Sound.Play("EnemyHit", 0.4f);
                resultText = "재료가 모자란다";
                lastSuccess = false;
                phase = Phase.Result;
                phaseStart = Time.unscaledTime - ResultTime * 0.5f;
                return;
            }
            phase = Phase.Rolling;
            phaseStart = Time.unscaledTime;
            Sound.Play("Tool", 0.6f);
        }

        void Resolve()
        {
            var d = Selected;
            Upgrade.Try(d, out lastSuccess);
            phase = Phase.Result;
            phaseStart = Time.unscaledTime;
            if (lastSuccess)
            {
                int l = Upgrade.Level(d);
                resultText = "+" + l + " 강화 성공!";
                flash = 1f;
                Sound.Play("Delivered", 1f);
                if (l >= Upgrade.MaxLevel) resultText = "★ +" + l + " 최대 강화! ★";
            }
            else
            {
                resultText = "강화 실패…  다음 성공 확률 +" + Upgrade.FailBonus + "%";
                Sound.Play("PlayerHurt", 0.5f);
            }
            Refresh();
            selected = Mathf.Max(0, list.IndexOf(d));
        }

        // ------------------------------------------------------------------ 그리기

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -50;
            Ui.Ensure();
            float s = Ui.S;
            if (big == null || big.fontSize != Ui.Px(44))
            {
                big = new GUIStyle(Ui.Title) { fontSize = Ui.Px(44), alignment = TextAnchor.MiddleCenter, wordWrap = false };
                right = new GUIStyle(Ui.Text) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            }

            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.02f, 0.06f, 0.78f));
            float w = Mathf.Min(Screen.width - 40 * s, 980 * s), h = Mathf.Min(Screen.height - 40 * s, 600 * s);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Ui.Panel(box);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 12 * s, w - 40 * s, 34 * s),
                "강화 작업대   <size=" + Ui.Px(15) + "><color=#9fb3ff>(W/S 고르기 · Enter 강화 · Esc 닫기)</color></size>", Ui.Title);

            // 가진 재료
            int crystals = GameState.ItemCount(Upgrade.Crystal), shards = GameState.ItemCount(Upgrade.Shard);
            var matRect = new Rect(box.xMax - 330 * s, box.y + 14 * s, 310 * s, 30 * s);
            DrawIcon(new Rect(matRect.x, matRect.y + 3 * s, 24 * s, 24 * s), "InkCrystal");
            GUI.Label(new Rect(matRect.x + 30 * s, matRect.y, 130 * s, 30 * s), "먹물 결정 " + crystals, Ui.Text);
            DrawIcon(new Rect(matRect.x + 165 * s, matRect.y + 3 * s, 24 * s, 24 * s), "MoonShard");
            GUI.Label(new Rect(matRect.x + 195 * s, matRect.y, 130 * s, 30 * s), "달빛 조각 " + shards, Ui.Text);

            var listRect = new Rect(box.x + 16 * s, box.y + 58 * s, w * 0.42f, h - 74 * s);
            var infoRect = new Rect(listRect.xMax + 14 * s, listRect.y, box.xMax - listRect.xMax - 30 * s, listRect.height);
            DrawList(listRect, s);
            DrawInfo(infoRect, s);
            DrawResult(box, s);
        }

        void DrawList(Rect r, float s)
        {
            Ui.Fill(r, new Color(0f, 0f, 0f, 0.25f));
            if (list.Count == 0)
            {
                GUI.Label(new Rect(r.x + 12 * s, r.y + 12 * s, r.width - 24 * s, 80 * s), "강화할 장비가 없다.", Ui.Text);
                return;
            }
            float rowH = 46 * s;
            int visible = Mathf.Max(1, Mathf.FloorToInt(r.height / rowH));
            // 고른 줄이 보이게 스크롤
            if (selected < scroll) scroll = selected;
            if (selected >= scroll + visible) scroll = selected - visible + 1;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && r.Contains(e.mousePosition))
            {
                scroll = Mathf.Clamp(scroll + Mathf.Sign(e.delta.y), 0, Mathf.Max(0, list.Count - visible));
                e.Use();
            }
            int first = Mathf.RoundToInt(scroll);
            for (int i = first; i < list.Count && i < first + visible; i++)
            {
                var d = list[i];
                var row = new Rect(r.x + 4 * s, r.y + 4 * s + (i - first) * rowH, r.width - 8 * s, rowH - 4 * s);
                if (phase == Phase.Choose && e.type == EventType.MouseDown && row.Contains(e.mousePosition))
                {
                    if (selected == i) StartRoll();
                    else Select(i);
                    e.Use();
                }
                Ui.Fill(row, i == selected ? new Color(1f, 0.85f, 0.5f, 0.18f) : new Color(1f, 1f, 1f, 0.04f));
                DrawIcon(new Rect(row.x + 6 * s, row.y + 5 * s, 32 * s, 32 * s), d.icon);
                GUI.Label(new Rect(row.x + 46 * s, row.y, row.width - 50 * s, row.height), Upgrade.RichName(d), Ui.Text);
                if (Upgrade.IsMax(d)) GUI.Label(new Rect(row.x, row.y, row.width - 10 * s, row.height), "<color=#ffa040>MAX</color>", right);
            }
        }

        void DrawInfo(Rect r, float s)
        {
            var d = Selected;
            if (d == null) return;
            int level = Upgrade.Level(d);
            float y = r.y + 6 * s;
            DrawIcon(new Rect(r.x + 6 * s, y, 64 * s, 64 * s), d.icon);
            GUI.Label(new Rect(r.x + 84 * s, y, r.width - 90 * s, 34 * s), "<size=" + Ui.Px(26) + "><b>" + Upgrade.RichName(d) + "</b></size>", Ui.Text);
            GUI.Label(new Rect(r.x + 84 * s, y + 34 * s, r.width - 90 * s, 30 * s), Stars(level), Ui.Text);
            y += 80 * s;
            GUI.Label(new Rect(r.x + 6 * s, y, r.width - 12 * s, 50 * s), d.description, Ui.Small);
            y += 56 * s;

            if (Upgrade.IsMax(d))
            {
                GUI.Label(new Rect(r.x + 6 * s, y, r.width, 30 * s), "지금: " + Upgrade.Describe(d, level), Ui.Text);
                GUI.Label(new Rect(r.x + 6 * s, y + 40 * s, r.width, 40 * s), "<color=#ffa040><b>최대 강화 완료!</b></color>", Ui.Text);
                return;
            }
            GUI.Label(new Rect(r.x + 6 * s, y, r.width, 30 * s), "지금:  " + Upgrade.Describe(d, level), Ui.Text);
            GUI.Label(new Rect(r.x + 6 * s, y + 30 * s, r.width, 30 * s), "다음:  <color=#ffd27a>" + Upgrade.Describe(d, level + 1) + "</color>", Ui.Text);
            y += 76 * s;

            // 재료와 확률
            int needC = Upgrade.Crystals(d), needS = Upgrade.Shards(d);
            bool okC = GameState.ItemCount(Upgrade.Crystal) >= needC, okS = GameState.ItemCount(Upgrade.Shard) >= needS;
            DrawIcon(new Rect(r.x + 6 * s, y + 3 * s, 24 * s, 24 * s), "InkCrystal");
            GUI.Label(new Rect(r.x + 36 * s, y, 200 * s, 30 * s), (okC ? "" : "<color=#ff7070>") + "먹물 결정 " + needC + (okC ? "" : "</color>"), Ui.Text);
            if (needS > 0)
            {
                DrawIcon(new Rect(r.x + 216 * s, y + 3 * s, 24 * s, 24 * s), "MoonShard");
                GUI.Label(new Rect(r.x + 246 * s, y, 200 * s, 30 * s), (okS ? "" : "<color=#ff7070>") + "달빛 조각 " + needS + (okS ? "" : "</color>"), Ui.Text);
            }
            y += 40 * s;
            int chance = Upgrade.Chance(d), bonus = Upgrade.FailBonusNow(d);
            string chanceColor = chance >= 80 ? "#8fe08f" : chance >= 50 ? "#ffd27a" : "#ff9a70";
            GUI.Label(new Rect(r.x + 6 * s, y, r.width, 40 * s),
                "성공 확률  <size=" + Ui.Px(30) + "><b><color=" + chanceColor + ">" + chance + "%</color></b></size>" +
                (bonus > 0 ? "   <color=#9fe0ff>(실패 보정 +" + bonus + "%)</color>" : ""), Ui.Text);
            y += 50 * s;
            GUI.Label(new Rect(r.x + 6 * s, y, r.width, 30 * s), "<color=#9fb3ff>실패해도 장비는 그대로, 재료만 사라진다.</color>", Ui.Small);

            // 강화 버튼(또는 게이지)
            var btn = new Rect(r.x + 6 * s, r.yMax - 60 * s, r.width - 12 * s, 52 * s);
            Ui.Panel(btn);
            if (phase == Phase.Rolling)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - phaseStart) / RollTime);
                float wobble = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 40f);
                Ui.Fill(new Rect(btn.x + 4 * s, btn.y + 4 * s, (btn.width - 8 * s) * t, btn.height - 8 * s), new Color(1f, 0.8f, 0.35f, 0.35f + 0.25f * wobble));
                GUI.Label(btn, "두근두근…", new GUIStyle(Ui.Center) { fontSize = Ui.Px(22) });
            }
            else
            {
                bool ok = okC && okS;
                if (ok) Ui.Fill(new Rect(btn.x + 4 * s, btn.y + 4 * s, btn.width - 8 * s, btn.height - 8 * s), new Color(1f, 0.8f, 0.35f, 0.18f + 0.08f * Mathf.Sin(Time.unscaledTime * 4f)));
                GUI.Label(btn, ok ? "<b>강화하기</b>  [Enter]" : "<color=#9a9aa8>재료가 모자란다</color>", new GUIStyle(Ui.Center) { fontSize = Ui.Px(22) });
                var e = Event.current;
                if (phase == Phase.Choose && e.type == EventType.MouseDown && btn.Contains(e.mousePosition))
                {
                    StartRoll();
                    e.Use();
                }
            }
        }

        void DrawResult(Rect box, float s)
        {
            if (flash > 0f)
            {
                Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0.9f, 0.55f, flash * 0.55f));
                if (Event.current.type == EventType.Repaint) flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2.2f);
            }
            if (phase != Phase.Result || string.IsNullOrEmpty(resultText)) return;
            float t = Time.unscaledTime - phaseStart;
            float pop = 1f + 0.25f * Mathf.Max(0f, 1f - t * 5f);
            var style = new GUIStyle(big) { fontSize = Mathf.RoundToInt(big.fontSize * pop) };
            style.normal.textColor = lastSuccess ? new Color(1f, 0.86f, 0.4f) : new Color(0.75f, 0.78f, 0.9f);
            var r = new Rect(box.x, box.center.y - 50 * s, box.width, 100 * s);
            Ui.Fill(new Rect(box.x, r.y + 10 * s, box.width, 80 * s), new Color(0f, 0f, 0f, 0.6f));
            Ui.Shadow(r, resultText, style);
        }

        static string Stars(int level)
        {
            string text = "";
            for (int i = 0; i < Upgrade.MaxLevel; i++) text += i < level ? "<color=#ffd34a>★</color>" : "<color=#555a70>☆</color>";
            return text;
        }

        static void DrawIcon(Rect r, string icon)
        {
            var tex = GameAssets.ItemIcon(icon);
            if (tex == null) return;
            float k = Mathf.Min(r.width / tex.width, r.height / tex.height);
            float iw = tex.width * k, ih = tex.height * k;
            GUI.DrawTexture(new Rect(r.center.x - iw * 0.5f, r.center.y - ih * 0.5f, iw, ih), tex);
        }
    }
}
