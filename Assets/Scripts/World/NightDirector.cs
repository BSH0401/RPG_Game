using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>편지를 들고 있으면 나타나는 보스.</summary>
    public class BossSpec
    {
        public string letterId;
        public string defeatFlag;
        public Vector2 spawn;
        public EnemyKind kind;
        public GameObject instance;
    }

    /// <summary>
    /// 편지 과제용 작은 사건: 그 편지를 들고 있는 동안 정해진 곳에 그림자 떼가 모인다.
    /// 모두 물리치면 flag 가 서고(배달 조건 등에 쓴다) 다시 나오지 않는다.
    /// </summary>
    public class EncounterSpec
    {
        public string letterId;
        /// <summary>letterId 대신 쓸 출현 조건(의뢰용, 예: "req:owen_nest").</summary>
        public string condition;
        public string flag;
        public Vector2[] spawns;
        public EnemyKind[] kinds;
        public string startToast;
        public string clearToast;
        public readonly List<GameObject> live = new List<GameObject>();
        public bool spawned;
    }

    /// <summary>밤의 분위기. 밤마다 하나가 정해져 시야·적 수·숨은 우표 표시가 달라진다.</summary>
    public enum NightMood { Clear, Fog, Fireflies, FullMoon }

    /// <summary>
    /// "밤마다 익숙한 길이 조금 달라진다." 지도 전체를 무작위로 만들지 않고,
    /// 수작업으로 만든 숲에서 쓰러진 나무(막힌 길), 단서 위치, 적 배치, 밤의 분위기만 밤 번호에 따라 바꾼다.
    /// 같은 밤에는 항상 같은 배치가 나오도록 밤 번호를 시드로 쓴다.
    /// </summary>
    public class NightDirector : MonoBehaviour
    {
        public static NightDirector I { get; private set; }

        public static NightMood Mood { get; private set; } = NightMood.Clear;

        public static string MoodName(NightMood m)
        {
            switch (m)
            {
                case NightMood.Fog: return "안개 낀 밤";
                case NightMood.Fireflies: return "반딧불이 밤";
                case NightMood.FullMoon: return "보름달 밤";
                default: return "맑은 밤";
            }
        }

        static string MoodHint(NightMood m)
        {
            switch (m)
            {
                case NightMood.Fog: return "앞이 잘 보이지 않는다. 숲에 그림자가 더 많다.";
                case NightMood.Fireflies: return "반딧불이가 길을 밝힌다. 그림자들이 조금 숨었다.";
                case NightMood.FullMoon: return "달빛에 잃어버린 우표가 반짝인다. [Tab] 지도에 표시된다.";
                default: return "조용한 밤이다. 숲의 길이 바뀌었다.";
            }
        }

        public GameObject northLog;
        public GameObject southLog;
        public Transform flourSack;
        public Vector2 flourSackNorth;
        public Vector2 flourSackSouth;
        public Vector2[] northSpawns;
        public Vector2[] southSpawns;
        public Vector2[] eastSpawns;
        /// <summary>서쪽 해안(우체국 등불을 켠 뒤 열림)의 적 위치.</summary>
        public Vector2[] coastSpawns = new Vector2[0];
        public BossSpec[] bosses = new BossSpec[0];

        // 북쪽 띠(GameBootstrap.North): 폐역 건널목 잔해와 적 위치
        public GameObject stationWestBlock;
        public GameObject stationEastBlock;
        public Vector2[] stationSpawns = new Vector2[0];
        public Vector2[] northForestSpawns = new Vector2[0];
        /// <summary>북쪽 갯바위(해안과 함께 우체국 등불을 켠 뒤 열림).</summary>
        public Vector2[] coastNorthSpawns = new Vector2[0];

        // 동쪽(GameBootstrap.East, 터널이 뚫린 뒤): 별빛 고개의 흔들다리 두 개 중 하나가 끊어진다.
        public GameObject passNorthBridge;
        public GameObject passSouthBridge;
        public Vector2[] passSpawns = new Vector2[0];
        public Vector2[] quarrySpawns = new Vector2[0];

        public EncounterSpec[] encounters = new EncounterSpec[0];

        /// <summary>오늘 밤의 황금 그림자(없거나 쓰러뜨렸으면 null)와 나타난 지역 이름.</summary>
        public static EnemyController Golden { get; private set; }
        public static string GoldenRegion { get; private set; }
        public static string GoldenFlag => "golden:" + GameState.Night;

        public bool NorthBlocked { get; private set; }
        public bool StationWestBlocked { get; private set; }

        // ------------------------------------------------------------------ 지역 레벨

        /// <summary>지역 이름, 기본 레벨, 적이 나오는 자리. 안쪽(동쪽 숲)이 1, 옛 채석장이 11.</summary>
        public IEnumerable<(string name, int level, Vector2[] spots)> Regions()
        {
            yield return ("동쪽 숲", 1, Concat(northSpawns, southSpawns));
            yield return ("숲 끝 오두막 근처", 2, eastSpawns);
            yield return ("폐역", 3, stationSpawns);
            yield return ("북쪽 숲", 4, northForestSpawns);
            yield return ("서쪽 해안", 6, coastSpawns);
            yield return ("북쪽 갯바위", 7, coastNorthSpawns);
            yield return ("별빛 고개", 9, passSpawns);
            yield return ("옛 채석장", 11, quarrySpawns);
        }

        /// <summary>지금 갈 수 있는(적이 나오는) 지역 이름들. 사냥 의뢰가 고른다.</summary>
        public List<string> OpenRegions()
        {
            var list = new List<string> { "동쪽 숲", "숲 끝 오두막 근처", "폐역", "북쪽 숲" };
            if (GameState.HasFlag("lamp_lit")) { list.Add("서쪽 해안"); list.Add("북쪽 갯바위"); }
            if (GameState.HasFlag("tunnel_open")) { list.Add("별빛 고개"); list.Add("옛 채석장"); }
            return list;
        }

        static Vector2[] Concat(Vector2[] a, Vector2[] b)
        {
            var r = new List<Vector2>();
            if (a != null) r.AddRange(a);
            if (b != null) r.AddRange(b);
            return r.ToArray();
        }

        /// <summary>밤이 깊어질수록 모든 지역의 레벨이 오른다(둘째 밤부터 두 밤마다 +1, 최대 +4).</summary>
        public static int NightBonus => Mathf.Clamp((GameState.Night - 1) / 2, 0, 4);

        /// <summary>
        /// p 에 나타나는 적의 레벨과 지역 이름. 가장 가까운 지역의 기본 레벨 + 0~1 + 밤 보너스.
        /// 너무 쉬워지지 않게 「내 레벨 - 3」 아래로는 내려가지 않는다. 보스는 정해진 레벨(내 레벨 - 1 이상).
        /// </summary>
        public static (int level, string region) LevelAt(Vector2 p, EnemyKind kind)
        {
            int player = Stats.Level;
            switch (kind)
            {
                case EnemyKind.Boss: return (Mathf.Max(4, player - 1), "동쪽 숲");
                case EnemyKind.Squid: return (Mathf.Max(8, player - 1), "서쪽 해안");
                case EnemyKind.Wraith: return (Mathf.Max(12, player - 1), "옛 채석장");
            }
            string name = "마을";
            int baseLevel = 1;
            if (I != null)
            {
                float best = 16f;
                foreach (var r in I.Regions())
                {
                    if (r.spots == null) continue;
                    foreach (var s in r.spots)
                    {
                        float d = Vector2.Distance(s, p);
                        if (d < best)
                        {
                            best = d;
                            name = r.name;
                            baseLevel = r.level;
                        }
                    }
                }
            }
            int lv = baseLevel + (Random.value < 0.5f ? 0 : 1) + NightBonus;
            return (Mathf.Max(lv, player - 3), name);
        }

        /// <summary>정예가 나올 확률: 10%, 안개 낀 밤 +5%, 밤마다 +1%(최대 20%).</summary>
        static float EliteChance => Mathf.Min(0.2f, 0.1f + (Mood == NightMood.Fog ? 0.05f : 0f) + 0.01f * GameState.Night);

        /// <summary>적 하나를 만들고, 확률로 정예로 만든다.</summary>
        GameObject Spawn(Vector2 p, EnemyKind kind, System.Random rng)
        {
            var go = Spawner.Enemy(p, kind);
            if (rng.NextDouble() < EliteChance)
                go.GetComponent<EnemyController>().MakeElite((EliteAffix)(1 + rng.Next(4)));
            return go;
        }

        /// <summary>지금 살아 있는 정예들(지도·자동 플레이테스트).</summary>
        public IEnumerable<EnemyController> Elites()
        {
            foreach (var g in enemies)
                if (g != null && g.TryGetComponent<EnemyController>(out var e) && e.IsElite) yield return e;
        }

        /// <summary>지금 살아 있는 보스와 사건의 적(자동 플레이테스트가 쓴다).</summary>
        public IEnumerable<GameObject> EventEnemies()
        {
            foreach (var b in bosses)
                if (b.instance != null) yield return b.instance;
            foreach (var e in encounters)
                foreach (var g in e.live)
                    if (g != null) yield return g;
        }

        readonly List<GameObject> enemies = new List<GameObject>();
        readonly List<GameObject> moodObjects = new List<GameObject>();
        int respawnCount;

        void Awake() => I = this;

        void Start()
        {
            GameState.NightChanged += OnNewNight;
            GameState.Changed += RefreshBoss;
            Reroll();
            Bounties.Refresh();
        }

        void OnDestroy()
        {
            GameState.NightChanged -= OnNewNight;
            GameState.Changed -= RefreshBoss;
            if (I == this) I = null;
        }

        void OnNewNight()
        {
            Reroll();
            Bounties.Refresh();
            HUD.Toast(GameState.Night + "번째 밤 — " + MoodName(Mood) + ". " + MoodHint(Mood));
            if (Golden != null) HUD.Toast("<color=#ffd34a>★ 황금 그림자</color>가 " + GoldenRegion + "에 나타났다!  [Tab] 지도");
        }

        public void Reroll()
        {
            respawnCount = 0;
            var rng = new System.Random(GameState.Night * 7919 + 17);
            Mood = PickMood(rng);
            BuildMoodObjects();
            NorthBlocked = rng.Next(2) == 0;
            northLog.SetActive(NorthBlocked);
            southLog.SetActive(!NorthBlocked);
            // 단서는 항상 열린 길 위에 놓는다.
            flourSack.position = NorthBlocked ? flourSackSouth : flourSackNorth;
            // 폐역: 두 건널목 중 한쪽이 잔해로 막힌다.
            StationWestBlocked = rng.Next(2) == 0;
            if (stationWestBlock != null) stationWestBlock.SetActive(StationWestBlocked);
            if (stationEastBlock != null) stationEastBlock.SetActive(!StationWestBlocked);
            // 별빛 고개: 흔들다리 하나가 끊어진다.
            bool northBridgeBroken = rng.Next(2) == 0;
            if (passNorthBridge != null) passNorthBridge.SetActive(northBridgeBroken);
            if (passSouthBridge != null) passSouthBridge.SetActive(!northBridgeBroken);
            SpawnEnemies(rng);
        }

        /// <summary>플레이어가 쓰러졌을 때 적을 다시 배치한다(같은 밤이어도 위치는 조금 다름).</summary>
        public void ResetEnemies()
        {
            respawnCount++;
            SpawnEnemies(new System.Random(GameState.Night * 7919 + 17 + respawnCount * 31));
        }

        void SpawnEnemies(System.Random rng)
        {
            foreach (var e in enemies)
                if (e != null) Destroy(e);
            enemies.Clear();
            foreach (var b in bosses)
            {
                if (b.instance != null) Destroy(b.instance);
                b.instance = null;
            }
            foreach (var e in encounters) ClearEncounter(e);

            // 밤이 깊어질수록 열린 길의 적이 조금 늘고 종류도 다양해진다.
            int night = GameState.Night;
            int pathCount = Mathf.Clamp(1 + night, 2, 3);
            if (Mood == NightMood.Fog) pathCount++;
            // 갱도의 편지들이 다시 배달 중이 되면(shadows_calm) 섬 전체의 그림자가 줄어든다.
            int calm = GameState.HasFlag("shadows_calm") ? 1 : 0;
            pathCount -= calm;
            if (Mood == NightMood.Fireflies) pathCount--;
            var openPath = NorthBlocked ? southSpawns : northSpawns;
            foreach (var p in Pick(openPath, pathCount, rng))
            {
                double r = rng.NextDouble();
                var kind = r < 0.4 && night >= 1 ? EnemyKind.Bat : (r > 0.8 && night >= 3 ? EnemyKind.Mole : EnemyKind.Rat);
                enemies.Add(Spawn(p, kind, rng));
            }
            // 숲 끝(오두막 근처)에는 둘째 밤부터 그림자 두더지가 숨어 있다.
            foreach (var p in Pick(eastSpawns, night >= 2 ? 2 : 1, rng))
                enemies.Add(Spawn(p, night >= 2 ? EnemyKind.Mole : EnemyKind.Rat, rng));

            // 서쪽 해안: 박쥐와 두더지가 섞여 나온다.
            if (GameState.HasFlag("lamp_lit"))
            {
                int i = 0;
                foreach (var p in Pick(coastSpawns, Mood == NightMood.Fog ? 4 : 3, rng))
                    enemies.Add(Spawn(p, i++ % 2 == 0 ? EnemyKind.Bat : EnemyKind.Mole, rng));
            }

            // 폐역과 북쪽 숲: 들쥐·박쥐, 밤이 깊으면 두더지도.
            foreach (var p in Pick(stationSpawns, Mood == NightMood.Fog ? 3 : 2, rng))
                enemies.Add(Spawn(p, rng.NextDouble() < 0.5 ? EnemyKind.Bat : EnemyKind.Rat, rng));
            foreach (var p in Pick(northForestSpawns, Mathf.Clamp(night, 2, 3) + (Mood == NightMood.Fog ? 1 : 0), rng))
                enemies.Add(Spawn(p, night >= 3 && rng.NextDouble() < 0.4 ? EnemyKind.Mole : (rng.NextDouble() < 0.5 ? EnemyKind.Bat : EnemyKind.Rat), rng));
            if (GameState.HasFlag("lamp_lit"))
                foreach (var p in Pick(coastNorthSpawns, 2, rng))
                    enemies.Add(Spawn(p, rng.NextDouble() < 0.5 ? EnemyKind.Bat : EnemyKind.Mole, rng));

            // 별빛 고개와 옛 채석장: 셋이 섞여 나오고, 채석장은 그림자가 더 짙다.
            if (GameState.HasFlag("tunnel_open"))
            {
                foreach (var p in Pick(passSpawns, 3 - calm + (Mood == NightMood.Fog ? 1 : 0), rng))
                {
                    double r = rng.NextDouble();
                    enemies.Add(Spawn(p, r < 0.4 ? EnemyKind.Bat : r < 0.7 ? EnemyKind.Mole : EnemyKind.Rat, rng));
                }
                foreach (var p in Pick(quarrySpawns, 4 - calm * 2 + (Mood == NightMood.Fog ? 1 : 0), rng))
                {
                    double r = rng.NextDouble();
                    enemies.Add(Spawn(p, r < 0.45 ? EnemyKind.Mole : r < 0.75 ? EnemyKind.Bat : EnemyKind.Rat, rng));
                }
            }

            SpawnGolden(rng);
            RefreshBoss();
        }

        /// <summary>
        /// 밤마다(둘째 밤부터) 열린 지역 중 한 곳에 황금 그림자가 하나 나타난다. 일반 그림자보다 4배 튼튼하고 빠르며,
        /// 쓰러뜨리면 전리품을 쏟는다(Loot). 그 밤에 잡으면 다시 나오지 않는다.
        /// </summary>
        void SpawnGolden(System.Random rng)
        {
            Golden = null;
            GoldenRegion = null;
            if (GameState.Night < 2 || GameState.HasFlag(GoldenFlag)) return;
            var regions = new List<(string name, Vector2[] spots)>
            {
                ("동쪽 숲", NorthBlocked ? southSpawns : northSpawns), ("숲 끝 오두막 근처", eastSpawns),
                ("폐역", stationSpawns), ("북쪽 숲", northForestSpawns),
            };
            if (GameState.HasFlag("lamp_lit"))
            {
                regions.Add(("서쪽 해안", coastSpawns));
                regions.Add(("북쪽 갯바위", coastNorthSpawns));
            }
            if (GameState.HasFlag("tunnel_open"))
            {
                regions.Add(("별빛 고개", passSpawns));
                regions.Add(("옛 채석장", quarrySpawns));
            }
            regions.RemoveAll(r => r.spots == null || r.spots.Length == 0);
            if (regions.Count == 0) return;
            var region = regions[rng.Next(regions.Count)];
            var pos = region.spots[rng.Next(region.spots.Length)];
            double k = rng.NextDouble();
            var kind = k < 0.4 ? EnemyKind.Rat : (k < 0.75 || GameState.Night < 3 ? EnemyKind.Bat : EnemyKind.Mole);
            var go = Spawner.Enemy(pos, kind);
            var e = go.GetComponent<EnemyController>();
            e.golden = true;
            e.displayName = "황금 " + e.displayName;
            e.Health.SetMax(e.Health.Max * 4 + 20, true);
            e.expReward *= 8;
            e.moveSpeed *= 1.15f;
            e.windupTime *= 0.9f;
            e.SetBaseColor(new Color(1f, 0.84f, 0.32f));
            go.transform.localScale = Vector3.one * 1.35f;
            Spawner.Glow(pos + new Vector2(0f, 0.3f), 2.4f, new Color(1f, 0.85f, 0.35f, 0.4f), go.transform);
            enemies.Add(go);
            Golden = e;
            GoldenRegion = region.name;
            e.region = region.name;
        }

        static void ClearEncounter(EncounterSpec e)
        {
            // 목록을 먼저 비워서, 파괴된 적을 "모두 물리쳤다"로 착각하지 않게 한다.
            var old = new List<GameObject>(e.live);
            e.live.Clear();
            e.spawned = false;
            foreach (var g in old)
                if (g != null) Destroy(g);
        }

        void Update()
        {
            foreach (var e in encounters)
            {
                if (!e.spawned) continue;
                e.live.RemoveAll(g => g == null);
                if (e.live.Count > 0) continue;
                e.spawned = false;
                GameState.SetFlag(e.flag);
                GameState.Save();
                if (!string.IsNullOrEmpty(e.clearToast)) HUD.Toast(e.clearToast);
                Sound.Play("Clue");
            }
        }

        void RefreshBoss()
        {
            foreach (var e in encounters)
            {
                bool need = (string.IsNullOrEmpty(e.condition) ? GameState.HasFlag("carrying:" + e.letterId) : GameState.Check(e.condition))
                            && !GameState.HasFlag(e.flag);
                if (need && !e.spawned)
                {
                    e.spawned = true;
                    for (int i = 0; i < e.spawns.Length; i++)
                        e.live.Add(Spawner.Enemy(e.spawns[i], e.kinds[i % e.kinds.Length]));
                    if (!string.IsNullOrEmpty(e.startToast)) HUD.Toast(e.startToast);
                }
                else if (!need && e.spawned) ClearEncounter(e);
            }

            foreach (var b in bosses)
            {
                bool defeated = GameState.HasFlag(b.defeatFlag);
                bool need = GameState.HasFlag("carrying:" + b.letterId) && !defeated;
                if (need && b.instance == null)
                {
                    b.instance = Spawner.Enemy(b.spawn, b.kind);
                }
                else if (!need && b.instance != null && !defeated)
                {
                    Destroy(b.instance);
                    b.instance = null;
                }
            }
        }

        static NightMood PickMood(System.Random rng)
        {
            if (GameState.Night <= 0) return NightMood.Clear;
            double r = rng.NextDouble();
            if (r < 0.35) return NightMood.Clear;
            if (r < 0.6) return NightMood.Fog;
            if (r < 0.82) return NightMood.Fireflies;
            return NightMood.FullMoon;
        }

        /// <summary>반딧불이 밤에는 숲에 반딧불이가 훨씬 많아진다.</summary>
        void BuildMoodObjects()
        {
            foreach (var o in moodObjects)
                if (o != null) Destroy(o);
            moodObjects.Clear();
            if (Mood != NightMood.Fireflies) return;
            var holder = new GameObject("MoodFireflies");
            holder.transform.SetParent(transform, false);
            moodObjects.Add(holder);
            for (int i = 0; i < 40; i++)
            {
                float x = Mathf.Lerp(18f, 51f, PixelCanvas.Hash(i, GameState.Night, 600));
                float y = Mathf.Lerp(-10f, 10f, PixelCanvas.Hash(i, GameState.Night, 601));
                Firefly.Spawn(holder.transform, new Vector2(x, y), new Color(0.85f, 1f, 0.5f), 1.4f, 1f);
            }
        }

        static List<Vector2> Pick(Vector2[] source, int count, System.Random rng)
        {
            var pool = new List<Vector2>(source);
            var result = new List<Vector2>();
            while (result.Count < count && pool.Count > 0)
            {
                int i = rng.Next(pool.Count);
                result.Add(pool[i]);
                pool.RemoveAt(i);
            }
            return result;
        }
    }
}
