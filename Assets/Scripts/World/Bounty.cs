using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>사냥 의뢰 하나(저장됨). type 0 = 종류 사냥, 1 = 지역 사냥, 2 = 정예 사냥, 3 = 황금 그림자.</summary>
    [Serializable]
    public class BountyData
    {
        public int night;
        public int type;
        public int kind;
        public string region;
        public int need;
        public int have;
        public bool claimed;
        /// <summary>보상 크기(은화·경험치는 받을 때의 레벨에 맞춰 정해진다).</summary>
        public float scale = 1f;
        public int rewardRarity;

        public bool Done => have >= need;
        public int RewardSilver => Mathf.RoundToInt((60 + 18 * Stats.Level) * scale / 5f) * 5;
        public int RewardExp => Mathf.RoundToInt(Stats.ExpToNext() * 0.25f * scale);
    }

    /// <summary>
    /// 마을 게시판의 사냥 의뢰. 밤마다 세 개가 새로 붙고(같은 밤에는 늘 같은 의뢰), 따로 받을 필요 없이
    /// 그림자를 쓰러뜨리면 바로 세어진다. 다 채우면 게시판에서 보상(은화 · 경험치 · 장비)을 받는다.
    /// 밤이 바뀌면 끝내지 못한 의뢰는 내려가고, 다 채웠지만 보상을 받지 않은 의뢰는 남는다.
    /// </summary>
    public static class Bounties
    {
        public static List<BountyData> All => GameState.Data.bounties;

        public static int ClaimableCount
        {
            get
            {
                int n = 0;
                foreach (var b in All)
                    if (b.Done && !b.claimed) n++;
                return n;
            }
        }

        /// <summary>오늘 밤의 의뢰가 아직 없으면 붙인다(새 밤, 불러오기, 게시판을 열 때).</summary>
        public static void Refresh()
        {
            var d = GameState.Data;
            if (d.bountyNight == GameState.Night && All.Count > 0) return;
            d.bountyNight = GameState.Night;
            All.RemoveAll(b => b.claimed || (!b.Done && b.night != GameState.Night));
            var rng = new System.Random(GameState.Night * 131 + 7);
            var regions = NightDirector.I != null ? NightDirector.I.OpenRegions() : new List<string> { "동쪽 숲" };

            // 1) 종류 사냥 (둘째 밤부터 황금 그림자가 나오면 반은 그것으로)
            if (GameState.Night >= 2 && NightDirector.Golden != null && rng.NextDouble() < 0.5)
                All.Add(Make(3, 0, NightDirector.GoldenRegion, 1, Gear.Epic, 2.2f));
            else
            {
                var kinds = new List<EnemyKind> { EnemyKind.Rat, EnemyKind.Bat };
                if (GameState.Night >= 2) kinds.Add(EnemyKind.Mole);
                var k = kinds[rng.Next(kinds.Count)];
                All.Add(Make(0, (int)k, null, k == EnemyKind.Mole ? 5 + rng.Next(3) : 8 + rng.Next(5), Gear.Uncommon, 1f));
            }
            // 2) 지역 사냥: 깊은 지역일수록 보상이 크다
            int ri = rng.Next(regions.Count);
            All.Add(Make(1, 0, regions[ri], 6 + rng.Next(3), ri >= 4 ? Gear.Rare : Gear.Uncommon, 1f + 0.12f * ri));
            // 3) 정예 사냥
            All.Add(Make(2, 0, null, GameState.Night >= 3 ? 2 : 1, Gear.Rare, 1.5f));
            GameState.NotifyChanged();
        }

        static BountyData Make(int type, int kind, string region, int need, int rarity, float scale) => new BountyData
        {
            night = GameState.Night, type = type, kind = kind, region = region, need = need, scale = scale, rewardRarity = rarity,
        };

        public static string Title(BountyData b)
        {
            switch (b.type)
            {
                case 0: return Spawner.KindName((EnemyKind)b.kind) + " " + b.need + "마리 사냥";
                case 1: return b.region + "의 그림자 " + b.need + "마리";
                case 2: return "정예 그림자 " + b.need + "마리";
                default: return "황금 그림자 (" + b.region + ")";
            }
        }

        public static string Hint(BountyData b)
        {
            switch (b.type)
            {
                case 0: return "어느 지역이든 괜찮다.";
                case 1: return "그 지역에서 쓰러뜨린 그림자만 센다.";
                case 2: return "빛나는 테두리의 정예 그림자. 날쌘 · 거대한 · 사나운 · 갈라지는.";
                default: return "오늘 밤 나타난 황금 그림자. [Tab] 지도에 ★.";
            }
        }

        public static string Reward(BountyData b) =>
            "은화 " + b.RewardSilver + " · 경험치 " + b.RewardExp + " · <color=" + Gear.RarityColor(b.rewardRarity) + ">" + Gear.RarityNames[b.rewardRarity] + "</color> 이상 장비";

        /// <summary>그림자를 쓰러뜨릴 때마다(EnemyController.OnDied).</summary>
        public static void OnKill(EnemyController e)
        {
            if (e == null || e.noLoot) return;
            foreach (var b in All)
            {
                if (b.claimed || b.Done || b.night != GameState.Night) continue;
                bool match =
                    b.type == 0 ? !e.isBoss && (int)e.kind == b.kind :
                    b.type == 1 ? !e.isBoss && e.region == b.region :
                    b.type == 2 ? e.IsElite :
                    e.golden;
                if (!match) continue;
                b.have++;
                if (b.Done)
                {
                    HUD.Toast("<color=#ffd98a>사냥 의뢰 달성!</color> " + Title(b) + " — 마을 게시판에서 보상을 받자.");
                    Sound.Play("Clue", 0.8f);
                    GameState.Save();
                }
            }
        }

        /// <summary>다 채운 의뢰의 보상을 받는다. 받았으면 그 장비(가방이 차면 은화로 바뀌어 null).</summary>
        public static bool Claim(BountyData b, out GearItem gear)
        {
            gear = null;
            if (b == null || !b.Done || b.claimed) return false;
            b.claimed = true;
            GameState.Data.bountiesDone++;
            int silver = b.RewardSilver, exp = b.RewardExp;
            GameState.AddSilver(silver);
            var player = PlayerController.I;
            Stats.AddExp(exp, player != null ? (Vector2)player.transform.position : Vector2.zero);
            var g = Gear.Roll(Stats.Level, b.rewardRarity);
            if (Gear.Pickup(g)) gear = g;
            Sound.Play("Delivered", 0.8f);
            GameState.Save();
            return true;
        }
    }

    /// <summary>
    /// 사냥 게시판 화면. 오늘 밤의 의뢰와 진행도, 보상. 다 채운 의뢰를 누르면 보상을 받는다. Esc · E 닫기.
    /// </summary>
    public class BountyMenu : MonoBehaviour
    {
        public static BountyMenu I { get; private set; }
        public static bool IsOpen => I != null && I.open;
        public static int LastClosedFrame { get; private set; } = -1;

        bool open;
        int openedFrame;
        string message;
        float messageTime;
        GearItem lastGear;

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Open()
        {
            if (I == null) return;
            Bounties.Refresh();
            I.open = true;
            I.openedFrame = Time.frameCount;
            I.message = null;
            I.lastGear = null;
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
        }

        /// <summary>i 번째 의뢰의 보상을 받는다(클릭, 자동 플레이테스트).</summary>
        public bool TryClaim(int i)
        {
            var list = Bounties.All;
            if (i < 0 || i >= list.Count) return false;
            var b = list[i];
            if (!b.Done)
            {
                message = "아직 다 채우지 못했다 (" + b.have + "/" + b.need + ").";
                messageTime = Time.unscaledTime;
                return false;
            }
            int lv = Stats.Level, silver = b.RewardSilver, exp = b.RewardExp;
            if (!Bounties.Claim(b, out var g)) return false;
            lastGear = g;
            message = "보상: 은화 " + silver + " · 경험치 " + exp
                      + (g != null ? " · " + g.RichName : " · (장비 가방이 가득 차 은화로 바꿨다)")
                      + (Stats.Level > lv ? "   <color=#ffd34a>레벨 업!</color>" : "");
            messageTime = Time.unscaledTime;
            return true;
        }

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -50;
            Ui.Ensure();
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.02f, 0.06f, 0.78f));
            float w = Mathf.Min(Screen.width - 40 * s, 1000 * s), h = Mathf.Min(Screen.height - 40 * s, 600 * s);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Ui.Panel(box);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 12 * s, w - 40 * s, 34 * s),
                "사냥 게시판   <size=" + Ui.Px(15) + "><color=#9fb3ff>(밤마다 새 의뢰 · 다 채운 의뢰를 누르면 보상 · Esc 닫기)</color></size>", Ui.Title);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 48 * s, w - 40 * s, 26 * s),
