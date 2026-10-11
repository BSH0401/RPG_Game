using UnityEngine;

namespace MoonlightPost
{
    public enum Stat { Str, Vit, Agi, Luk }

    /// <summary>
    /// 레벨·경험치·스탯. 그림자를 쓰러뜨리면 경험치가 오르고, 레벨이 오를 때마다 스탯 포인트 3개를 받아
    /// 캐릭터 창(C)에서 힘·체력·민첩·행운에 나눠 준다. 공격력·최대 체력·속도·치명타는 모두 여기서 계산한다.
    /// </summary>
    public static class Stats
    {
        public const int PointsPerLevel = 3;
        public const int MaxLevel = 60;
        /// <summary>치명타 피해 배율: 기본 1.7 + 장비의 치명타 피해.</summary>
        public static float CritMultiplier => 1.7f + Gear.Total(GearStat.CritDmg) / 100f;

        public static int Level => Mathf.Max(1, GameState.Data.level);
        public static int Exp => GameState.Data.exp;
        public static int Points => GameState.Data.statPoints;

        /// <summary>level 에서 다음 레벨까지 필요한 경험치.</summary>
        public static int ExpToNext(int level) => Mathf.RoundToInt(12f * Mathf.Pow(level, 1.4f));
        public static int ExpToNext() => ExpToNext(Level);

        public static int Get(Stat s)
        {
            var d = GameState.Data;
            switch (s)
            {
                case Stat.Str: return d.str;
                case Stat.Vit: return d.vit;
                case Stat.Agi: return d.agi;
                default: return d.luk;
            }
        }

        public static string Name(Stat s)
        {
            switch (s)
            {
                case Stat.Str: return "힘";
                case Stat.Vit: return "체력";
                case Stat.Agi: return "민첩";
                default: return "행운";
            }
        }

        /// <summary>포인트 1개가 주는 효과.</summary>
        public static string PerPoint(Stat s)
        {
            switch (s)
            {
                case Stat.Str: return "공격력 +2";
                case Stat.Vit: return "최대 체력 +8";
                case Stat.Agi: return "공격 속도 +2%, 이동 속도 +1.5%";
                default: return "치명타 확률 +1%, 전리품 +3%";
            }
        }

        // ------------------------------------------------------------------ 계산된 능력치

        /// <summary>공격력: (기본 10 + 레벨 + 힘 x2 + 장비) x (1 + 장비의 공격력 %).</summary>
        public static int Attack => Mathf.RoundToInt((10 + (Level - 1) + 2 * Get(Stat.Str) + Inventory.EffectSum("attack") + Gear.Total(GearStat.Attack))
                                                     * (1f + Gear.Total(GearStat.AttackPct) / 100f));

        /// <summary>방어: 장비에서만 나온다. 받는 피해를 방어/(방어+60) 만큼 줄인다(최대 60%).</summary>
        public static int Defense => Mathf.RoundToInt(Gear.Total(GearStat.Defense));
        public static float DamageReduction => Mathf.Min(0.6f, Defense / (Defense + 60f));

        /// <summary>최대 체력: 기본 60 + 레벨당 5 + 체력 x8 + 편지 보상·장비(1 = 10).</summary>
        public static int MaxHp =>
            60 + 5 * (Level - 1) + 8 * Get(Stat.Vit)
            + 10 * Mathf.Max(0, GameState.Data.maxHp - GameState.BaseMaxHp)
            + Mathf.RoundToInt(10f * Inventory.EffectSum("maxHp") + Gear.Total(GearStat.MaxHp));

        public static float MoveSpeedMul => 1f + Mathf.Min(0.6f, 0.015f * Get(Stat.Agi) + Gear.Total(GearStat.MoveSpeed) / 100f);
        public static float AttackSpeedMul => 1f + Mathf.Min(1f, 0.02f * Get(Stat.Agi) + Gear.Total(GearStat.AtkSpeed) / 100f);
        public static float CritChance => Mathf.Min(0.75f, 0.05f + 0.01f * Get(Stat.Luk) + Gear.Total(GearStat.Crit) / 100f);
        public static float LootMul => 1f + 0.03f * Get(Stat.Luk) + Gear.Total(GearStat.LootPct) / 100f;
        public static float ExpMul => 1f + Gear.Total(GearStat.ExpPct) / 100f;
        /// <summary>스킬 위력 % 와 재사용 대기 감소 %(장비).</summary>
        public static float SkillPowerBonus => Gear.Total(GearStat.SkillPower) / 100f;
        public static float CooldownMul => Mathf.Max(0.5f, 1f - Gear.Total(GearStat.Cooldown) / 100f);

        /// <summary>공격력 x mul 에 ±10% 흔들림과 치명타를 굴린다.</summary>
        public static int RollDamage(float mul, out bool crit)
        {
            crit = Random.value < CritChance;
            float dmg = Attack * mul * Random.Range(0.9f, 1.1f);
            if (crit) dmg *= CritMultiplier;
            return Mathf.Max(1, Mathf.RoundToInt(dmg));
        }

        // ------------------------------------------------------------------ 경험치·레벨업

        public static void AddExp(int amount, Vector2 at)
        {
            if (amount <= 0 || Level >= MaxLevel) return;
            amount = Mathf.RoundToInt(amount * ExpMul);
            var d = GameState.Data;
            d.exp += amount;
            HUD.Popup(at + Vector2.up * 0.4f, "+" + amount + " EXP", new Color(0.55f, 0.85f, 1f));
            int ups = 0;
            while (Level < MaxLevel && d.exp >= ExpToNext(d.level))
            {
                d.exp -= ExpToNext(d.level);
                d.level++;
                d.statPoints += PointsPerLevel;
                ups++;
            }
            if (Level >= MaxLevel) d.exp = 0;
            GameState.NotifyChanged();
            if (ups > 0) OnLevelUp();
        }

        static void OnLevelUp()
        {
            var player = PlayerController.I;
            if (player != null)
            {
                player.Health.SetMax(MaxHp, true);
                Vector2 p = player.transform.position;
                RingFx.Spawn(p, 2.6f, new Color(1f, 0.9f, 0.45f, 1f), 0.6f);
                RingFx.Spawn(p, 1.4f, new Color(0.6f, 0.9f, 1f, 1f), 0.45f);
                HUD.Popup(p + Vector2.up * 1.6f, "LEVEL UP!", new Color(1f, 0.88f, 0.35f), 1.6f);
            }
            HUD.LevelUpBanner(Level);
            
            Sound.Play("Delivered", 0.9f);
            CameraFollow.Shake(0.15f, 0.1f);
            GameState.Save();
        }

        /// <summary>나눠 준 스탯 포인트 합계.</summary>
        public static int Spent => GameState.Data.str + GameState.Data.vit + GameState.Data.agi + GameState.Data.luk;

        /// <summary>나눠 준 포인트를 모두 돌려받는다(망각의 두루마리). 돌려받은 수.</summary>
        public static int ResetPoints()
        {
            var d = GameState.Data;
            int n = Spent;
            d.statPoints += n;
            d.str = d.vit = d.agi = d.luk = 0;
            GameState.NotifyChanged();
            return n;
        }

        /// <summary>스탯 포인트 하나를 쓴다.</summary>
        public static bool Spend(Stat s)
        {
            var d = GameState.Data;
            if (d.statPoints <= 0) return false;
            d.statPoints--;
            switch (s)
            {
                case Stat.Str: d.str++; break;
                case Stat.Vit: d.vit++; break;
                case Stat.Agi: d.agi++; break;
                default: d.luk++; break;
            }
            GameState.NotifyChanged();
            return true;
        }
    }
}
