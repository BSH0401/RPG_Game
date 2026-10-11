using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>장비 그리기 도우미: 등급색 칸에 아이콘, 마우스를 올렸을 때의 설명(지금 낀 장비와 비교).</summary>
    public static class GearUi
    {
        /// <summary>등급색 테두리 칸과 아이콘. 끼운 장비면 왼쪽 위에 E.</summary>
        public static void Cell(Rect r, GearItem g, bool equipped = false, bool selected = false)
        {
            float s = Ui.S;
            var tint = g != null ? Gear.RarityTint(g.rarity) : new Color(0.5f, 0.5f, 0.6f);
            Ui.Fill(r, new Color(tint.r * 0.25f, tint.g * 0.25f, tint.b * 0.3f, 0.9f));
            float b = Mathf.Max(1f, 2f * s);
            var edge = selected ? Color.white : new Color(tint.r, tint.g, tint.b, g != null && g.rarity >= Gear.Rare ? 0.95f : 0.45f);
            Ui.Fill(new Rect(r.x, r.y, r.width, b), edge);
            Ui.Fill(new Rect(r.x, r.yMax - b, r.width, b), edge);
            Ui.Fill(new Rect(r.x, r.y, b, r.height), edge);
            Ui.Fill(new Rect(r.xMax - b, r.y, b, r.height), edge);
            if (g == null) return;
            Icon(new Rect(r.x + r.width * 0.15f, r.y + r.height * 0.15f, r.width * 0.7f, r.height * 0.7f), g.Base.icon);
            if (equipped)
                GUI.Label(new Rect(r.x + 3 * s, r.y, 20 * s, 20 * s), "<b><color=#ffd34a>E</color></b>", Ui.Small);
            if (g.rarity >= Gear.Legendary && Mathf.Repeat(Time.unscaledTime * 1.5f, 1f) < 0.5f)
                Ui.Fill(new Rect(r.x + b, r.y + b, r.width - 2 * b, r.height - 2 * b), new Color(1f, 0.7f, 0.3f, 0.12f));
            if (g.plus > 0)
            {
                // 강화 단계: 오른쪽 아래 +N, 빛의 단계(+3 · +6 · +9)부터 그 색으로 안쪽 테두리가 숨 쉰다
                int tier = WeaponVisual.TierOf(g.plus);
                var tc = tier > 0 ? WeaponVisual.TierColor(tier) : new Color(0.9f, 0.9f, 0.9f);
                if (tier > 0)
                {
                    float a = 0.35f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f);
                    var inner = new Color(tc.r, tc.g, tc.b, a);
                    Ui.Fill(new Rect(r.x + b, r.y + b, r.width - 2 * b, b), inner);
                    Ui.Fill(new Rect(r.x + b, r.yMax - 2 * b, r.width - 2 * b, b), inner);
                    Ui.Fill(new Rect(r.x + b, r.y + b, b, r.height - 2 * b), inner);
                    Ui.Fill(new Rect(r.xMax - 2 * b, r.y + b, b, r.height - 2 * b), inner);
                }
                var style = new GUIStyle(Ui.Small) { alignment = TextAnchor.LowerRight, fontSize = Mathf.Max(8, Mathf.RoundToInt(Mathf.Min(r.height * 0.32f, Ui.Px(15)))) };
                Ui.Shadow(new Rect(r.x, r.y, r.width - 3 * s, r.height - 1 * s), "<b><color=#" + ColorUtility.ToHtmlStringRGB(tc) + ">+" + g.plus + "</color></b>", style);
            }
        }

        public static void Icon(Rect r, string icon)
        {
            var tex = GameAssets.ItemIcon(icon);
            if (tex == null) return;
            float k = Mathf.Min(r.width / tex.width, r.height / tex.height);
            float w = tex.width * k, h = tex.height * k;
            GUI.DrawTexture(new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h), tex);
        }

        /// <summary>장비 설명 문장(이름·등급·능력, 그리고 지금 낀 장비와의 차이).</summary>
        public static string Describe(GearItem g, bool compare, string footer = null)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("<size=").Append(Ui.Px(20)).Append("><b>").Append(g.RichName).Append("</b></size>\n");
            sb.Append("<color=").Append(Gear.RarityColor(g.rarity)).Append(">").Append(Gear.RarityNames[g.rarity]).Append("</color> ")
              .Append(Gear.SlotNames[g.slot]).Append("  ·  아이템 레벨 ").Append(g.ilvl)
              .Append(g.plus > 0 ? "  ·  <color=#ffd34a>강화 +" + g.plus + "</color>" : "").Append('\n');
            for (int i = 0; i < g.stats.Count; i++)
            {
                if (i == 1) sb.Append("<color=#9aa0b0>────────</color>\n");
                sb.Append(i == 0 ? "<b>" : "<color=#b8c8ff>").Append(Gear.StatLine((GearStat)g.stats[i], g.Value(i))).Append(i == 0 ? "</b>" : "</color>").Append('\n');
            }
            var cur = GameState.EquippedIn(g.Slot);
            if (compare && cur != null && cur != g)
            {
                sb.Append("\n<color=#9fb3ff>지금 낀 ").Append(Josa.Gwa(cur.Name)).Append(" 비교</color>\n");
                var all = new List<int>();
                foreach (var st in g.stats) if (!all.Contains(st)) all.Add(st);
                foreach (var st in cur.stats) if (!all.Contains(st)) all.Add(st);
                foreach (var st in all)
                {
                    float diff = g.Sum((GearStat)st) - cur.Sum((GearStat)st);
                    if (Mathf.Abs(diff) < 0.05f) continue;
                    sb.Append(diff > 0f ? "<color=#7fe07f>▲ " : "<color=#ff7a7a>▼ ").Append(Gear.StatLine((GearStat)st, diff)).Append("</color>\n");
                }
                float score = g.Score - cur.Score;
                sb.Append(score > 0.5f ? "<color=#7fe07f><b>더 좋아 보인다</b></color>" : score < -0.5f ? "<color=#ff9a7a>지금 것이 더 나아 보인다</color>" : "<color=#d0d0d0>비슷하다</color>");
                sb.Append('\n');
            }
            else if (compare && cur == null) sb.Append("\n<color=#7fe07f>이 부위는 비어 있다 — 끼면 바로 강해진다</color>\n");
            if (!string.IsNullOrEmpty(footer)) sb.Append('\n').Append(footer);
            return sb.ToString();
        }

        /// <summary>마우스 근처에 설명 상자를 그린다(화면 밖으로 나가지 않게).</summary>
        public static void Tooltip(string text)
        {
            float s = Ui.S;
            float w = 340 * s;
            float h = Ui.Small.CalcHeight(new GUIContent(text), w - 24 * s) + 20 * s;
            var m = Event.current.mousePosition;
            float x = m.x + 20 * s, y = m.y + 10 * s;
            if (x + w > Screen.width - 8 * s) x = m.x - w - 20 * s;
            if (y + h > Screen.height - 8 * s) y = Screen.height - h - 8 * s;
            var r = new Rect(x, Mathf.Max(8 * s, y), w, h);
            Ui.Panel(r);
            GUI.Label(new Rect(r.x + 12 * s, r.y + 10 * s, r.width - 24 * s, r.height - 16 * s), text, Ui.Small);
        }

        /// <summary>가방 정렬: 부위 → 등급 높은 순 → 강한 순.</summary>
        public static List<GearItem> Sorted(bool includeEquipped)
        {
            var list = new List<GearItem>();
            foreach (var g in GameState.Gear)
                if (includeEquipped || !GameState.IsEquipped(g)) list.Add(g);
            list.Sort((a, b) =>
            {
                if (a.slot != b.slot) return a.slot - b.slot;
                if (a.rarity != b.rarity) return b.rarity - a.rarity;
                return b.Score.CompareTo(a.Score);
            });
            return list;
        }
    }
}
