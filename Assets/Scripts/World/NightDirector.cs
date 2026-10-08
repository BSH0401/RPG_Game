using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// "밤마다 익숙한 길이 조금 달라진다." 지도 전체를 무작위로 만들지 않고,
    /// 수작업으로 만든 숲에서 쓰러진 나무(막힌 길), 단서 위치, 적 배치만 밤 번호에 따라 바꾼다.
    /// 같은 밤에는 항상 같은 배치가 나오도록 밤 번호를 시드로 쓴다.
    /// </summary>
    public class NightDirector : MonoBehaviour
    {
        public static NightDirector I { get; private set; }

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
        GameObject boss;
        int respawnCount;

        void Awake() => I = this;

        void Start()
        {
            GameState.NightChanged += Reroll;
            GameState.Changed += RefreshBoss;
            Reroll();
        }

        void OnDestroy()
        {
            GameState.NightChanged -= Reroll;
            GameState.Changed -= RefreshBoss;
            if (I == this) I = null;
        }

        public void Reroll()
        {
            respawnCount = 0;
            var rng = new System.Random(GameState.Night * 7919 + 17);
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

            // 밤이 깊어질수록 열린 길의 적이 조금 늘어난다(최대 3).
            int pathCount = Mathf.Clamp(1 + GameState.Night, 2, 3);
            var openPath = NorthBlocked ? southSpawns : northSpawns;
            foreach (var p in Pick(openPath, pathCount, rng)) enemies.Add(Spawner.Enemy(p, false));
            foreach (var p in Pick(eastSpawns, 1, rng)) enemies.Add(Spawner.Enemy(p, false));

            RefreshBoss();
        }

        void RefreshBoss()
        {
            bool needBoss = GameState.HasFlag("carrying:" + bossLetterId) && !GameState.HasFlag(bossDefeatFlag);
            if (needBoss && boss == null)
            {
                boss = Spawner.Enemy(bossSpawn, true);
            }
            else if (!needBoss && boss != null && !GameState.HasFlag(bossDefeatFlag))
            {
                Destroy(boss);
                boss = null;
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
