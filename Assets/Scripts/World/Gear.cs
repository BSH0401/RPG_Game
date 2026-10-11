using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MoonlightPost
{
    public enum GearSlot { Weapon, Head, Body, Feet, Ring, Amulet }

    /// <summary>장비에 붙는 능력. 퍼센트 능력은 값이 % 단위.</summary>
    public enum GearStat { Attack, AttackPct, MaxHp, Defense, Crit, CritDmg, AtkSpeed, MoveSpeed, SkillPower, Cooldown, ExpPct, LootPct, LifeOnKill }

    /// <summary>
    /// 몬스터가 떨어뜨리는 장비 한 개. 같은 이름이라도 등급·레벨·옵션이 제각각이다.
    /// stats/values 는 나란한 목록(첫 번째가 주 능력, 나머지가 붙은 옵션).
    /// </summary>
    [Serializable]
    public class GearItem
    {
        public int uid;
        public int slot;
        public int baseIndex;
        public int rarity;
        public int ilvl;
        public string prefix = "";
        public List<int> stats = new List<int>();
        public List<float> values = new List<float>();
        /// <summary>강화 단계(+0 ~ +10)와 연속 실패 횟수(실패 보정).</summary>
        public int plus;
        public int fails;

        /// <summary>강화가 반영된 i 번째 능력 값: 주 능력은 단계마다 +10%, 옵션은 +5%.</summary>
        public float Value(int i) => ValueAt(i, plus);

        public float ValueAt(int i, int level) =>
            Gear.RoundStat((GearStat)stats[i], values[i] * (1f + (i == 0 ? 0.1f : 0.05f) * level));

        public GearSlot Slot => (GearSlot)slot;
        public GearBase Base => Gear.Bases(Slot)[Mathf.Clamp(baseIndex, 0, Gear.Bases(Slot).Length - 1)];
        public string Name => (rarity >= Gear.Legendary ? "★ " : "") + (string.IsNullOrEmpty(prefix) ? "" : prefix + " ") + Base.name + (plus > 0 ? " +" + plus : "");
        public string RichName => "<color=" + Gear.RarityColor(rarity) + ">" + Name + "</color>";

        public float Sum(GearStat s)
        {
            float v = 0f;
            for (int i = 0; i < stats.Count; i++)
                if (stats[i] == (int)s) v += Value(i);
            return v;
        }

        /// <summary>대략적인 강함(비교·정렬용).</summary>
        public float Score
        {
            get
            {
                float score = 0f;
                for (int i = 0; i < stats.Count; i++) score += Value(i) * Gear.Weight((GearStat)stats[i]);
                return score;
            }
        }
    }

    public struct GearBase
    {
        public string name, icon;
        /// <summary>주 능력 배율(무기는 공격력).</summary>
        public float mult;
        public GearBase(string name, string icon, float mult)
        {
            this.name = name;
            this.icon = icon;
            this.mult = mult;
        }
    }

    /// <summary>
    /// 장비 파밍: 종류(6 부위 x 기본형), 등급(일반·고급·희귀·영웅·전설), 무작위 옵션, 떨어뜨릴 확률, 은화 값.
    /// 장비는 GameState 의 가방(gear)에 들고, 부위마다 하나씩 낀다(equipped). 능력 합계는 Stats 가 읽는다.
    /// </summary>
    public static class Gear
    {
        public const int Common = 0, Uncommon = 1, Rare = 2, Epic = 3, Legendary = 4;
        public const int BagSize = 48;
        public static readonly string[] RarityNames = { "일반", "고급", "희귀", "영웅", "전설" };
        static readonly string[] RarityColors = { "#e8e8e8", "#7fe07f", "#6fb4ff", "#c58bff", "#ffa640" };
        static readonly float[] RarityMult = { 1f, 1.12f, 1.27f, 1.45f, 1.7f };
        static readonly int[] AffixCount = { 0, 1, 2, 3, 4 };

        public static string RarityColor(int r) => RarityColors[Mathf.Clamp(r, 0, 4)];
        public static Color RarityTint(int r) => ColorUtility.TryParseHtmlString(RarityColor(r), out var c) ? c : Color.white;

        public static readonly string[] SlotNames = { "무기", "모자", "옷", "신발", "반지", "목걸이" };

        static readonly GearBase[] Weapons =
        {
            new GearBase("우편 칼", "Gear/W_Sword", 1f),
            new GearBase("은빛 칼", "Gear/W_Sword2", 1.05f),
            new GearBase("손도끼", "Gear/W_Axe", 1.12f),
            new GearBase("레이피어", "Gear/W_Rapier", 0.95f),
            new GearBase("망치", "Gear/W_Hammer", 1.18f),
            new GearBase("달빛 카타나", "Gear/W_Katana", 1.1f),
            new GearBase("대검", "Gear/W_BigSword", 1.25f),
        };
        static readonly GearBase[] Heads = { new GearBase("우편 모자", "Gear/A_Helmet", 1f), new GearBase("철 투구", "Gear/A_Helmet", 1.15f) };
        static readonly GearBase[] Bodies = { new GearBase("우체부 외투", "Gear/A_Armor", 1f), new GearBase("가죽 갑옷", "Gear/A_Armor", 1.15f) };
        static readonly GearBase[] Feet = { new GearBase("배달 장화", "Gear/A_Boot", 1f), new GearBase("바람 신발", "Gear/A_Boot", 1.12f) };
        static readonly GearBase[] Rings = { new GearBase("별빛 반지", "Gear/A_Ring", 1f) };
        static readonly GearBase[] Amulets = { new GearBase("달 목걸이", "Gear/A_Amulet", 1f) };

        public static GearBase[] Bases(GearSlot s)
        {
            switch (s)
            {
                case GearSlot.Weapon: return Weapons;
                case GearSlot.Head: return Heads;
                case GearSlot.Body: return Bodies;
                case GearSlot.Feet: return Feet;
                case GearSlot.Ring: return Rings;
                default: return Amulets;
            }
        }

        /// <summary>부위마다 정해진 주 능력.</summary>
        static GearStat MainStat(GearSlot s)
        {
            switch (s)
            {
                case GearSlot.Weapon: return GearStat.Attack;
                case GearSlot.Head: return GearStat.MaxHp;
                case GearSlot.Body: return GearStat.Defense;
                case GearSlot.Feet: return GearStat.MoveSpeed;
                case GearSlot.Ring: return GearStat.Crit;
                default: return GearStat.SkillPower;
            }
        }

        static float MainValue(GearSlot s, int ilvl)
        {
            switch (s)
            {
                case GearSlot.Weapon: return 4f + ilvl * 1.6f;
                case GearSlot.Head: return 10f + ilvl * 4f;
                case GearSlot.Body: return 3f + ilvl * 1.2f;
                case GearSlot.Feet: return 3f + ilvl * 0.12f;
                case GearSlot.Ring: return 2f + ilvl * 0.1f;
                default: return 5f + ilvl * 0.4f;
            }
        }

        static float AffixValue(GearStat s, int ilvl)
        {
            switch (s)
            {
                case GearStat.Attack: return 1f + ilvl * 0.6f;
                case GearStat.AttackPct: return 3f + ilvl * 0.2f;
                case GearStat.MaxHp: return 5f + ilvl * 2.5f;
                case GearStat.Defense: return 1f + ilvl * 0.7f;
                case GearStat.Crit: return 1f + ilvl * 0.08f;
                case GearStat.CritDmg: return 5f + ilvl * 0.5f;
                case GearStat.AtkSpeed: return 2f + ilvl * 0.15f;
                case GearStat.MoveSpeed: return 1.5f + ilvl * 0.08f;
                case GearStat.SkillPower: return 4f + ilvl * 0.4f;
                case GearStat.Cooldown: return 2f + ilvl * 0.15f;
                case GearStat.ExpPct: return 3f + ilvl * 0.3f;
                case GearStat.LootPct: return 4f + ilvl * 0.4f;
                default: return 1f + ilvl * 0.4f; // LifeOnKill
            }
        }

        /// <summary>정수로 보이는 능력(공격력·체력·방어·처치 회복)은 반올림, 나머지는 소수 첫째 자리.</summary>
        static bool IsFlat(GearStat s) => s == GearStat.Attack || s == GearStat.MaxHp || s == GearStat.Defense || s == GearStat.LifeOnKill;

        public static float RoundStat(GearStat s, float v) => Round(s, v);

        static float Round(GearStat s, float v) => IsFlat(s) ? Mathf.Max(1f, Mathf.Round(v)) : Mathf.Round(v * 10f) / 10f;

        /// <summary>비교 점수에서 능력 하나의 무게(공격력 1 ≈ 체력 4 ≈ 방어 1.5 ≈ 치명타 1.2% …).</summary>
        public static float Weight(GearStat s)
        {
            switch (s)
            {
                case GearStat.Attack: return 1f;
                case GearStat.AttackPct: return 0.9f;
                case GearStat.MaxHp: return 0.25f;
                case GearStat.Defense: return 0.7f;
                case GearStat.Crit: return 0.8f;
                case GearStat.CritDmg: return 0.25f;
                case GearStat.AtkSpeed: return 0.6f;
                case GearStat.MoveSpeed: return 0.5f;
                case GearStat.SkillPower: return 0.3f;
                case GearStat.Cooldown: return 0.4f;
                case GearStat.ExpPct: return 0.25f;
                case GearStat.LootPct: return 0.25f;
                default: return 0.6f;
            }
        }

        /// <summary>"공격력 +12" 처럼 한 줄로. v 가 음수면 "-" (비교할 때). 재사용 대기는 줄어드는 것이 좋은 쪽이라 부호가 반대.</summary>
        public static string StatLine(GearStat s, float v)
        {
            string n = Mathf.Abs(v).ToString(IsFlat(s) ? "0" : "0.#");
            string p = v >= 0f ? "+" : "-";
            switch (s)
            {
                case GearStat.Attack: return "공격력 " + p + n;
                case GearStat.AttackPct: return "공격력 " + p + n + "%";
                case GearStat.MaxHp: return "최대 체력 " + p + n;
                case GearStat.Defense: return "방어 " + p + n;
                case GearStat.Crit: return "치명타 확률 " + p + n + "%";
                case GearStat.CritDmg: return "치명타 피해 " + p + n + "%";
                case GearStat.AtkSpeed: return "공격 속도 " + p + n + "%";
                case GearStat.MoveSpeed: return "이동 속도 " + p + n + "%";
                case GearStat.SkillPower: return "스킬 위력 " + p + n + "%";
                case GearStat.Cooldown: return "스킬 재사용 대기 " + (v >= 0f ? "-" : "+") + n + "%";
                case GearStat.ExpPct: return "경험치 " + p + n + "%";
                case GearStat.LootPct: return "전리품 " + p + n + "%";
                default: return "처치할 때 체력 " + p + n;
            }
        }

        static readonly Dictionary<GearStat, string> Prefixes = new Dictionary<GearStat, string>
        {
            { GearStat.Attack, "날카로운" }, { GearStat.AttackPct, "사나운" }, { GearStat.MaxHp, "튼튼한" }, { GearStat.Defense, "단단한" },
            { GearStat.Crit, "예리한" }, { GearStat.CritDmg, "잔혹한" }, { GearStat.AtkSpeed, "재빠른" }, { GearStat.MoveSpeed, "날렵한" },
            { GearStat.SkillPower, "신비한" }, { GearStat.Cooldown, "부지런한" }, { GearStat.ExpPct, "지혜로운" }, { GearStat.LootPct, "행운의" },
            { GearStat.LifeOnKill, "굶주린" },
        };

        // ------------------------------------------------------------------ 만들기

        /// <summary>장비 하나를 굴린다. minRarity 이상, ilvl 레벨.</summary>
        public static GearItem Roll(int ilvl, int minRarity = Common, GearSlot? slot = null)
        {
            ilvl = Mathf.Max(1, ilvl);
            var s = slot ?? (GearSlot)PickSlot();
            var g = new GearItem
            {
                uid = GameState.NextGearUid(),
                slot = (int)s,
                baseIndex = Random.Range(0, Bases(s).Length),
                rarity = Mathf.Max(minRarity, RollRarity()),
                ilvl = ilvl,
            };
            var main = MainStat(s);
            float rm = RarityMult[g.rarity];
            g.stats.Add((int)main);
            g.values.Add(Round(main, MainValue(s, ilvl) * g.Base.mult * rm * Random.Range(0.9f, 1.05f)));
            // 옵션: 주 능력과 다른 것, 겹치지 않게
            var pool = new List<GearStat>((GearStat[])Enum.GetValues(typeof(GearStat)));
            pool.Remove(main);
            for (int i = 0; i < AffixCount[g.rarity] && pool.Count > 0; i++)
            {
                var a = pool[Random.Range(0, pool.Count)];
                pool.Remove(a);
                g.stats.Add((int)a);
                g.values.Add(Round(a, AffixValue(a, ilvl) * Random.Range(0.7f, 1f) * (1f + 0.1f * g.rarity)));
            }
            if (g.stats.Count > 1) g.prefix = Prefixes[(GearStat)g.stats[1]];
            if (g.rarity >= Legendary) g.prefix = "별이 깃든";
            return g;
        }

        static int PickSlot()
        {
            // 무기가 조금 더 자주
            float r = Random.value;
            if (r < 0.25f) return 0;
            return 1 + Random.Range(0, 5);
        }

        /// <summary>등급: 일반 60% · 고급 28% · 희귀 9% · 영웅 2.5% · 전설 0.5% (행운·전리품 옵션이 위쪽 확률을 늘린다).</summary>
        static int RollRarity()
        {
            float luck = Stats.LootMul;
            float[] w = { 60f, 28f, 9f * luck, 2.5f * luck, 0.5f * luck };
            float total = 0f;
            foreach (var x in w) total += x;
            float r = Random.value * total;
            for (int i = 0; i < w.Length; i++)
            {
                if (r < w[i]) return i;
                r -= w[i];
            }
            return 0;
        }

        /// <summary>떨어지는 장비의 레벨: 지금 레벨 근처(±1), 지역이 깊을수록 조금 더.</summary>
        public static int DropLevel(Vector2 at)
        {
            int region = at.x >= 53f ? 3 : at.y > 11f ? 2 : at.x > 17f || at.x < -17f ? 1 : 0;
            return Mathf.Max(1, Stats.Level + Random.Range(-1, 2) + region);
        }

        /// <summary>상점에 팔 때 받는 은화.</summary>
        public static int SellPrice(GearItem g) => Mathf.RoundToInt((4 + g.ilvl * 2) * new[] { 1f, 2f, 4f, 9f, 20f }[g.rarity] * (1f + 0.25f * g.plus));

        // ------------------------------------------------------------------ 끼운 장비의 능력 합계

        static readonly float[] sums = new float[Enum.GetValues(typeof(GearStat)).Length];
        static int sumsVersion = -1;

        public static float Total(GearStat s)
        {
            if (sumsVersion != GameState.GearVersion)
            {
                Array.Clear(sums, 0, sums.Length);
                foreach (var g in GameState.Equipped())
                    for (int i = 0; i < g.stats.Count; i++) sums[g.stats[i]] += g.Value(i);
                sumsVersion = GameState.GearVersion;
            }
            return sums[(int)s];
        }

        /// <summary>장비를 줍는다. 가방이 가득 차면 은화로 바꾼다. 가방에 들어갔으면 true.</summary>
        public static bool Pickup(GearItem g)
        {
            if (GameState.Gear.Count >= BagSize)
            {
                int price = SellPrice(g);
                GameState.AddSilver(price);
                HUD.Toast("장비 가방이 가득 차서 <color=" + RarityColor(g.rarity) + ">" + Josa.Eul(g.Name) + "</color> 은화 " + price + "닢으로 바꿨다.");
                return false;
            }
            GameState.AddGear(g);
            // 빈 부위면 바로 낀다
            if (GameState.EquippedIn(g.Slot) == null) GameState.Equip(g);
            return true;
        }
    }
}
