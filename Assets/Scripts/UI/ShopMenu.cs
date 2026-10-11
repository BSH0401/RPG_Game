using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 떠돌이 상인 루카의 상점(Shop). 「사기」: 물약·빵·먹물 결정·장비 상자·스탯 초기화, 「팔기」: 끼지 않은 장비를 은화로.
    /// 마우스로 고르고 누른다. A/D 탭 · Esc 닫기.
    /// </summary>
    public class ShopMenu : MonoBehaviour
    {
        public static ShopMenu I { get; private set; }
        public static bool IsOpen => I != null && I.open;
        public static int LastClosedFrame { get; private set; } = -1;

        bool open;
        int openedFrame;
        int tab;
        string message;
        float messageTime;
        GearItem lastBoxItem;

        struct Offer
        {
            public string name, icon, desc;
            public Func<int> price;
            /// <summary>샀으면 결과 문장, 못 샀으면 null(이유는 메시지로).</summary>
            public Func<string> buy;
        }

        readonly List<Offer> offers = new List<Offer>();

        void Awake()
        {
            I = this;
            offers.Add(new Offer
            {
                name = "회복 물약", icon = "Gear/LifePot", desc = "[R] 로 마시면 체력 60 회복. 최대 10개.",
                price = () => 40, buy = () => GiveItem("life_potion"),
            });
            offers.Add(new Offer
            {
                name = "미라의 크루아상", icon = "Croissant", desc = "[R] 로 먹으면 체력 30 회복. 최대 5개.",
                price = () => 15, buy = () => GiveItem("croissant"),
            });
            offers.Add(new Offer
            {
                name = "먹물 결정 x10", icon = "InkCrystal", desc = "강화 작업대에서 쓰는 재료.",
                price = () => 60,
                buy = () =>
                {
                    GameState.AddItem(Upgrade.Crystal, 10, 999);
                    return "먹물 결정 10개를 샀다.";
                },
            });
            offers.Add(new Offer
            {
                name = "장비 상자", icon = "Gear/GearBox", desc = "지금 레벨의 <color=#6fb4ff>희귀</color> 이상 장비 하나가 무작위로 들어 있다.",
                price = () => 150 + 20 * Stats.Level, buy = () => OpenBox(Gear.Rare),
            });
            offers.Add(new Offer
            {
                name = "빛나는 장비 상자", icon = "Gear/GearBox", desc = "지금 레벨의 <color=#c58bff>영웅</color> 이상 장비 하나. 아주 드물게 전설.",
                price = () => 900 + 80 * Stats.Level, buy = () => OpenBox(Gear.Epic),
            });
            offers.Add(new Offer
            {
                name = "망각의 두루마리", icon = "Gear/ResetScroll", desc = "나눠 준 스탯 포인트를 모두 돌려받는다.",
                price = () => 300,
                buy = () =>
                {
                    if (Stats.Spent == 0)
                    {
                        Say("돌려받을 스탯 포인트가 없다.");
                        return null;
                    }
                    int n = Stats.ResetPoints();
                    return "스탯 포인트 " + n + "개를 돌려받았다. [C] 캐릭터 창에서 다시 나누자.";
                },
            });
        }

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        /// <summary>자동 플레이테스트: 탭을 고른다(0 사기, 1 팔기).</summary>
        public static void ShowTab(int t)
        {
            if (I != null) I.tab = t;
        }

        public static void Open()
        {
            if (I == null) return;
            I.open = true;
            I.openedFrame = Time.frameCount;
            I.message = null;
            I.lastBoxItem = null;
            Time.timeScale = 0f;
            Sound.Play("Talk", 0.6f);
        }

        public static void Close()
        {
            if (I == null || !I.open) return;
            I.open = false;
            LastClosedFrame = Time.frameCount;
            Time.timeScale = HUD.AnyOverlay || GameMenu.IsOpen ? 0f : 1f;
            GameState.Save();
        }

        void Update()
        {
            if (!open || Time.frameCount == openedFrame) return;
            if (GameInput.PausePressed || GameInput.InteractPressed) Close();
            else if (GameInput.MenuLeft || GameInput.MenuRight) tab = 1 - tab;
        }

        void Say(string text)
        {
            message = text;
            messageTime = Time.unscaledTime;
        }

        static string GiveItem(string id)
        {
            var def = GameData.GetItem(id);
            int max = def != null && def.maxStack > 0 ? def.maxStack : 99;
            if (GameState.ItemCount(id) >= max)
            {
                I.Say("더 들 수 없다(최대 " + max + "개).");
                return null;
            }
            GameState.AddItem(id, 1, max);
            return (def != null ? Josa.Eul(def.name) : id) + " 샀다.";
        }

        string OpenBox(int minRarity)
        {
            if (GameState.Gear.Count >= Gear.BagSize)
            {
                Say("장비 가방이 가득 찼다. 「팔기」에서 정리하자.");
                return null;
            }
            var g = Gear.Roll(Stats.Level, minRarity);
            Gear.Pickup(g);
            lastBoxItem = g;
            if (g.rarity >= Gear.Epic) Sound.Play("Delivered", 1f);
            return "상자에서 " + g.RichName + " 나왔다!";
        }

        // ------------------------------------------------------------------ 그리기

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -50;
            Ui.Ensure();
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.02f, 0.06f, 0.78f));
            float w = Mathf.Min(Screen.width - 40 * s, 1000 * s), h = Mathf.Min(Screen.height - 40 * s, 620 * s);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Ui.Panel(box);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 12 * s, w - 40 * s, 34 * s),
                "떠돌이 상인 루카   <size=" + Ui.Px(15) + "><color=#9fb3ff>(클릭으로 사고팔기 · A/D 탭 · Esc 닫기)</color></size>", Ui.Title);
            var silver = new Rect(box.xMax - 200 * s, box.y + 14 * s, 180 * s, 30 * s);
            GearUi.Icon(new Rect(silver.x, silver.y + 3 * s, 24 * s, 24 * s), "Gear/MoneyBag");
            GUI.Label(new Rect(silver.x + 28 * s, silver.y, 150 * s, 30 * s), "<b>은화 " + GameState.Silver + "</b>", Ui.Text);

            // 탭
            string[] names = { "사기", "팔기" };
            var e = Event.current;
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(box.x + 20 * s + i * 130 * s, box.y + 54 * s, 120 * s, 34 * s);
                Ui.Fill(r, i == tab ? new Color(1f, 0.85f, 0.45f, 0.25f) : new Color(1f, 1f, 1f, 0.05f));
                GUI.Label(r, i == tab ? "<b>" + names[i] + "</b>" : "<color=#9aa0b8>" + names[i] + "</color>", Ui.Center);
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                {
                    tab = i;
                    e.Use();
                }
            }
            var body = new Rect(box.x + 16 * s, box.y + 98 * s, box.width - 32 * s, box.height - 150 * s);
            Ui.Fill(body, new Color(0f, 0f, 0f, 0.25f));
            if (tab == 0) DrawBuy(body, s);
            else DrawSell(body, s);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime - messageTime < 4f)
                GUI.Label(new Rect(box.x + 20 * s, box.yMax - 46 * s, box.width - 40 * s, 36 * s), message, Ui.Text);
        }

        void DrawBuy(Rect r, float s)
        {
            var e = Event.current;
            float rowH = Mathf.Min(68 * s, (r.height - 8 * s) / offers.Count);
            for (int i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                int price = o.price();
                bool afford = GameState.Silver >= price;
                var row = new Rect(r.x + 6 * s, r.y + 4 * s + i * rowH, r.width - (lastBoxItem != null ? 120 : 12) * s, rowH - 6 * s);
                bool hot = row.Contains(e.mousePosition);
                Ui.Fill(row, hot ? new Color(1f, 0.85f, 0.5f, 0.16f) : new Color(1f, 1f, 1f, 0.04f));
                GearUi.Icon(new Rect(row.x + 8 * s, row.y + 6 * s, row.height - 12 * s, row.height - 12 * s), o.icon);
                GUI.Label(new Rect(row.x + row.height + 6 * s, row.y + 2 * s, row.width * 0.6f, 28 * s), "<b>" + o.name + "</b>", Ui.Text);
                GUI.Label(new Rect(row.x + row.height + 6 * s, row.y + 28 * s, row.width * 0.65f, 30 * s), o.desc, Ui.Small);
                GUI.Label(new Rect(row.xMax - 170 * s, row.y, 160 * s, row.height),
                    (afford ? "<color=#e8ecff>" : "<color=#ff7a7a>") + "은화 " + price + "</color>",
                    new GUIStyle(Ui.Text) { alignment = TextAnchor.MiddleRight });
                if (hot && e.type == EventType.MouseDown)
                {
                    e.Use();
                    TryBuy(i);
                }
            }
            if (lastBoxItem != null)
            {
                // 상자에서 나온 장비: 오른쪽 아래에 크게
                var cell = new Rect(r.xMax - 100 * s, r.y + 34 * s, 90 * s, 90 * s);
                GUI.Label(new Rect(cell.x - 10 * s, r.y + 6 * s, 110 * s, 26 * s), "<color=#9fb3ff>상자에서</color>", Ui.Center);
                GearUi.Cell(cell, lastBoxItem);
                if (cell.Contains(e.mousePosition)) GearUi.Tooltip(GearUi.Describe(lastBoxItem, true));
            }
        }

        /// <summary>물건 하나를 산다(클릭, 자동 플레이테스트). 샀으면 true.</summary>
        public bool TryBuy(int index)
        {
            if (index < 0 || index >= offers.Count) return false;
            var o = offers[index];
            int price = o.price();
            if (GameState.Silver < price)
            {
                Say("은화가 모자란다.");
                Sound.Play("EnemyHit", 0.4f);
                return false;
            }
            string result = o.buy();
            if (result == null) return false;
            GameState.SpendSilver(price);
            Say(result);
            Sound.Play("Clue", 0.6f);
            GameState.Save();
            return true;
        }

        /// <summary>끼지 않은 장비 중 maxRarity 이하를 모두 판다. 판 개수.</summary>
        public int SellUpTo(int maxRarity)
        {
            int n = 0, total = 0;
            foreach (var g in GearUi.Sorted(false))
                if (g.rarity <= maxRarity)
                {
                    total += Gear.SellPrice(g);
                    GameState.RemoveGear(g);
                    n++;
                }
            if (n > 0)
            {
                GameState.AddSilver(total);
                Say("장비 " + n + "개를 팔아 은화 " + total + "닢을 받았다.");
                Sound.Play("Clue", 0.6f);
                GameState.Save();
            }
            else Say("팔 일반·고급 장비가 없다.");
            return n;
        }

        void DrawSell(Rect r, float s)
        {
            var e = Event.current;
            var list = GearUi.Sorted(false);
            // 일괄 판매 버튼
            var bulk = new Rect(r.xMax - 260 * s, r.y + 6 * s, 250 * s, 34 * s);
            Ui.Panel(bulk);
            GUI.Label(bulk, "일반·고급 모두 팔기", Ui.Center);
            if (e.type == EventType.MouseDown && bulk.Contains(e.mousePosition))
            {
                e.Use();
                SellUpTo(Gear.Uncommon);
                return;
            }
            GUI.Label(new Rect(r.x + 10 * s, r.y + 8 * s, r.width - 280 * s, 30 * s), "<color=#9fb3ff>끼지 않은 장비를 누르면 판다.</color>", Ui.Small);
            if (list.Count == 0)
            {
                GUI.Label(new Rect(r.x + 10 * s, r.y + 50 * s, r.width - 20 * s, 40 * s), "팔 장비가 없다.", Ui.Text);
                return;
            }
            float cs = 62 * s, gap = 8 * s;
            var grid = new Rect(r.x, r.y + 46 * s, r.width, r.height - 46 * s);
            int cols = Mathf.Max(1, Mathf.FloorToInt((grid.width - gap) / (cs + gap)));
            GearItem hover = null;
            for (int i = 0; i < list.Count; i++)
            {
                var g = list[i];
                var c = new Rect(grid.x + gap + (i % cols) * (cs + gap), grid.y + gap + (i / cols) * (cs + gap), cs, cs);
                if (c.yMax > grid.yMax) break;
                GearUi.Cell(c, g);
                if (!c.Contains(e.mousePosition)) continue;
                hover = g;
                if (e.type == EventType.MouseDown)
                {
                    e.Use();
                    int price = Gear.SellPrice(g);
                    GameState.RemoveGear(g);
                    GameState.AddSilver(price);
                    Say(g.RichName + " → 은화 " + price + "닢");
                    Sound.Play("Clue", 0.5f);
                    GameState.Save();
                    hover = null;
                    break;
                }
            }
            if (hover != null) GearUi.Tooltip(GearUi.Describe(hover, true, "<color=#ffd98a>누르면 은화 " + Gear.SellPrice(hover) + "닢에 판다</color>"));
        }
    }

    /// <summary>떠돌이 상인 루카. 말을 걸면 상점이 열린다.</summary>
    public class Shop : Interactable
    {
        public override string DisplayName => "떠돌이 상인 루카";
        public override string Prompt => "떠돌이 상인 루카 (상점)";
        public override void Interact(PlayerController player) => ShopMenu.Open();
    }
}
