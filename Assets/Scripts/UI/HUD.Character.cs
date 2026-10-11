using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// HUD 의 성장 부분: 왼쪽 위 체력·경험치·스킬 칸, 레벨업 배너, 캐릭터 창(C: 스탯 올리기·스킬 고르기).
    /// </summary>
    public partial class HUD
    {
        public static bool CharOpen { get; private set; }
        /// <summary>지도·가방·캐릭터 창 중 하나라도 열려 있으면 게임이 멈춘다.</summary>
        public static bool AnyOverlay => MapOpen || BagOpen || CharOpen;

        static readonly Stat[] AllStats = { Stat.Str, Stat.Vit, Stat.Agi, Stat.Luk };

        int bannerLevel;
        float bannerStart = -10f;
        GUIStyle bigStyle, barStyle;

        public static void SetChar(bool open)
        {
            CharOpen = open;
            Time.timeScale = AnyOverlay ? 0f : 1f;
        }

        /// <summary>레벨이 오르면 화면 가운데 위에 크게 띄운다.</summary>
        public static void LevelUpBanner(int level)
        {
            if (I == null) return;
            I.bannerLevel = level;
            I.bannerStart = Time.unscaledTime;
        }

        void UpdateCharacterKeys()
        {
            if (GameInput.CharacterPressed && !MapOpen && !BagOpen && (CharOpen || !DialogueSystem.IsOpen)) SetChar(!CharOpen);
            if (!CharOpen) return;
            for (int i = 0; i < AllStats.Length; i++)
                if (GameInput.ChoicePressed(i)) SpendPoint(AllStats[i]);
        }

        static void SpendPoint(Stat s)
        {
            if (!Stats.Spend(s)) return;
            Sound.Play("Clue", 0.5f);
            if (Stats.Points == 0) GameState.Save();
        }

        void EnsureGrowthStyles()
        {
            if (bigStyle == null || bigStyle.fontSize != Ui.Px(46))
            {
                bigStyle = new GUIStyle(Ui.Center) { fontSize = Ui.Px(46), fontStyle = FontStyle.Bold };
                barStyle = new GUIStyle(Ui.Small) { alignment = TextAnchor.MiddleCenter, fontSize = Ui.Px(14), fontStyle = FontStyle.Bold };
            }
        }

        // ------------------------------------------------------------------ 왼쪽 위: 체력 · 경험치 · 스킬

        /// <summary>그린 높이(아래 줄이 이어 그릴 y, 720p 기준 단위)를 돌려준다.</summary>
        float DrawVitals(PlayerController player)
        {
            EnsureGrowthStyles();
            float s = Ui.S;

            // 레벨 배지
            var badge = Ui.R(16, 16, 58, 50);
            Ui.Panel(badge);
            GUI.Label(new Rect(badge.x, badge.y + 2 * s, badge.width, 18 * s), "Lv", barStyle);
            GUI.Label(new Rect(badge.x, badge.y + 16 * s, badge.width, 32 * s), "<b>" + Stats.Level + "</b>", Ui.Center);

            // 체력 막대
            var hp = player.Health;
            var hpBar = Ui.R(84, 18, 270, 24);
            Ui.Fill(hpBar, new Color(0f, 0f, 0f, 0.7f));
            float t = hp.Max > 0 ? (float)hp.Current / hp.Max : 0f;
            bool low = t < 0.3f;
            Color hpColor = low && Mathf.Repeat(Time.unscaledTime * 3f, 1f) < 0.5f ? new Color(1f, 0.35f, 0.35f) : new Color(0.86f, 0.22f, 0.3f);
            Ui.Fill(new Rect(hpBar.x + 2 * s, hpBar.y + 2 * s, (hpBar.width - 4 * s) * t, hpBar.height - 4 * s), hpColor);
            Ui.Fill(new Rect(hpBar.x + 2 * s, hpBar.y + 2 * s, (hpBar.width - 4 * s) * t, 4 * s), new Color(1f, 1f, 1f, 0.18f));
            Ui.Shadow(hpBar, hp.Current + " / " + hp.Max, barStyle);

            // 경험치 막대
            int need = Stats.ExpToNext();
            var expBar = Ui.R(84, 46, 270, 14);
            Ui.Fill(expBar, new Color(0f, 0f, 0f, 0.7f));
            float e = Stats.Level >= Stats.MaxLevel ? 1f : Mathf.Clamp01((float)Stats.Exp / need);
            Ui.Fill(new Rect(expBar.x + 2 * s, expBar.y + 2 * s, (expBar.width - 4 * s) * e, expBar.height - 4 * s), new Color(0.35f, 0.75f, 1f));
            var expStyle = new GUIStyle(barStyle) { fontSize = Ui.Px(11) };
            Ui.Shadow(expBar, Stats.Level >= Stats.MaxLevel ? "MAX" : "EXP " + Stats.Exp + " / " + need, expStyle);

            // 스킬 칸 (우클릭)
            var part = Inventory.CurrentToolPart;
            var slot = Ui.R(16, 74, 44, 44);
            Ui.Panel(slot);
            if (part != null) DrawIconFit(new Rect(slot.x + 6 * s, slot.y + 6 * s, slot.width - 12 * s, slot.height - 12 * s), GameAssets.ItemIcon(part.icon));
            float cd = player.SkillCooldownRemaining;
            if (cd > 0f)
            {
                float k = Mathf.Clamp01(cd / Mathf.Max(0.1f, player.SkillCooldownTotal));
                Ui.Fill(new Rect(slot.x, slot.y + slot.height * (1f - k), slot.width, slot.height * k), new Color(0f, 0f, 0.1f, 0.65f));
                Ui.Shadow(slot, cd.ToString(cd < 10f ? "0.0" : "0"), barStyle);
            }
            string skill = "<color=#ffd98a>[우클릭]</color> " + PlayerController.SkillName(part) + (cd > 0f ? "" : "  <color=#8fe08f>준비</color>");
            Ui.Shadow(Ui.R(68, 76, 300, 22), skill, Ui.Small);
            if (Stats.Points > 0)
            {
                bool blink = Mathf.Repeat(Time.unscaledTime * 1.5f, 1f) < 0.6f;
                Ui.Shadow(Ui.R(68, 98, 300, 22), (blink ? "<color=#ffd34a>" : "<color=#c8a850>") + "▲ 스탯 포인트 " + Stats.Points + "  [C]</color>", Ui.Small);
            }
            return 126f;
        }

        // ------------------------------------------------------------------ 레벨업 배너

        void DrawLevelBanner()
        {
            float age = Time.unscaledTime - bannerStart;
            const float life = 2.2f;
            if (age > life) return;
            EnsureGrowthStyles();
            float s = Ui.S;
            float a = age < 0.15f ? age / 0.15f : Mathf.Clamp01((life - age) / 0.5f);
            float pop = 1f + 0.25f * Mathf.Max(0f, 1f - age * 5f);
            var r = new Rect(0, Screen.height * 0.32f - 40 * s * pop, Screen.width, 80 * s * pop);
            var old = bigStyle.normal.textColor;
            int size = bigStyle.fontSize;
            bigStyle.fontSize = Mathf.RoundToInt(Ui.Px(46) * pop);
            bigStyle.normal.textColor = new Color(0f, 0f, 0f, 0.7f * a);
            GUI.Label(new Rect(r.x + 3, r.y + 3, r.width, r.height), "LEVEL UP!", bigStyle);
            bigStyle.normal.textColor = new Color(1f, 0.86f, 0.3f, a);
            GUI.Label(r, "LEVEL UP!", bigStyle);
            bigStyle.fontSize = size;
            bigStyle.normal.textColor = old;
            var sub = new Rect(0, r.yMax, Screen.width, 30 * s);
            var c = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            Ui.Shadow(sub, "레벨 " + bannerLevel + "  ·  스탯 포인트 +" + Stats.PointsPerLevel + "  ·  <color=#9fb3ff>[C] 캐릭터 창</color>", Ui.Center);
            GUI.color = c;
        }

        // ------------------------------------------------------------------ 캐릭터 창 (C)

        void DrawCharacter(PlayerController player)
        {
            EnsureGrowthStyles();
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.08f, 0.75f));
            float w = Mathf.Min(Screen.width - 40 * s, 900 * s), h = 560 * s;
            var box = new Rect((Screen.width - w) * 0.5f, Mathf.Max(16 * s, (Screen.height - h) * 0.5f), w, h);
            Ui.Panel(box);
            float x = box.x + 24 * s, y = box.y + 16 * s;
            GUI.Label(new Rect(x, y, w - 48 * s, 32 * s), "캐릭터   <size=" + Ui.Px(15) + "><color=#9fb3ff>(C 닫기 · 1~4 / 클릭으로 스탯 올리기)</color></size>", Ui.Title);
            y += 42 * s;

            int need = Stats.ExpToNext();
            GUI.Label(new Rect(x, y, w - 48 * s, 28 * s),
                "<b>레벨 " + Stats.Level + "</b>    경험치 " + Stats.Exp + " / " + need +
                "    남은 포인트 <color=" + (Stats.Points > 0 ? "#ffd34a" : "#9aa0b0") + "><b>" + Stats.Points + "</b></color>", Ui.Text);
            y += 40 * s;

            // 왼쪽: 스탯 올리기
            float colW = (w - 72 * s) * 0.55f;
            for (int i = 0; i < AllStats.Length; i++)
            {
                var st = AllStats[i];
                var row = new Rect(x, y + i * 62 * s, colW, 54 * s);
                Ui.Fill(row, new Color(1f, 1f, 1f, 0.05f));
                GUI.Label(new Rect(row.x + 12 * s, row.y + 4 * s, 200 * s, 28 * s), "<color=#ffd98a>" + (i + 1) + "</color>  <b>" + Stats.Name(st) + "</b>  " + Stats.Get(st), Ui.Text);
                GUI.Label(new Rect(row.x + 12 * s, row.y + 28 * s, colW - 80 * s, 24 * s), "포인트당 " + Stats.PerPoint(st), Ui.Small);
                var plus = new Rect(row.xMax - 58 * s, row.y + 8 * s, 46 * s, 38 * s);
                GUI.enabled = Stats.Points > 0;
                if (GUI.Button(plus, "+", new GUIStyle(Ui.Button) { alignment = TextAnchor.MiddleCenter })) SpendPoint(st);
                GUI.enabled = true;
            }

            // 오른쪽: 계산된 능력치
            float rx = x + colW + 24 * s, rw = w - 48 * s - colW - 24 * s;
            var stat = new Rect(rx, y, rw, 4 * 62 * s - 8 * s);
            Ui.Fill(stat, new Color(1f, 1f, 1f, 0.05f));
            string derived =
                "<b>능력치</b>\n" +
                "공격력  <color=#ffd98a>" + Stats.Attack + "</color>\n" +
                "최대 체력  <color=#ff8a8a>" + player.Health.Max + "</color>\n" +
                "치명타  " + Mathf.RoundToInt(Stats.CritChance * 100f) + "%  (피해 x" + Stats.CritMultiplier.ToString("0.0") + ")\n" +
                "공격 속도  +" + Mathf.RoundToInt((Stats.AttackSpeedMul - 1f) * 100f) + "%\n" +
                "이동 속도  +" + Mathf.RoundToInt((Stats.MoveSpeedMul - 1f) * 100f) + "%\n" +
                "전리품  +" + Mathf.RoundToInt((Stats.LootMul - 1f) * 100f) + "%";
            GUI.Label(new Rect(stat.x + 14 * s, stat.y + 8 * s, stat.width - 28 * s, stat.height - 16 * s), derived, Ui.Text);
            y += 4 * 62 * s + 8 * s;

            // 아래: 스킬 고르기
            GUI.Label(new Rect(x, y, w - 48 * s, 28 * s), "<b>스킬</b>  <size=" + Ui.Px(14) + "><color=#9fb3ff>(우클릭 · Q 로 사용. 편지 도구 부품을 골라 끼운다)</color></size>", Ui.Text);
            y += 32 * s;
            var parts = Inventory.OwnedParts();
            var current = Inventory.CurrentToolPart;
            float bw = (w - 48 * s - 16 * s * (Mathf.Max(1, parts.Count) - 1)) / Mathf.Max(1, parts.Count);
            for (int i = 0; i < parts.Count; i++)
            {
                var p = parts[i];
                var r = new Rect(x + i * (bw + 16 * s), y, bw, box.yMax - y - 16 * s);
                bool on = current != null && current.id == p.id;
                Ui.Fill(r, on ? new Color(1f, 0.85f, 0.4f, 0.22f) : new Color(1f, 1f, 1f, 0.05f));
                if (on) Ui.Fill(new Rect(r.x, r.y, 4 * s, r.height), new Color(1f, 0.85f, 0.4f));
                DrawIconFit(new Rect(r.x + 10 * s, r.y + 10 * s, 36 * s, 36 * s), GameAssets.ItemIcon(p.icon));
                GUI.Label(new Rect(r.x + 54 * s, r.y + 8 * s, r.width - 60 * s, 26 * s), "<b>" + PlayerController.SkillName(p) + "</b>" + (on ? "  <color=#ffd34a>사용 중</color>" : ""), Ui.Text);
                GUI.Label(new Rect(r.x + 10 * s, r.y + 50 * s, r.width - 20 * s, r.height - 54 * s),
                    PlayerController.SkillDescription(p) + "\n재사용 " + (p.value > 0 ? p.value : 6f).ToString("0.#") + "초", Ui.Small);
                if (!on && GUI.Button(r, GUIContent.none, GUIStyle.none)) Inventory.EquipPart(p);
            }
        }
    }
}
