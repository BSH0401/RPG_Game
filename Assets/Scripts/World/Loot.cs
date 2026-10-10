using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 전리품: 그림자를 쓰러뜨리거나 상자를 열면 먹물 결정(가끔 달빛 조각)이 통통 튀어나와 플레이어에게 빨려 든다.
    /// 밤마다 나오는 황금 그림자는 재료를 잔뜩 쏟고, 낮은 확률로 전설 장비를 떨어뜨린다(4마리마다 한 번은 반드시).
    /// </summary>
    public static class Loot
    {
        public const string Trophy = "golden_trophy";
        public static readonly string[] Legendaries = { "meteor_heart", "comet_quill", "galaxy_feather", "aurora_lamp" };
        /// <summary>황금 그림자를 이만큼 잡을 때마다 전설 장비가 한 번은 반드시 나온다.</summary>
        const int LegendaryPity = 4;
        const float LegendaryChance = 0.2f;

        public static void FromEnemy(EnemyController e)
        {
            if (e == null || e.noLoot) return;
            Vector2 p = e.transform.position;
            if (e.golden)
            {
                Golden(p);
                return;
            }
            if (e.lootCrystals <= 0) return;
            int crystals = Random.Range(Mathf.Max(1, e.lootCrystals - 1), e.lootCrystals + 1);
            int shards = e.isBoss ? Random.Range(2, 4) : (Random.value < e.shardChance ? 1 : 0);
            Drop(p, crystals, shards);
        }

        /// <summary>결정은 최대 다섯 덩이로 나눠 흩뿌리고, 조각은 하나씩.</summary>
        public static void Drop(Vector2 p, int crystals, int shards)
        {
            int pieces = Mathf.Clamp(crystals, 0, 5);
            for (int i = 0; i < pieces; i++)
                LootDrop.Spawn(p, Upgrade.Crystal, crystals / pieces + (i < crystals % pieces ? 1 : 0));
            for (int i = 0; i < shards; i++)
                LootDrop.Spawn(p, Upgrade.Shard, 1);
        }

        static void Golden(Vector2 p)
        {
            GameState.SetFlag(NightDirector.GoldenFlag);
            Drop(p, Random.Range(12, 19), Random.Range(2, 5));
            LootDrop.Spawn(p, Trophy, 1);
            var legend = RollLegendary();
            if (legend != null) LootDrop.Spawn(p, legend, 1, true);
            HUD.Toast("황금 그림자를 쓰러뜨렸다! 전리품이 쏟아진다." + (legend != null ? "  <color=#ffd34a>★ 무언가 눈부시게 빛난다!</color>" : ""));
            CameraFollow.Shake(0.35f, 0.25f);
            RingFx.Spawn(p, 3f, new Color(1f, 0.85f, 0.35f, 0.95f), 0.6f);
            Sound.Play("Delivered", 0.8f);
            GameState.Save();
        }

        /// <summary>아직 없는 전설 장비 중 하나(확률, 또는 천장). 모두 가졌으면 null.</summary>
        static string RollLegendary()
        {
            var missing = new List<string>();
            foreach (var id in Legendaries)
                if (!GameState.HasFlag("got:" + id) && !LootDrop.Pending(id)) missing.Add(id);
            if (missing.Count == 0) return null;
            int owned = Legendaries.Length - missing.Count;
            int kills = GameState.ItemCount(Trophy) + 1; // 이번에 떨어진 증표까지
            bool pity = kills >= LegendaryPity * (owned + 1);
            if (!pity && Random.value >= LegendaryChance) return null;
            return missing[Random.Range(0, missing.Count)];
        }
    }

    /// <summary>바닥에 떨어진 전리품 하나. 튀어 오른 뒤 가까이 가면 빨려 들어와 가방에 들어간다.</summary>
    public class LootDrop : MonoBehaviour
    {
        public static readonly List<LootDrop> All = new List<LootDrop>();

        public string itemId;
        public int amount;
        public bool legendary;

        Transform body;
        SpriteRenderer glow;
        Vector2 ground, slide;
        float height, vz, age, pull;
        bool magnet;
        static float lastSave;

        public static bool Pending(string id) => All.Exists(d => d != null && d.itemId == id);

        public static LootDrop Spawn(Vector2 pos, string itemId, int amount, bool legendary = false)
        {
            var def = GameData.GetItem(itemId);
            var go = new GameObject("Loot_" + itemId);
            go.transform.SetParent(Spawner.Root, false);
            go.transform.position = pos;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            var sr = bodyGo.AddComponent<SpriteRenderer>();
            sr.sprite = (def != null ? GameAssets.ItemSprite(def.icon) : null) ?? Art.Chest(false);
            if (legendary) bodyGo.transform.localScale = Vector3.one * 1.4f;
            go.AddComponent<YSort>();

            Color c = legendary ? new Color(1f, 0.82f, 0.3f, 0.6f)
                : itemId == Upgrade.Crystal ? new Color(0.7f, 0.45f, 1f, 0.4f)
                : itemId == Upgrade.Shard ? new Color(0.6f, 1f, 0.75f, 0.5f)
                : new Color(1f, 0.85f, 0.4f, 0.5f);
            var g = Spawner.Glow(pos, legendary ? 3f : 1.4f, c, go.transform);
            if (legendary)
            {
                // 하늘로 솟는 빛기둥
                var beam = Spawner.Glow(pos + new Vector2(0f, 3f), 1f, new Color(1f, 0.85f, 0.4f, 0.35f), go.transform);
                beam.transform.localScale = new Vector3(1.2f, 9f, 1f);
            }

            var d = go.AddComponent<LootDrop>();
            d.itemId = itemId;
            d.amount = amount;
            d.legendary = legendary;
            d.body = bodyGo.transform;
            d.glow = g;
            d.ground = pos;
            float a = Random.value * Mathf.PI * 2f;
            d.slide = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.2f, legendary ? 1.2f : 3f);
            d.vz = legendary ? 9f : Random.Range(4f, 6.5f);
            return d;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            age += dt;

            // 튀어 오르기(가짜 높이)와 미끄러지기
            if (height > 0f || vz != 0f)
            {
                vz -= 22f * dt;
                height += vz * dt;
                if (height <= 0f)
                {
                    height = 0f;
                    vz = -vz * 0.45f;
                    if (vz < 1.2f) vz = 0f;
                }
            }
            ground += slide * dt;
            slide *= Mathf.Max(0f, 1f - 4f * dt);

            // 가까이 가면 빨려 든다(떨어진 직후에는 잠깐 기다린다)
            var player = PlayerController.I;
            if (player != null && age > 0.5f && !player.Health.IsDead)
            {
                Vector2 to = (Vector2)player.transform.position - ground;
                float dist = to.magnitude;
                if (dist < (legendary ? 1.8f : 3.2f)) magnet = true;
                if (magnet)
                {
                    pull += 28f * dt;
                    ground += to.normalized * Mathf.Min(dist, pull * dt);
                }
                if (dist < 0.45f)
                {
                    Collect();
                    return;
                }
            }

            transform.position = ground;
            float bob = height > 0f ? 0f : Mathf.Sin(age * 4f) * 0.06f;
            body.localPosition = new Vector3(0f, 0.15f + height + bob, 0f);
            if (glow != null)
            {
                var c = glow.color;
                c.a = (legendary ? 0.6f : 0.4f) * (0.75f + 0.25f * Mathf.Sin(age * 5f));
                glow.color = c;
            }
        }

        void Collect()
        {
            var def = GameData.GetItem(itemId);
            Vector2 at = (Vector2)transform.position + Vector2.up * 1.1f;
            if (legendary)
            {
                Inventory.Give(itemId, amount);
                HUD.Toast("<color=#ffd34a>★ 전설 장비 " + Josa.Eul("「" + (def != null ? def.name : itemId) + "」") + " 얻었다! ★</color>");
                HUD.Popup(at, "★ 전설 ★", new Color(1f, 0.85f, 0.3f));
                RingFx.Spawn(transform.position, 3.5f, new Color(1f, 0.9f, 0.5f, 1f), 0.7f);
                CameraFollow.Shake(0.4f, 0.2f);
                Juice.HitStop(0.2f);
            }
            else if (def != null && (def.IsEquipment || itemId == Loot.Trophy))
            {
                Inventory.Give(itemId, amount);
            }
            else
            {
                int max = def != null && def.maxStack > 0 ? def.maxStack : 999;
                GameState.AddItem(itemId, amount, max);
                bool shard = itemId == Upgrade.Shard;
                HUD.Popup(at, "+" + amount + (shard ? " 달빛 조각" : ""), shard ? new Color(0.6f, 1f, 0.75f) : new Color(0.8f, 0.6f, 1f));
                Sound.Play("Clue", shard ? 0.6f : 0.3f);
                if (!GameState.HasFlag("hint:upgrade"))
                {
                    GameState.SetFlag("hint:upgrade");
                    HUD.Toast("먹물 결정을 얻었다! 우체국 옆 <b>강화 작업대</b>에서 장비를 강화할 수 있다.");
                }
                // 결정을 주울 때마다 파일을 쓰지 않도록 몇 초에 한 번만 저장한다.
                if (shard || Time.unscaledTime - lastSave > 4f)
                {
                    lastSave = Time.unscaledTime;
                    GameState.Save();
                }
            }
            Destroy(gameObject);
        }
    }
}
