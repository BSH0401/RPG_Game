using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 장비·편지 도구 강화(+1 ~ +5). 재료는 그림자가 떨어뜨리는 먹물 결정과 달빛 조각.
    /// 단계가 오를수록 비싸고 성공 확률이 낮아진다. 실패하면 재료만 사라지고 장비는 그대로이며,
    /// 실패할 때마다 그 장비의 다음 성공 확률이 오른다(실패 보정). 강화 화면은 UpgradeMenu, 강화하는 곳은 Workbench.
    /// </summary>
    public static class Upgrade
    {
        public const int MaxLevel = 5;
        public const string Crystal = "ink_crystal";
        public const string Shard = "moon_shard";
        public const int FailBonus = 10;

        static readonly int[] CrystalCost = { 10, 20, 35, 55, 80 };
        static readonly int[] ShardCost = { 0, 0, 1, 2, 3 };
        static readonly int[] BaseChance = { 100, 85, 65, 45, 30 };

        public static bool CanUpgrade(ItemDef d) => d != null && (d.IsEquipment || d.IsPart);
        public static int Level(ItemDef d) => d == null ? 0 : GameState.UpgradeLevel(d.id);
        public static bool IsMax(ItemDef d) => Level(d) >= MaxLevel;

        public static int Crystals(ItemDef d) => CrystalCost[Mathf.Clamp(Level(d), 0, MaxLevel - 1)];
        public static int Shards(ItemDef d) => ShardCost[Mathf.Clamp(Level(d), 0, MaxLevel - 1)];

        /// <summary>지금 강화했을 때의 성공 확률(%) — 기본 확률 + 실패 보정.</summary>
        public static int Chance(ItemDef d)
        {
            int level = Level(d);
            if (level >= MaxLevel) return 0;
            return Mathf.Min(100, BaseChance[level] + FailBonus * GameState.UpgradeFails(d.id));
        }

        public static int FailBonusNow(ItemDef d) => FailBonus * GameState.UpgradeFails(d.id);

        public static bool Affordable(ItemDef d) =>
            GameState.ItemCount(Crystal) >= Crystals(d) && GameState.ItemCount(Shard) >= Shards(d);

        /// <summary>강화 단계가 더해 주는 효과(items.json 의 value 에 더한다). 편지 도구 부품은 끼웠을 때 도구 위력 %.</summary>
        public static float Bonus(ItemDef d, int level)
        {
            if (d == null || level <= 0) return 0f;
            if (d.IsPart) return 8f * level;
            switch (d.effect)
            {
                case "dodge": return 5f * level;
                case "tool": return 6f * level;
                case "light": return 0.25f * level;
                case "maxHp": return (level >= 3 ? 1 : 0) + (level >= 5 ? 1 : 0);
                default: return 0f;
            }
        }

        /// <summary>level 단계일 때의 효과를 한 줄로.</summary>
        public static string Describe(ItemDef d, int level)
        {
            float v = d.value + Bonus(d, level);
            if (d.IsPart) return level > 0 ? "끼웠을 때 편지 도구 위력 +" + Bonus(d, level) + "%" : "끼웠을 때 기본 위력";
            switch (d.effect)
            {
                case "dodge": return "회피 시간 +" + v + "%";
                case "tool": return "편지 도구 위력·재사용 대기 +" + v + "%";
                case "light": return "밤 시야 +" + v.ToString("0.##");
                case "maxHp": return "최대 체력 +" + v;
                default: return d.description;
            }
        }

        /// <summary>강화를 한 번 시도한다. 재료가 모자라거나 최대 단계면 false. 결과는 success.</summary>
        public static bool Try(ItemDef d, out bool success)
        {
            success = false;
            if (!CanUpgrade(d) || IsMax(d) || !Affordable(d)) return false;
            int chance = Chance(d);
            GameState.RemoveItem(Crystal, Crystals(d));
            if (Shards(d) > 0) GameState.RemoveItem(Shard, Shards(d));
            success = Random.Range(0, 100) < chance;
            if (success)
            {
                GameState.SetUpgradeFails(d.id, 0);
                GameState.SetUpgradeLevel(d.id, Level(d) + 1);
            }
            else GameState.SetUpgradeFails(d.id, GameState.UpgradeFails(d.id) + 1);
            GameState.Save();
            return true;
        }

        public static bool IsLegendary(ItemDef d) => d != null && d.rarity == "legendary";

        /// <summary>이름 색: 전설은 금색, 그 밖에는 강화 단계(+1~2 초록, +3~4 파랑, +5 주황).</summary>
        public static string NameColor(ItemDef d)
        {
            if (IsLegendary(d)) return "#ffd34a";
            int l = Level(d);
            return l >= 5 ? "#ffa040" : l >= 3 ? "#7fb8ff" : l >= 1 ? "#8fe08f" : "#ffffff";
        }

        /// <summary>"<color>이름 +3</color>" 형태.</summary>
        public static string RichName(ItemDef d)
        {
            int l = Level(d);
            return "<color=" + NameColor(d) + ">" + (IsLegendary(d) ? "★ " : "") + d.name + (l > 0 ? " +" + l : "") + "</color>";
        }
    }
}
