using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 가방(I) 의 「장비」 칸: 왼쪽은 부위별로 낀 장비, 오른쪽은 장비 가방.
    /// 가방의 장비를 누르면 낀다(같은 부위의 것과 바뀐다). 낀 장비를 누르면 벗는다. 마우스를 올리면 지금 것과 비교.
    /// </summary>
    public partial class HUD
    {
        /// <summary>0 = 장비, 1 = 소지품.</summary>
        int bagTab;

        /// <summary>가방을 연다(자동 플레이테스트). tab 0 = 장비, 1 = 소지품.</summary>
        public static void ShowBag(bool open, int tab = 0)
        {
            if (I != null) I.bagTab = tab;
            SetBag(open);
        }

        void DrawBag()
        {
            float s = Ui.S;
            if (bagTab == 0) DrawGearTab();
            else DrawItems();

            // 위쪽 탭
            string[] tabs = { "장비", "소지품" };
            float tw = 150 * s, th = 38 * s;
            float x0 = Screen.width * 0.5f - tw - 6 * s;
            for (int i = 0; i < tabs.Length; i++)
            {
                var r = new Rect(x0 + i * (tw + 12 * s), 10 * s, tw, th);
                Ui.Panel(r);
                if (i == bagTab) Ui.Fill(new Rect(r.x + 3 * s, r.y + 3 * s, r.width - 6 * s, r.height - 6 * s), new Color(1f, 0.85f, 0.45f, 0.25f));
                GUI.Label(r, (i == bagTab ? "<b>" : "<color=#9aa0b8>") + tabs[i] + (i == bagTab ? "</b>" : "</color>"), Ui.Center);
                var e = Event.current;
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    bagTab = i;
                    Sound.Play("Talk", 0.4f);
                    e.Use();
                }
            }
        }

        void DrawGearTab()
        {
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.08f, 0.8f));
            float w = Mathf.Min(Screen.width - 40 * s, 1160 * s), h = Screen.height - 76 * s;
            var box = new Rect((Screen.width - w) * 0.5f, 58 * s, w, h - 8 * s);
            Ui.Panel(box);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 10 * s, w - 40 * s, 30 * s),
                "장비   <size=" + Ui.Px(15) + "><color=#9fb3ff>(클릭: 끼기/벗기 · 마우스를 올리면 비교 · A/D 탭 · I 닫기)</color></size>", Ui.Title);
            var silver = new Rect(box.xMax - 260 * s, box.y + 12 * s, 240 * s, 28 * s);
            GearUi.Icon(new Rect(silver.x, silver.y + 4 * s, 20 * s, 20 * s), "Gear/MoneyBag");
            GUI.Label(new Rect(silver.x + 26 * s, silver.y, 220 * s, 28 * s), "은화 " + GameState.Silver + "    가방 " + GameState.Gear.Count + "/" + Gear.BagSize, Ui.Text);

            GearItem hover = null;
            var e = Event.current;

            // 왼쪽: 부위별 낀 장비
            var left = new Rect(box.x + 16 * s, box.y + 50 * s, 330 * s, box.height - 66 * s);
            Ui.Fill(left, new Color(0f, 0f, 0f, 0.25f));
            float rowH = Mathf.Min(70 * s, (left.height - 8 * s) / 6f);
            for (int i = 0; i < 6; i++)
            {
                var slot = (GearSlot)i;
                var g = GameState.EquippedIn(slot);
                var row = new Rect(left.x + 6 * s, left.y + 4 * s + i * rowH, left.width - 12 * s, rowH - 6 * s);
                var cell = new Rect(row.x, row.y, row.height, row.height);
                GearUi.Cell(cell, g);
                GUI.Label(new Rect(cell.xMax + 10 * s, row.y + 2 * s, row.width - cell.width - 14 * s, 22 * s), "<color=#9aa0b8>" + Gear.SlotNames[i] + "</color>", Ui.Small);
                GUI.Label(new Rect(cell.xMax + 10 * s, row.y + 20 * s, row.width - cell.width - 14 * s, 26 * s),
                    g != null ? "<b>" + g.RichName + "</b>" : "<color=#6a7088>비어 있음</color>", Ui.Text);
                if (g != null)
                    GUI.Label(new Rect(cell.xMax + 10 * s, row.y + 44 * s, row.width - cell.width - 14 * s, 22 * s),
                        Gear.StatLine((GearStat)g.stats[0], g.values[0]), Ui.Small);
                if (g != null && row.Contains(e.mousePosition))
                {
                    hover = g;
                    if (e.type == EventType.MouseDown)
                    {
                        GameState.Unequip(slot);
                        Sound.Play("Talk", 0.5f);
                        e.Use();
                    }
                }
            }

            // 오른쪽: 장비 가방(격자)
            var right = new Rect(left.xMax + 14 * s, left.y, box.xMax - left.xMax - 30 * s, left.height);
            Ui.Fill(right, new Color(0f, 0f, 0f, 0.25f));
            var list = GearUi.Sorted(true);
            if (list.Count == 0)
                GUI.Label(new Rect(right.x + 14 * s, right.y + 14 * s, right.width - 28 * s, 80 * s),
                    "아직 장비가 없다. 그림자를 쓰러뜨리면 가끔 장비가 떨어진다. 황금 그림자와 보스는 반드시 떨어뜨린다.", Ui.Text);
            float cs = 62 * s, gap = 8 * s;
            int cols = Mathf.Max(1, Mathf.FloorToInt((right.width - gap) / (cs + gap)));
            for (int i = 0; i < list.Count; i++)
            {
                var g = list[i];
                var r = new Rect(right.x + gap + (i % cols) * (cs + gap), right.y + gap + (i / cols) * (cs + gap), cs, cs);
                if (r.yMax > right.yMax) break;
                bool eq = GameState.IsEquipped(g);
                GearUi.Cell(r, g, eq);
                if (!r.Contains(e.mousePosition)) continue;
                hover = g;
                if (e.type == EventType.MouseDown)
                {
                    if (eq) GameState.Unequip(g.Slot);
                    else
                    {
                        GameState.Equip(g);
                        if (g.rarity >= Gear.Epic) RingFx.Spawn(PlayerController.I.transform.position, 1.6f, Gear.RarityTint(g.rarity), 0.4f);
                    }
                    Sound.Play(eq ? "Talk" : "Tool", 0.5f);
                    GameState.Save();
                    e.Use();
                }
            }

            // 아래: 지금 능력 요약
            var sum = new Rect(right.x, right.yMax - 34 * s, right.width, 30 * s);
            GUI.Label(new Rect(sum.x + 10 * s, sum.y, sum.width - 20 * s, sum.height),
                "<color=#9fb3ff>공격력 " + Stats.Attack + "  ·  최대 체력 " + GameState.MaxHp + "  ·  방어 " + Stats.Defense +
                "  ·  치명타 " + Mathf.RoundToInt(Stats.CritChance * 100f) + "%</color>", Ui.Small);

            if (hover != null)
                GearUi.Tooltip(GearUi.Describe(hover, !GameState.IsEquipped(hover),
                    GameState.IsEquipped(hover) ? "<color=#ffd34a>끼고 있음 — 누르면 벗는다</color>" : "<color=#9fb3ff>누르면 낀다</color>   팔면 은화 " + Gear.SellPrice(hover)));
        }
    }
}
