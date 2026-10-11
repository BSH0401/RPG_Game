using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 그림자 균열: 마을 동쪽의 보랏빛 균열로 들어가는 반복 사냥터(섬 남쪽 바다 밑의 둥근 돌 마당, GameBootstrap.Rift).
    ///   단계 N: 몬스터 레벨 2 + 2N, 세 파도(마지막 파도는 정예와 3단계부터 보스). 다 물리치면 보상 상자와 돌아가는 균열이 열린다.
    ///   깬 단계 + 1 까지 고를 수 있다. 균열 안에서 쓰러지면 보상 없이 마을로 돌아간다.
    ///   보스 재도전: 쓰러뜨린 보스를 더 높은 레벨로 다시 불러낸다(밤마다 보스마다 한 번). 세트 장비 2개를 반드시 준다.
    /// </summary>
    public class RiftManager : MonoBehaviour
    {
        public static RiftManager I { get; private set; }
        public static bool Active => I != null && I.active;
        public const string RegionName = "그림자 균열";

        /// <summary>균열 마당의 가운데와 반지름(GameBootstrap.Rift 가 만든다).</summary>
        public static Vector2 Center = new Vector2(0f, -80f);
        public const float Radius = 8.5f;

        public static int Best => GameState.Data.riftBest;
        public static int MonsterLevel(int tier) => 2 + 2 * tier;
        public static float SetChance(int tier) => Mathf.Min(0.6f, 0.2f + 0.05f * tier);
        public static int RewardRarity(int tier) => tier >= 8 ? Gear.Epic : tier >= 3 ? Gear.Rare : Gear.Uncommon;

        public static readonly EnemyKind[] BossKinds = { EnemyKind.Boss, EnemyKind.Squid, EnemyKind.Wraith };
        public static readonly string[] BossFlags = { "boss_forest_defeated", "boss_coast_defeated", "boss_quarry_defeated" };
        public static readonly string[] BossNames = { "먹물 그림자", "심해 먹물 문어", "부치지 못한 편지의 그림자" };
        static readonly int[] BossBaseLevel = { 6, 10, 14 };

        public static bool BossUnlocked(int i) => GameState.HasFlag(BossFlags[i]);
        public static bool BossDoneTonight(int i) => GameState.HasFlag("rematch:" + i + ":" + GameState.Night);
        public static int BossLevel(int i) => Mathf.Max(BossBaseLevel[i], Stats.Level + 2);

        bool active;
        int tier, bossIndex = -1, wave;
        Vector2 returnPos;
        readonly List<GameObject> live = new List<GameObject>();
        readonly List<GameObject> props = new List<GameObject>();
        Coroutine run;

        /// <summary>지금 균열의 단계(보스 재도전이면 0)와 파도.</summary>
        public static int Tier => I != null ? I.tier : 0;
        public static int Wave => I != null ? I.wave : 0;
        public static bool Cleared { get; private set; }
        /// <summary>마당에 살아 있는 적(갈라진 작은 그림자까지).</summary>
        public static IEnumerable<GameObject> Live
        {
            get
            {
                foreach (var e in EnemyController.Active)
                    if (e != null && !e.Health.IsDead && InArena(e.transform.position)) yield return e.gameObject;
            }
        }

        static bool InArena(Vector2 p) => Vector2.Distance(p, Center) < Radius + 2f;

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        // ------------------------------------------------------------------ 들어가기 · 나가기

        public static void Enter(int tier) => I?.Begin(tier, -1);
        public static void EnterBoss(int index) => I?.Begin(0, index);

        void Begin(int t, int boss)
        {
            if (active) return;
            var player = PlayerController.I;
            if (player == null) return;
            active = true;
            Cleared = false;
            tier = t;
            bossIndex = boss;
            wave = 0;
            returnPos = player.transform.position;
            ClearAll();
            Move(player, Center + new Vector2(0f, -Radius + 2f));
            HUD.Toast(boss >= 0
                ? "<color=#c58bff>그림자 균열</color> — " + BossNames[boss] + " 재도전 (Lv." + BossLevel(boss) + ")"
                : "<color=#c58bff>그림자 균열 " + t + "단계</color> — 몬스터 Lv." + MonsterLevel(t) + ", 세 파도를 버티자!");
            Sound.Play("BossSlam", 0.6f);
            run = StartCoroutine(boss >= 0 ? BossRun(boss) : Waves(t));
        }

        static void Move(PlayerController player, Vector2 p)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            rb.position = p;
            player.transform.position = p;
            rb.SetVelocity(Vector2.zero);
            if (CameraFollow.I != null) CameraFollow.I.SnapToTarget();
        }

        /// <summary>돌아가는 균열로 나간다(다 깬 뒤).</summary>
        public static void Leave()
        {
            if (I == null || !I.active) return;
            I.active = false;
            if (I.run != null) I.StopCoroutine(I.run);
            I.ClearAll();
            var player = PlayerController.I;
            if (player != null) Move(player, I.returnPos);
            Sound.Play("LetterGet", 0.6f);
        }

        /// <summary>균열 안에서 쓰러졌다: 보상 없이 끝(배달부는 PlayerController 가 우체국으로 옮긴다).</summary>
        public static void PlayerDied()
        {
            if (I == null || !I.active) return;
            I.active = false;
            if (I.run != null) I.StopCoroutine(I.run);
            I.ClearAll();
            HUD.Toast("균열에서 밀려났다… 보상은 없다.");
        }

        void ClearAll()
        {
            foreach (var g in live)
                if (g != null) Destroy(g);
            live.Clear();
            foreach (var e in new List<EnemyController>(EnemyController.Active))
                if (e != null && InArena(e.transform.position)) Destroy(e.gameObject);
            foreach (var g in props)
                if (g != null) Destroy(g);
            props.Clear();
            // 마당에 남은 전리품은 주인에게 날아간다
            foreach (var d in new List<LootDrop>(LootDrop.All))
                if (d != null && Vector2.Distance(d.transform.position, Center) < Radius + 3f) d.Collect();
        }

        // ------------------------------------------------------------------ 파도

        IEnumerator Waves(int t)
        {
            int lv = MonsterLevel(t);
            for (wave = 1; wave <= 3; wave++)
            {
                HUD.Toast("<color=#c58bff>" + wave + "번째 파도</color>" + (wave == 3 ? " — 마지막!" : ""));
                yield return new WaitForSeconds(1.2f);
                int normals = 3 + t / 2 + wave;
                for (int i = 0; i < normals; i++)
                    SpawnAt(RandomSpot(), RandomKind(), lv, wave == 3 ? EliteAffix.None : (Random.value < 0.08f * t ? RandomAffix() : EliteAffix.None));
                if (wave >= 2)
                    for (int i = 0; i < (wave == 3 ? 1 + t / 3 : 1); i++)
                        SpawnAt(RandomSpot(), RandomKind(), lv, RandomAffix());
                if (wave == 3 && t >= 3) SpawnAt(Center + new Vector2(0f, 2.5f), EnemyKind.Boss, lv, EliteAffix.None);
                while (AnyAlive()) yield return new WaitForSeconds(0.25f);
                Sound.Play("Clue", 0.8f);
                yield return new WaitForSeconds(0.8f);
            }
            if (t > GameState.Data.riftBest) GameState.Data.riftBest = t;
            Finish(t);
        }

        IEnumerator BossRun(int i)
        {
            wave = 1;
            yield return new WaitForSeconds(1.2f);
            var go = SpawnAt(Center + new Vector2(0f, 2.5f), BossKinds[i], BossLevel(i), EliteAffix.None);
            var e = go.GetComponent<EnemyController>();
            e.defeatFlag = null;
            e.displayName = e.displayName + " (재도전)";
            while (AnyAlive()) yield return new WaitForSeconds(0.25f);
            GameState.SetFlag("rematch:" + i + ":" + GameState.Night);
            Finish(0);
        }

        bool AnyAlive()
        {
            live.RemoveAll(g => g == null);
            foreach (var g in Live) return true;
            return false;
        }

        GameObject SpawnAt(Vector2 p, EnemyKind kind, int level, EliteAffix affix)
        {
            var go = Spawner.Enemy(p, kind);
            var e = go.GetComponent<EnemyController>();
            e.SetLevel(level, RegionName);
            e.detectRange = Mathf.Max(e.detectRange, 30f);
            if (affix != EliteAffix.None) e.MakeElite(affix);
            live.Add(go);
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, p, 16f, new Color(0.7f, 0.45f, 1f), 600, 1.4f);
            return go;
        }

        static Vector2 RandomSpot()
        {
            for (int k = 0; k < 12; k++)
            {
                float a = Random.value * Mathf.PI * 2f;
                var p = Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3.5f, Radius - 1.5f);
                var player = PlayerController.I;
                if (player != null && Vector2.Distance(p, player.transform.position) < 3f) continue;
                if (!EnemyController.TerrainBlocked(p, 0.4f)) return p;
            }
            return Center;
        }

        static EnemyKind RandomKind()
        {
            float r = Random.value;
            return r < 0.4f ? EnemyKind.Rat : r < 0.7f ? EnemyKind.Bat : EnemyKind.Mole;
        }

        static EliteAffix RandomAffix() => (EliteAffix)(1 + Random.Range(0, 4));

        // ------------------------------------------------------------------ 보상

        void Finish(int t)
        {
            Cleared = true;
            Vector2 chest = Center + new Vector2(0f, 1.2f);
            int ilvl = t > 0 ? MonsterLevel(t) + 1 : BossLevel(bossIndex) + 1;
            if (t > 0)
            {
                Loot.DropSilver(chest, Mathf.RoundToInt((40 + 25 * t) * (1f + Stats.Level * 0.1f) * Stats.LootMul));
                Loot.Drop(chest, 10 + 4 * t, 1 + t / 2);
                LootDrop.SpawnGear(chest, Gear.Roll(ilvl, RewardRarity(t)));
                LootDrop.SpawnGear(chest, Random.value < SetChance(t) ? GearSets.Roll(ilvl) : Gear.Roll(ilvl, RewardRarity(t)));
                HUD.Toast("<color=#c58bff>그림자 균열 " + t + "단계를 정화했다!</color> 보상이 쏟아진다. 가운데의 균열로 돌아가자.");
            }
            else
            {
                LootDrop.SpawnGear(chest, GearSets.Roll(ilvl));
                LootDrop.SpawnGear(chest, GearSets.Roll(ilvl));
                HUD.Toast("<color=#c58bff>재도전 성공!</color> 세트 장비 두 개를 얻었다. 가운데의 균열로 돌아가자.");
            }
            RingFx.Spawn(chest, 3f, new Color(0.8f, 0.55f, 1f, 0.95f), 0.6f);
            CameraFollow.Shake(0.3f, 0.2f);
            Sound.Play("Delivered", 1f);
            props.Add(MakePortal(Center + new Vector2(0f, 3.4f), true));
            GameState.Save();
        }

        /// <summary>보랏빛 균열(들어가는 곳과 돌아가는 곳).</summary>
        public static GameObject MakePortal(Vector2 pos, bool exit)
        {
            var go = new GameObject(exit ? "RiftExit" : "RiftPortal");
            go.transform.SetParent(Spawner.Root, false);
            go.transform.position = pos;
            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            core.transform.localScale = new Vector3(1.1f, 1.7f, 1f);
            var sr = core.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(0.12f, 0.04f, 0.22f, 0.95f);
            sr.sortingOrder = 300;
            var ring = new GameObject("Ring");
            ring.transform.SetParent(core.transform, false);
            var rr = ring.AddComponent<SpriteRenderer>();
            rr.sprite = SpriteFactory.Ring;
            rr.color = new Color(0.8f, 0.5f, 1f, 0.9f);
            rr.sortingOrder = 301;
            go.AddComponent<PortalFx>().Init(core.transform, rr);
            Spawner.Glow(pos + new Vector2(0f, 0.9f), 4.5f, new Color(0.7f, 0.4f, 1f, 0.35f), go.transform);
            if (exit) go.AddComponent<RiftExit>().rangeScale = 1.4f;
            else go.AddComponent<RiftPortal>().rangeScale = 1.4f;
            return go;
        }
    }

    /// <summary>균열이 숨 쉬듯 일렁이고 반짝이가 빨려 든다.</summary>
    public class PortalFx : MonoBehaviour
    {
        Transform core;
        SpriteRenderer ring;
        float nextSpark;

        public void Init(Transform c, SpriteRenderer r)
        {
            core = c;
            ring = r;
        }

        void Update()
        {
            float t = Time.time;
            core.localScale = new Vector3(1.1f + 0.06f * Mathf.Sin(t * 3f), 1.7f + 0.08f * Mathf.Sin(t * 2.3f), 1f);
            ring.transform.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * 5f));
            ring.color = new Color(0.8f, 0.5f, 1f, 0.6f + 0.3f * Mathf.Sin(t * 4f));
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.15f;
                Spark.Spawn((Vector2)core.position + Random.insideUnitCircle * 1.1f, new Color(0.8f, 0.55f, 1f), 0.3f);
            }
        }
    }

    /// <summary>마을의 그림자 균열. 말을 걸면 단계·보스를 고르는 화면이 열린다.</summary>
    public class RiftPortal : Interactable
    {
        public override string DisplayName => "그림자 균열";
        public override string Prompt => "그림자 균열 (반복 사냥터 · 보스 재도전)";
        public override void Interact(PlayerController player) => RiftMenu.Open();
    }

    /// <summary>균열 마당의 돌아가는 균열(다 깬 뒤 열린다).</summary>
    public class RiftExit : Interactable
    {
        public override string DisplayName => "돌아가는 균열";
        public override string Prompt => "돌아가는 균열 (마을로)";
        public override void Interact(PlayerController player) => RiftManager.Leave();
    }

    /// <summary>균열 단계와 보스 재도전을 고르는 화면. 클릭으로 들어간다. Esc · E 닫기.</summary>
    public class RiftMenu : MonoBehaviour
    {
        public static RiftMenu I { get; private set; }
        public static bool IsOpen => I != null && I.open;
        public static int LastClosedFrame { get; private set; } = -1;

        bool open;
        int openedFrame;

        void Awake() => I = this;

        void OnDestroy()
        {
            if (I == this) I = null;
        }

        public static void Open()
        {
            if (I == null || RiftManager.Active) return;
            I.open = true;
            I.openedFrame = Time.frameCount;
            Time.timeScale = 0f;
            Sound.Play("Talk", 0.6f);
        }

        public static void Close()
        {
            if (I == null || !I.open) return;
            I.open = false;
            LastClosedFrame = Time.frameCount;
            Time.timeScale = HUD.AnyOverlay || GameMenu.IsOpen ? 0f : 1f;
        }

        void Update()
        {
            if (!open || Time.frameCount == openedFrame) return;
            if (GameInput.PausePressed || GameInput.InteractPressed) Close();
        }

        /// <summary>단계로 들어간다(클릭, 자동 플레이테스트). 잠긴 단계면 false.</summary>
        public static bool ChooseTier(int t)
        {
            if (t < 1 || t > RiftManager.Best + 1) return false;
            Close();
            RiftManager.Enter(t);
            return true;
        }

        public static bool ChooseBoss(int i)
        {
            if (!RiftManager.BossUnlocked(i) || RiftManager.BossDoneTonight(i)) return false;
            Close();
            RiftManager.EnterBoss(i);
            return true;
        }

        void OnGUI()
        {
            if (!open) return;
            GUI.depth = -50;
            Ui.Ensure();
            float s = Ui.S;
            Ui.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.04f, 0.01f, 0.08f, 0.8f));
            float w = Mathf.Min(Screen.width - 40 * s, 1000 * s), h = Mathf.Min(Screen.height - 40 * s, 620 * s);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            Ui.Panel(box);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 12 * s, w - 40 * s, 34 * s),
                "<color=#c58bff>그림자 균열</color>   <size=" + Ui.Px(15) + "><color=#9fb3ff>(눌러서 들어가기 · Esc 닫기)</color></size>", Ui.Title);
            GUI.Label(new Rect(box.x + 20 * s, box.y + 48 * s, w - 40 * s, 26 * s),
                "<color=#9aa0b8>세 파도를 모두 물리치면 보상 상자. 쓰러지면 보상 없이 마을로. 가장 높이 깬 단계 " + RiftManager.Best + "</color>", Ui.Small);

            var e = Event.current;
            // 왼쪽: 단계
            var left = new Rect(box.x + 16 * s, box.y + 84 * s, w * 0.55f, h - 100 * s);
            Ui.Fill(left, new Color(0f, 0f, 0f, 0.25f));
            int top = RiftManager.Best + 1, first = Mathf.Max(1, top - 5);
            float rowH = Mathf.Min(78 * s, (left.height - 8 * s) / 6f);
            for (int t = top, row = 0; t >= first; t--, row++)
            {
                var r = new Rect(left.x + 6 * s, left.y + 4 * s + row * rowH, left.width - 12 * s, rowH - 6 * s);
                bool hot = r.Contains(e.mousePosition);
                bool next = t == top;
                Ui.Fill(r, hot ? new Color(0.8f, 0.5f, 1f, 0.22f) : next ? new Color(0.8f, 0.5f, 1f, 0.12f) : new Color(1f, 1f, 1f, 0.04f));
                int lv = RiftManager.MonsterLevel(t);
                string lvColor = lv - Stats.Level >= 4 ? "#ff6a5a" : lv - Stats.Level >= 2 ? "#ffd34a" : "#e8ecff";
                GUI.Label(new Rect(r.x + 12 * s, r.y + 4 * s, r.width - 24 * s, 30 * s),
                    "<b>" + t + "단계</b>" + (next ? "  <color=#c58bff>새 단계</color>" : "  <color=#7fd68a>깬 단계</color>")
                    + "   <color=" + lvColor + ">몬스터 Lv." + lv + "</color>", Ui.Text);
                GUI.Label(new Rect(r.x + 12 * s, r.y + 34 * s, r.width - 24 * s, 26 * s),
                    "<color=#b8c8ff>보상: <color=" + Gear.RarityColor(RiftManager.RewardRarity(t)) + ">" + Gear.RarityNames[RiftManager.RewardRarity(t)]
                    + "</color> 이상 장비 2 · 세트 장비 " + Mathf.RoundToInt(RiftManager.SetChance(t) * 100) + "% · 은화 · 결정 · 조각</color>", Ui.Small);
                if (hot && e.type == EventType.MouseDown)
                {
                    e.Use();
                    ChooseTier(t);
                    return;
                }
            }

            // 오른쪽: 보스 재도전
            var right = new Rect(left.xMax + 14 * s, left.y, box.xMax - left.xMax - 30 * s, left.height);
            Ui.Fill(right, new Color(0f, 0f, 0f, 0.25f));
            GUI.Label(new Rect(right.x + 12 * s, right.y + 6 * s, right.width - 24 * s, 28 * s), "<b>보스 재도전</b>  <color=#9aa0b8>(밤마다 한 번)</color>", Ui.Text);
            for (int i = 0; i < RiftManager.BossKinds.Length; i++)
            {
                var r = new Rect(right.x + 6 * s, right.y + 40 * s + i * 96 * s, right.width - 12 * s, 88 * s);
                bool unlocked = RiftManager.BossUnlocked(i), done = RiftManager.BossDoneTonight(i);
                bool hot = unlocked && !done && r.Contains(e.mousePosition);
                Ui.Fill(r, hot ? new Color(1f, 0.4f, 0.5f, 0.22f) : new Color(1f, 1f, 1f, unlocked && !done ? 0.06f : 0.02f));
                string name = unlocked ? RiftManager.BossNames[i] : "???";
                GUI.Label(new Rect(r.x + 10 * s, r.y + 4 * s, r.width - 20 * s, 28 * s),
                    (unlocked && !done ? "<b>" : "<color=#6a7088>") + name + (unlocked && !done ? "</b>" : "</color>")
                    + (unlocked ? "  <color=#ff8a8a>Lv." + RiftManager.BossLevel(i) + "</color>" : ""), Ui.Text);
                GUI.Label(new Rect(r.x + 10 * s, r.y + 34 * s, r.width - 20 * s, 50 * s),
                    !unlocked ? "<color=#6a7088>이야기에서 먼저 쓰러뜨리면 열린다.</color>"
                    : done ? "<color=#6a7088>오늘 밤은 이미 물리쳤다. 다음 밤에 다시.</color>"
                    : "<color=#b8c8ff>보상: <color=#5fe08f>세트 장비 2개</color> + 보스 전리품(장비 3)</color>", Ui.Small);
                if (hot && e.type == EventType.MouseDown)
                {
                    e.Use();
                    ChooseBoss(i);
                    return;
                }
            }
        }
    }
}
