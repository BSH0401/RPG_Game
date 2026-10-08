using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 체력, 도구 쿨타임, 들고 있는 편지, 조사 안내, 알림, 이름표, 보스 체력, 지도(Tab), 조작법(F1).
    /// </summary>
    public class HUD : MonoBehaviour
    {
        public struct MapRegion
        {
            public Rect area;
            public Color color;
            public string label;
        }

        struct ToastMsg
        {
            public string text;
            public float until;
        }

        public static HUD I { get; private set; }
        public static bool MapOpen { get; private set; }

        public Rect worldBounds;
        public readonly List<MapRegion> regions = new List<MapRegion>();
        public Vector2 postOfficePos;

        readonly List<ToastMsg> toasts = new List<ToastMsg>();
        bool showHelp = true;
        float helpUntil;

        void Awake()
        {
            I = this;
            helpUntil = Time.unscaledTime + 25f;
        }

        void OnDestroy()
        {
            if (I == this)
            {
                I = null;
                SetMap(false);
            }
        }

        public static void Toast(string text)
        {
            if (I == null || string.IsNullOrEmpty(text)) return;
            I.toasts.Add(new ToastMsg { text = text, until = Time.unscaledTime + 3.5f });
            if (I.toasts.Count > 4) I.toasts.RemoveAt(0);
        }

        void Update()
        {
            if (GameInput.MapTogglePressed && (MapOpen || !DialogueSystem.IsOpen)) SetMap(!MapOpen);
            if (GameInput.HelpPressed)
            {
                showHelp = !showHelp;
                helpUntil = float.MaxValue;
            }
            if (GameInput.ResetPressed)
            {
                // 개발용: 저장을 지우고 처음부터.
                SetMap(false);
                GameBootstrap.RestartNewGame();
            }
        }

        static void SetMap(bool openMap)
        {
            MapOpen = openMap;
            Time.timeScale = openMap ? 0f : 1f;
        }

        void OnGUI()
        {
            Ui.Ensure();
            var player = PlayerController.I;
            if (player == null) return;

            DrawStatus(player);
            DrawLetterPanel();
            DrawNameLabels(player);
            DrawBossBar(player);
            if (!DialogueSystem.IsOpen && !MapOpen) DrawPrompt(player);
            DrawToasts();
            if (showHelp && Time.unscaledTime < helpUntil && !MapOpen) DrawHelp();
            if (MapOpen) DrawMap(player);
        }

        void DrawStatus(PlayerController player)
        {
            float s = Ui.S;
            var hp = player.Health;
            for (int i = 0; i < hp.Max; i++)
            {
                var r = new Rect((16 + i * 30) * s, 16 * s, 24 * s, 24 * s);
                Ui.Fill(r, new Color(0f, 0f, 0f, 0.6f));
                var inner = new Rect(r.x + 3 * s, r.y + 3 * s, r.width - 6 * s, r.height - 6 * s);
                Ui.Fill(inner, i < hp.Current ? new Color(0.95f, 0.3f, 0.35f) : new Color(0.25f, 0.2f, 0.25f));
            }

            float cd = player.ToolCooldownRemaining;
            string tool = cd > 0f ? "[Q] 봉인끈  " + cd.ToString("0.0") + "초" : "[Q] 봉인끈  준비됨";
            Ui.Shadow(Ui.R(16, 46, 300, 26), tool, Ui.Small);
            Ui.Shadow(Ui.R(16, 70, 300, 26), GameState.Night > 0 ? GameState.Night + "번째 밤" : "첫 밤 전", Ui.Small);
        }

        void DrawLetterPanel()
        {
            float s = Ui.S;
            float w = 380 * s;
            var box = new Rect(Screen.width - w - 16 * s, 16 * s, w, 118 * s);
            Ui.Panel(box);
            float x = box.x + 14 * s;
            float iw = box.width - 28 * s;
            var letter = LetterManager.Carrying;
            if (letter == null)
            {
                GUI.Label(new Rect(x, box.y + 10 * s, iw, 30 * s), "들고 있는 편지 없음", Ui.Title);
                GUI.Label(new Rect(x, box.y + 44 * s, iw, 70 * s),
                    LetterManager.NextAvailable() != null ? "우체국 창구에서 새 편지를 받자." : "오늘 밤의 편지는 모두 배달했다.", Ui.Small);
                return;
            }
            GUI.Label(new Rect(x, box.y + 10 * s, iw, 30 * s), "「" + letter.title + "」", Ui.Title);
            GUI.Label(new Rect(x, box.y + 42 * s, iw, 24 * s), "보낸 이: " + letter.senderName, Ui.Small);
            string to = LetterManager.IsRecipientRevealed(letter) ? "받는 이: " + letter.recipientName : "받는 이: ???  (단서를 찾자)";
            GUI.Label(new Rect(x, box.y + 66 * s, iw, 48 * s), to, Ui.Small);
        }

        void DrawPrompt(PlayerController player)
        {
            var target = player.CurrentTarget;
            if (target == null) return;
            float s = Ui.S;
            var r = new Rect(Screen.width * 0.5f - 260 * s, Screen.height - 90 * s, 520 * s, 40 * s);
            Ui.Fill(r, new Color(0f, 0f, 0f, 0.55f));
            GUI.Label(r, "[E] " + target.Prompt, Ui.Center);
        }

        void DrawNameLabels(PlayerController player)
        {
            var cam = Camera.main;
            if (cam == null) return;
            float s = Ui.S;
            var letter = LetterManager.Carrying;
            foreach (var it in Interactable.All)
            {
                if (!(it is NpcInteractable npc)) continue;
                if (Vector2.Distance(player.transform.position, npc.transform.position) > 7f) continue;
                Vector3 sp = cam.WorldToScreenPoint(npc.transform.position + Vector3.up * 0.9f);
                if (sp.z < 0) continue;
                bool isTarget = letter != null && letter.recipientId == npc.npcId && LetterManager.IsRecipientRevealed(letter);
                string label = isTarget ? "[편지] " + npc.DisplayName : npc.DisplayName;
                var r = new Rect(sp.x - 120 * s, Screen.height - sp.y - 26 * s, 240 * s, 26 * s);
                Ui.Shadow(r, label, Ui.Center);
            }
        }

        void DrawBossBar(PlayerController player)
        {
            foreach (var e in EnemyController.Active)
            {
                if (!e.isBoss) continue;
                if (Vector2.Distance(player.transform.position, e.transform.position) > 12f) continue;
                float s = Ui.S;
                float w = 460 * s;
                var r = new Rect((Screen.width - w) * 0.5f, 20 * s, w, 18 * s);
                Ui.Fill(r, new Color(0f, 0f, 0f, 0.7f));
                float t = (float)e.Health.Current / e.Health.Max;
                Ui.Fill(new Rect(r.x + 2 * s, r.y + 2 * s, (w - 4 * s) * t, r.height - 4 * s), new Color(0.6f, 0.3f, 0.8f));
                Ui.Shadow(new Rect(r.x, r.yMax + 2 * s, w, 26 * s), e.displayName, Ui.Center);
                return;
            }
        }

        void DrawToasts()
        {
            float s = Ui.S;
            toasts.RemoveAll(t => Time.unscaledTime > t.until);
            for (int i = 0; i < toasts.Count; i++)
            {
                var r = new Rect(Screen.width * 0.5f - 300 * s, (80 + i * 36) * s, 600 * s, 32 * s);
                Ui.Fill(r, new Color(0f, 0f, 0f, 0.5f));
                GUI.Label(r, toasts[i].text, Ui.Center);
            }
        }

        void DrawHelp()
        {
            float s = Ui.S;
            var r = new Rect(16 * s, Screen.height - 250 * s, 330 * s, 234 * s);
            Ui.Panel(r);
            GUI.Label(new Rect(r.x + 12 * s, r.y + 8 * s, r.width - 24 * s, r.height - 16 * s),
                "<b>조작법</b>  (F1로 숨기기)\n" +
                "WASD / 방향키  이동\n" +
                "마우스 왼쪽 / J  공격\n" +
                "Space / Shift  회피 (무적)\n" +
                "E  조사 · 대화 · 배달\n" +
                "Q  봉인끈 (주변 적 묶기)\n" +
                "Tab / M  지도와 의뢰\n" +
                "F12  저장 삭제 후 새 게임", Ui.Small);
        }

        void DrawMap(PlayerController player)
        {
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.08f, 0.88f));

            float maxW = Screen.width - 80 * s;
            float maxH = Screen.height - 260 * s;
            float scale = Mathf.Min(maxW / worldBounds.width, maxH / worldBounds.height);
            float mw = worldBounds.width * scale;
            float mh = worldBounds.height * scale;
            var map = new Rect((Screen.width - mw) * 0.5f, 70 * s, mw, mh);

            Ui.Shadow(new Rect(0, 20 * s, Screen.width, 40 * s), "<b>달빛 우체국 — 섬 지도</b>   (Tab 닫기)", Ui.Center);
            Ui.Fill(map, new Color(0.1f, 0.12f, 0.2f));

            foreach (var reg in regions)
            {
                var r = ToMap(map, reg.area);
                Ui.Fill(r, reg.color);
                if (!string.IsNullOrEmpty(reg.label))
                    GUI.Label(new Rect(r.x, r.y + 6 * s, r.width, 30 * s), reg.label, Ui.Center);
            }

            Marker(map, postOfficePos, new Color(1f, 0.85f, 0.4f), "우체국");

            var letter = LetterManager.Carrying;
            string info;
            if (letter != null)
            {
                var npc = NpcInteractable.Find(letter.recipientId);
                if (LetterManager.IsRecipientRevealed(letter) && npc != null)
                    Marker(map, npc.transform.position, new Color(1f, 0.4f, 0.4f), "[편지] " + npc.DisplayName);
                else
                    GUI.Label(new Rect(map.x + map.width * 0.55f, map.y + map.height * 0.45f, map.width * 0.4f, 40 * s), "? 받는 사람을 모름", Ui.Center);

                info = "<b>「" + letter.title + "」</b>  보낸 이: " + letter.senderName + "\n받는 이: " + LetterManager.RecipientLabel(letter);
            }
            else
            {
                info = LetterManager.NextAvailable() != null ? "들고 있는 편지가 없다. 우체국 창구에서 편지를 받자." : "모든 편지를 배달했다.";
            }

            Marker(map, player.transform.position, new Color(0.5f, 0.9f, 1f), "나");

            var infoBox = new Rect(map.x, map.yMax + 16 * s, map.width, 120 * s);
            Ui.Panel(infoBox);
            GUI.Label(new Rect(infoBox.x + 14 * s, infoBox.y + 10 * s, infoBox.width - 28 * s, infoBox.height - 20 * s),
                info + "\n<color=#9fb3ff>동쪽 숲의 길은 밤마다 조금씩 바뀐다. 쓰러진 나무가 길을 막고 있다면 반대쪽 길로 돌아가자.</color>", Ui.Text);
        }

        Rect ToMap(Rect map, Rect area)
        {
            float x0 = map.x + (area.xMin - worldBounds.xMin) / worldBounds.width * map.width;
            float x1 = map.x + (area.xMax - worldBounds.xMin) / worldBounds.width * map.width;
            float y0 = map.y + (1f - (area.yMax - worldBounds.yMin) / worldBounds.height) * map.height;
            float y1 = map.y + (1f - (area.yMin - worldBounds.yMin) / worldBounds.height) * map.height;
            return new Rect(x0, y0, x1 - x0, y1 - y0);
        }

        void Marker(Rect map, Vector2 world, Color color, string label)
        {
            float s = Ui.S;
            var p = ToMap(map, new Rect(world.x, world.y, 0, 0));
            float size = 14 * s;
            Ui.Fill(new Rect(p.x - size * 0.5f - 2 * s, p.y - size * 0.5f - 2 * s, size + 4 * s, size + 4 * s), Color.black);
            Ui.Fill(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), color);
            Ui.Shadow(new Rect(p.x - 100 * s, p.y + size * 0.6f, 200 * s, 26 * s), label, Ui.Center);
        }
    }
}
