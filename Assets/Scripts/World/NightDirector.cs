using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
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
                default: return "조용한 밤이다.";
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
        public Vector2 bossSpawn;
        public string bossLetterId = "noah_letter";
        public string bossDefeatFlag = "boss_forest_defeated";

        public bool NorthBlocked { get; private set; }

        readonly List<GameObject> enemies = new List<GameObject>();
        readonly List<GameObject> moodObjects = new List<GameObject>();
        GameObject boss;
        int respawnCount;

        void Awake() => I = this;

        void Start()
        {
            GameState.NightChanged += OnNewNight;
            GameState.Changed += RefreshBoss;
            Reroll();
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
            HUD.Toast(GameState.Night + "번째 밤 — " + MoodName(Mood) + ". " + MoodHint(Mood));
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
            if (boss != null) Destroy(boss);
            boss = null;

            // 밤이 깊어질수록 열린 길의 적이 조금 늘고 종류도 다양해진다.
            int night = GameState.Night;
            int pathCount = Mathf.Clamp(1 + night, 2, 3);
            if (Mood == NightMood.Fog) pathCount++;
            if (Mood == NightMood.Fireflies) pathCount--;
            var openPath = NorthBlocked ? southSpawns : northSpawns;
            foreach (var p in Pick(openPath, pathCount, rng))
            {
                double r = rng.NextDouble();
                var kind = r < 0.4 && night >= 1 ? EnemyKind.Bat : (r > 0.8 && night >= 3 ? EnemyKind.Mole : EnemyKind.Rat);
                enemies.Add(Spawner.Enemy(p, kind));
            }
            // 숲 끝(오두막 근처)에는 둘째 밤부터 그림자 두더지가 숨어 있다.
            foreach (var p in Pick(eastSpawns, night >= 2 ? 2 : 1, rng))
                enemies.Add(Spawner.Enemy(p, night >= 2 ? EnemyKind.Mole : EnemyKind.Rat));

            RefreshBoss();
        }

        void RefreshBoss()
        {
            bool needBoss = GameState.HasFlag("carrying:" + bossLetterId) && !GameState.HasFlag(bossDefeatFlag);
            if (needBoss && boss == null)
            {
                boss = Spawner.Enemy(bossSpawn, EnemyKind.Boss);
            }
            else if (!needBoss && boss != null && !GameState.HasFlag(bossDefeatFlag))
            {
                Destroy(boss);
                boss = null;
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