"<color=#9aa0b8>" + (GameState.Night > 0 ? GameState.Night + "번째 밤의 의뢰" : "첫 밤 전 의뢰") + " · 지금까지 끝낸 사냥 의뢰 " + GameState.Data.bountiesDone + "개 · 지역 레벨 +" + NightDirector.NightBonus + "</color>", Ui.Small);

            var e = Event.current;
            var list = Bounties.All;
            var body = new Rect(box.x + 16 * s, box.y + 82 * s, box.width - 32 * s - (lastGear != null ? 120 * s : 0f), box.height - 140 * s);
            float rowH = Mathf.Min(104 * s, body.height / Mathf.Max(3, list.Count));
            if (list.Count == 0)
                GUI.Label(new Rect(body.x + 10 * s, body.y + 10 * s, body.width - 20 * s, 40 * s), "오늘 밤은 붙은 의뢰가 없다.", Ui.Text);
            for (int i = 0; i < list.Count; i++)
            {
                var b = list[i];
                var row = new Rect(body.x, body.y + i * rowH, body.width, rowH - 8 * s);
                bool ready = b.Done && !b.claimed;
                bool hot = row.Contains(e.mousePosition);
                Ui.Fill(row, b.claimed ? new Color(1f, 1f, 1f, 0.02f)
                    : ready ? new Color(1f, 0.85f, 0.4f, hot ? 0.28f : 0.18f)
                    : new Color(1f, 1f, 1f, hot ? 0.08f : 0.05f));
                var icon = b.type == 3 ? "GoldenStamp" : b.type == 2 ? "Gear/GearBox" : "Scroll";
                GearUi.Icon(new Rect(row.x + 10 * s, row.y + 12 * s, 48 * s, 48 * s), icon);
                float tx = row.x + 72 * s, tw = row.width - 72 * s - 200 * s;
                GUI.Label(new Rect(tx, row.y + 4 * s, tw, 30 * s), (b.claimed ? "<color=#6a7088>" : "<b>") + Bounties.Title(b) + (b.claimed ? " (끝)</color>" : "</b>")
                    + (b.night != GameState.Night && !b.claimed ? "  <color=#9aa0b8>(지난 밤)</color>" : ""), Ui.Text);
                GUI.Label(new Rect(tx, row.y + 32 * s, tw, 24 * s), "<color=#9fb3ff>" + Bounties.Hint(b) + "</color>", Ui.Small);
                GUI.Label(new Rect(tx, row.y + 56 * s, tw + 180 * s, 24 * s), "보상: " + Bounties.Reward(b), Ui.Small);
                // 진행 막대
                var bar = new Rect(row.xMax - 190 * s, row.y + 16 * s, 170 * s, 16 * s);
                Ui.Fill(bar, new Color(0f, 0f, 0f, 0.5f));
                Ui.Fill(new Rect(bar.x + 2 * s, bar.y + 2 * s, (bar.width - 4 * s) * Mathf.Clamp01((float)b.have / b.need), bar.height - 4 * s),
                    b.Done ? new Color(1f, 0.82f, 0.3f) : new Color(0.45f, 0.65f, 1f));
                GUI.Label(new Rect(bar.x, bar.yMax + 2 * s, bar.width, 26 * s),
                    b.claimed ? "<color=#6a7088>받음</color>" : ready ? "<color=#ffd34a><b>누르면 보상 받기</b></color>" : Mathf.Min(b.have, b.need) + " / " + b.need, Ui.Center);
                if (hot && e.type == EventType.MouseDown)
                {
                    e.Use();
                    if (!b.claimed) TryClaim(i);
                }
            }
            if (lastGear != null)
            {
                var cell = new Rect(box.xMax - 116 * s, body.y + 30 * s, 90 * s, 90 * s);
                GUI.Label(new Rect(cell.x - 10 * s, body.y + 2 * s, 110 * s, 26 * s), "<color=#9fb3ff>보상 장비</color>", Ui.Center);
                GearUi.Cell(cell, lastGear);
                if (cell.Contains(e.mousePosition)) GearUi.Tooltip(GearUi.Describe(lastGear, true));
            }
            if (!string.IsNullOrEmpty(message) && Time.unscaledTime - messageTime < 5f)
                GUI.Label(new Rect(box.x + 20 * s, box.yMax - 50 * s, box.width - 40 * s, 40 * s), message, Ui.Text);
        }
    }

    /// <summary>우체국 앞 사냥 게시판. 말을 걸면(E) 사냥 의뢰 화면이 열린다.</summary>
    public class BountyBoard : Interactable
    {
        public override string DisplayName => "사냥 게시판";
        public override string Prompt => "사냥 게시판 (사냥 의뢰)";
        public override void Interact(PlayerController player) => BountyMenu.Open();
    }

    /// <summary>위아래로 천천히 떠다닌다(게시판 위 두루마리).</summary>
    public class Bob : MonoBehaviour
    {
        Vector3 start;

        void Start() => start = transform.localPosition;

        void Update() => transform.localPosition = start + Vector3.up * (0.08f * Mathf.Sin(Time.time * 2.4f));
    }
}
