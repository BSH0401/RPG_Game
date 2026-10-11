using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 세트 장비 셋. 같은 세트를 2부위 끼면 능력이, 4부위 끼면 특수 효과가 붙는다.
    ///   1 별똥별   2: 치명타 +8%            4: 치명타가 터지면 주변 적에게 별똥별 폭발(공격력 70%)
    ///   2 밤바람   2: 이동·공격 속도 +10%   4: 회피한 뒤 2초 동안 공격력 +40%
    ///   3 등불지기 2: 최대 체력 +50, 방어 +10  4: 처치할 때 최대 체력의 5% 회복, 받는 피해 -10%
    /// 세트 장비는 희귀 이상으로만 나온다: 정예 8%, 황금 그림자 15%, 보스는 하나씩, 그림자 균열 보상 상자, 보스 재도전.
    /// </summary>
    public static class GearSets
    {
        public const int None = 0, Meteor = 1, Wind = 2, Lamp = 3, Count = 3;
        public static readonly string[] Names = { "", "별똥별", "밤바람", "등불지기" };
        public static readonly string[] Colors = { "#ffffff", "#ff9a5c", "#6fd8ff", "#ffd34a" };

        public static string Bonus2(int set)
        {
            switch (set)
            {
                case Meteor: return "치명타 확률 +8%";
                case Wind: return "이동 속도 +10%, 공격 속도 +10%";
                case Lamp: return "최대 체력 +50, 방어 +10";
                default: return "";
            }
        }

        public static string Bonus4(int set)
        {
            switch (set)
            {
                case Meteor: return "치명타가 터지면 주변 적에게 별똥별 폭발(공격력의 70%)";
                case Wind: return "회피한 뒤 2초 동안 공격력 +40%";
                case Lamp: return "처치할 때 최대 체력의 5% 회복, 받는 피해 -10%";
                default: return "";
            }
        }

        /// <summary>2부위 효과를 능력 합계에 더한다(Gear.Total 이 부른다).</summary>
        public static void AddBonuses(float[] sums)
        {
            for (int set = 1; set <= Count; set++)
            {
                if (Equipped(set) < 2) continue;
                switch (set)
                {
                    case Meteor: sums[(int)GearStat.Crit] += 8f; break;
                    case Wind:
                        sums[(int)GearStat.MoveSpeed] += 10f;
                        sums[(int)GearStat.AtkSpeed] += 10f;
                        break;
                    case Lamp:
                        sums[(int)GearStat.MaxHp] += 50f;
                        sums[(int)GearStat.Defense] += 10f;
                        break;
                }
            }
        }

        /// <summary>그 세트를 몇 부위 끼었나.</summary>
        public static int Equipped(int set)
        {
            int n = 0;
            foreach (var g in GameState.Equipped())
                if (g.set == set) n++;
            return n;
        }

        public static bool Has(int set, int pieces) => Equipped(set) >= pieces;

        /// <summary>장비 하나를 세트 장비로 굴린다(희귀 이상, 60% 희귀 · 35% 영웅 · 5% 전설).</summary>
        public static GearItem Roll(int ilvl, int set = 0, GearSlot? slot = null)
        {
            float r = Random.value;
            int rarity = r < 0.05f ? Gear.Legendary : r < 0.4f ? Gear.Epic : Gear.Rare;
            var g = Gear.Roll(ilvl, rarity, slot);
            g.rarity = Mathf.Max(g.rarity, rarity);
            g.set = set > 0 ? set : 1 + Random.Range(0, Count);
            g.prefix = Names[g.set];
            return g;
        }

        // ------------------------------------------------------------------ 4부위 효과

        static float windUntil;

        /// <summary>회피할 때(PlayerController).</summary>
        public static void OnDodge()
        {
            if (!Has(Wind, 4)) return;
            if (Time.time >= windUntil && PlayerController.I != null)
                HUD.Popup((Vector2)PlayerController.I.transform.position + Vector2.up * 1.4f, "밤바람!", new Color(0.45f, 0.85f, 1f));
            windUntil = Time.time + 2f;
        }

        /// <summary>공격 피해 배율(밤바람 4부위).</summary>
        public static float DamageMul => Time.time < windUntil ? 1.4f : 1f;
        public static bool WindActive => Time.time < windUntil;

        /// <summary>받는 피해 배율(등불지기 4부위).</summary>
        public static float DamageTakenMul => Has(Lamp, 4) ? 0.9f : 1f;

        /// <summary>치명타로 맞힌 적 주변 폭발(별똥별 4부위). 이미 맞은 적은 빼고.</summary>
        public static void OnCrit(EnemyController target, int damage, ICollection<EnemyController> already)
        {
            if (!Has(Meteor, 4) || target == null) return;
            Vector2 at = target.transform.position;
            int splash = Mathf.Max(1, Mathf.RoundToInt(damage * 0.7f));
            foreach (var e in new List<EnemyController>(EnemyController.Active))
            {
                if (e == null || e == target || already.Contains(e) || e.Health.IsDead) continue;
                if (Vector2.Distance(e.transform.position, at) > 1.8f) continue;
                e.TakeHit(splash, at, 4f);
            }
            RingFx.Spawn(at, 1.8f, new Color(1f, 0.6f, 0.3f, 0.95f), 0.3f);
            for (int i = 0; i < 5; i++) Spark.Spawn(at + Random.insideUnitCircle * 0.5f, new Color(1f, 0.7f, 0.35f), 0.45f);
        }

        /// <summary>적을 쓰러뜨릴 때(등불지기 4부위).</summary>
        public static void OnKill()
        {
            if (!Has(Lamp, 4)) return;
            var player = PlayerController.I;
            if (player == null || player.Health.IsDead || player.Health.Current >= player.Health.Max) return;
            int heal = Mathf.Max(1, Mathf.RoundToInt(player.Health.Max * 0.05f));
            player.Health.Heal(heal);
            HUD.Popup((Vector2)player.transform.position + Vector2.up * 1.5f, "+" + heal, new Color(1f, 0.85f, 0.4f));
        }

        /// <summary>장비 설명에 붙는 세트 줄(낀 부위 수와 켜진 효과).</summary>
        public static string Describe(int set)
        {
            if (set <= 0) return "";
            int n = Equipped(set);
            string on = "<color=" + Colors[set] + ">", off = "<color=#6a7088>";
            return on + "◆ " + Names[set] + " 세트 (" + n + "/4)</color>\n"
                   + (n >= 2 ? on : off) + "  2부위: " + Bonus2(set) + "</color>\n"
                   + (n >= 4 ? on : off) + "  4부위: " + Bonus4(set) + "</color>\n";
        }
    }
}
