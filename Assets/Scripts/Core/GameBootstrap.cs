using System.Collections;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 프로토타입 월드를 코드로 만든다. 빈 씬에서 Play 를 누르면 자동으로 생성된다.
    ///
    /// 지도 (1 유닛 = 1 타일)
    ///   x -16 ~ 17 : 마을 (우체국, 빵집, 바다)
    ///   x  17      : 울타리, 가운데(y -2~2)에 숲으로 가는 문
    ///   x  17 ~ 42 : 숲길. 가운데 덤불이 막고 있어 북쪽 길 / 남쪽 길로 나뉜다. 밤마다 한쪽 길이 쓰러진 나무로 막힌다.
    ///   x  42 ~ 52 : 숲 끝. 오웬의 오두막, 녹슨 표지판, 낡은 우체통.
    ///
    /// 나중에 타일맵과 실제 아트로 씬을 직접 만들게 되면, 이 클래스는 그 씬의 참조를 연결하는 역할만 남기면 된다.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        /// <summary>false 로 바꾸면 빈 씬에서 자동 생성되지 않는다(직접 만든 씬을 쓸 때).</summary>
        public static bool AutoStart = true;

        static GameBootstrap instance;
        GameObject worldRoot;

        static readonly Rect WorldBounds = new Rect(-16f, -11f, 68f, 22f);
        static readonly Vector2 PlayerSpawn = new Vector2(0f, 3.6f);
        static readonly Vector2 PostOfficePos = new Vector2(0f, 7.5f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureBootstrap()
        {
            if (!AutoStart) return;
            if (FindAnyObjectByType<GameBootstrap>() == null)
                new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
        }

        /// <summary>저장을 지우고 월드를 처음부터 다시 만든다(F12).</summary>
        public static void RestartNewGame()
        {
            if (instance == null) return;
            GameState.DeleteSave();
            instance.StartCoroutine(instance.Rebuild());
        }

        void Awake()
        {
            instance = this;
            Build();
        }

        IEnumerator Rebuild()
        {
            Destroy(worldRoot);
            yield return null;
            Build();
        }

        void Build()
        {
            Time.timeScale = 1f;
            GameState.Load();
            GameData.Load();

            worldRoot = new GameObject("World");
            Spawner.Root = worldRoot.transform;
            Physics2D.gravity = Vector2.zero;

            var systems = new GameObject("Systems");
            systems.transform.SetParent(worldRoot.transform, false);
            systems.AddComponent<DialogueSystem>();
            systems.AddComponent<WorldVisuals>();
            var hud = systems.AddComponent<HUD>();
            hud.worldBounds = WorldBounds;
            hud.postOfficePos = PostOfficePos;

            BuildGround(hud);
            BuildVillage();
            BuildForest();
            var player = BuildPlayer();
            SetupCamera(player.transform);

            if (GameState.Night == 0)
                HUD.Toast("우체국 창구(E)에서 첫 편지를 받자.");
        }

        // ------------------------------------------------------------------ 지형

        void BuildGround(HUD hud)
        {
            var village = new Rect(-16f, -11f, 33f, 22f);
            var forest = new Rect(17f, -11f, 35f, 22f);
            var thicket = new Rect(20f, -3f, 22f, 6f);
            var water = new Rect(-16f, -11f, 24f, 3f);

            Ground("VillageGround", village, new Color(0.16f, 0.22f, 0.3f), -1000);
            Ground("ForestGround", forest, new Color(0.08f, 0.15f, 0.15f), -1000);
            Ground("RoadNS", new Rect(-1.2f, -6f, 2.4f, 12f), new Color(0.26f, 0.29f, 0.36f), -990);
            Ground("RoadEW", new Rect(-12f, -1.2f, 30f, 2.4f), new Color(0.26f, 0.29f, 0.36f), -990);

            hud.regions.Add(new HUD.MapRegion { area = village, color = new Color(0.2f, 0.28f, 0.4f), label = "마을" });
            hud.regions.Add(new HUD.MapRegion { area = forest, color = new Color(0.1f, 0.22f, 0.2f), label = "동쪽 숲" });
            hud.regions.Add(new HUD.MapRegion { area = thicket, color = new Color(0.04f, 0.1f, 0.08f), label = "덤불" });
            hud.regions.Add(new HUD.MapRegion { area = water, color = new Color(0.1f, 0.18f, 0.38f), label = "밤바다" });

            // 바깥 경계
            Wall(new Vector2(18f, 11.5f), new Vector2(70f, 1f));
            Wall(new Vector2(18f, -11.5f), new Vector2(70f, 1f));
            Wall(new Vector2(-16.5f, 0f), new Vector2(1f, 24f));
            Wall(new Vector2(52.5f, 0f), new Vector2(1f, 24f));

            // 바다
            Spawner.Block("Sea", water.center, water.size, new Color(0.08f, 0.15f, 0.32f), true, -980);
            Spawner.Block("Shore", new Vector2(-4f, -7.9f), new Vector2(24f, 0.3f), new Color(0.6f, 0.65f, 0.75f, 0.5f), false, -970);

            // 마을과 숲 사이 울타리, 가운데가 문
            Spawner.Block("Fence_N", new Vector2(17f, 6.5f), new Vector2(1f, 9f), new Color(0.12f, 0.2f, 0.18f), true);
            Spawner.Block("Fence_S", new Vector2(17f, -6.5f), new Vector2(1f, 9f), new Color(0.12f, 0.2f, 0.18f), true);
            Lamp(new Vector2(17f, 2.6f), true);
            Lamp(new Vector2(17f, -2.6f), true);

            // 숲 가운데 덤불
            Spawner.Block("Thicket", thicket.center, thicket.size, new Color(0.04f, 0.1f, 0.08f), true);
        }

        void Ground(string name, Rect r, Color c, int order) => Spawner.Block(name, r.center, r.size, c, false, order);

        void Wall(Vector2 center, Vector2 size) => Spawner.Block("Wall", center, size, new Color(0.03f, 0.05f, 0.08f), true, -960);

        void Lamp(Vector2 pos, bool lit)
        {
            Spawner.Block("LampPost", pos, new Vector2(0.2f, 0.9f), new Color(0.2f, 0.2f, 0.25f), true);
            if (lit) Spawner.Circle("LampGlow", pos + Vector2.up * 0.5f, 1.6f, new Color(1f, 0.85f, 0.5f, 0.18f), false, 900);
        }

        // ------------------------------------------------------------------ 마을

        void BuildVillage()
        {
            var visuals = WorldVisuals.I;

            // 우체국
            Spawner.Block("PostOffice", PostOfficePos, new Vector2(7f, 3.5f), new Color(0.55f, 0.35f, 0.3f), true);
            Spawner.Block("PostOffice_Roof", PostOfficePos + new Vector2(0f, 1.9f), new Vector2(7.6f, 0.8f), new Color(0.3f, 0.2f, 0.35f), false);
            Spawner.Block("PostOffice_Door", PostOfficePos + new Vector2(0f, -1.3f), new Vector2(1.2f, 0.9f), new Color(0.25f, 0.15f, 0.12f), false);
            var counter = Spawner.Block("Counter", new Vector2(0f, 5.2f), new Vector2(1.6f, 0.5f), new Color(0.85f, 0.75f, 0.5f), false);
            counter.AddComponent<PostOfficeCounter>().rangeScale = 1.2f;

            // 우체국 등불: 마지막 편지를 배달하면 켜진다.
            Spawner.Block("PostLamp", new Vector2(3f, 5.4f), new Vector2(0.25f, 1f), new Color(0.2f, 0.2f, 0.25f), true);
            visuals.Register(Spawner.Circle("PostLamp_Off", new Vector2(3f, 6f), 0.45f, new Color(0.35f, 0.35f, 0.4f), false, 901), "!lamp_lit");
            var lampOn = Spawner.Circle("PostLamp_On", new Vector2(3f, 6f), 0.5f, new Color(1f, 0.9f, 0.55f), false, 901);
            var lampGlow = Spawner.Circle("PostLamp_Glow", new Vector2(3f, 6f), 5f, new Color(1f, 0.85f, 0.5f, 0.15f), false, 900);
            visuals.Register(lampOn, "lamp_lit");
            visuals.Register(lampGlow, "lamp_lit");

            // 빵집: 첫 편지 배달 후 다시 문을 연다.
            var bakeryPos = new Vector2(-9f, 6f);
            Spawner.Block("Bakery", bakeryPos, new Vector2(5f, 3f), new Color(0.45f, 0.38f, 0.3f), true);
            Spawner.Block("Bakery_Roof", bakeryPos + new Vector2(0f, 1.65f), new Vector2(5.6f, 0.7f), new Color(0.35f, 0.22f, 0.2f), false);
            visuals.Register(Spawner.Block("Bakery_Boards", bakeryPos + new Vector2(0f, -1.1f), new Vector2(1.6f, 0.5f), new Color(0.3f, 0.2f, 0.12f), false), "!bakery_open");
            visuals.Register(Spawner.Block("Bakery_Window", bakeryPos + new Vector2(-1.4f, 0.2f), new Vector2(1f, 0.8f), new Color(1f, 0.82f, 0.45f), false), "bakery_open");
            visuals.Register(Spawner.Block("Bakery_Window2", bakeryPos + new Vector2(1.4f, 0.2f), new Vector2(1f, 0.8f), new Color(1f, 0.82f, 0.45f), false), "bakery_open");
            visuals.Register(Spawner.Block("Bakery_Stall", new Vector2(-5.6f, 3.6f), new Vector2(2.2f, 0.9f), new Color(0.85f, 0.55f, 0.35f), true), "bakery_open");
            visuals.Register(Spawner.Circle("Bakery_Glow", new Vector2(-7f, 4f), 6f, new Color(1f, 0.8f, 0.45f, 0.12f), false, 900), "bakery_open");

            // 그 밖의 집과 가로등
            Spawner.Block("House_A", new Vector2(9f, 6.5f), new Vector2(4f, 3f), new Color(0.35f, 0.38f, 0.48f), true);
            Spawner.Block("House_B", new Vector2(-11f, -4f), new Vector2(4f, 3f), new Color(0.4f, 0.35f, 0.45f), true);
            Spawner.Block("House_C", new Vector2(11f, -5f), new Vector2(3.5f, 2.6f), new Color(0.33f, 0.4f, 0.42f), true);
            Lamp(new Vector2(-4f, 1.8f), true);
            Lamp(new Vector2(6f, 1.8f), true);
            Lamp(new Vector2(-4f, -2.6f), false);
            Spawner.Tree(new Vector2(-14f, 9f));
            Spawner.Tree(new Vector2(14f, 9f));
            Spawner.Tree(new Vector2(-14f, 1.5f));
            Spawner.Tree(new Vector2(14.5f, -8.5f));

            // 주민: 조건에 따라 서 있는 위치가 바뀐다(같은 id 의 NPC 를 조건별로 하나씩 둔다).
            var miraBody = new Color(0.95f, 0.75f, 0.7f);
            var miraHat = new Color(0.95f, 0.95f, 0.9f);
            visuals.Register(Spawner.Npc("mira", new Vector2(-9f, 3.6f), miraBody, miraHat).gameObject, "!reply_softened");
            // 답장의 마지막 줄을 부드럽게 전하면, 미라는 숲 쪽 문 앞에 나와 서 있다.
            visuals.Register(Spawner.Npc("mira", new Vector2(14.5f, 0.6f), miraBody, miraHat).gameObject, "reply_softened");

            var noahBody = new Color(0.6f, 0.8f, 0.95f);
            var noahHat = new Color(0.9f, 0.6f, 0.3f);
            visuals.Register(Spawner.Npc("noah", new Vector2(4.5f, -6.6f), noahBody, noahHat).gameObject, "!lamp_lit");
            visuals.Register(Spawner.Npc("noah", new Vector2(1.8f, 4.4f), noahBody, noahHat).gameObject, "lamp_lit");
        }

        // ------------------------------------------------------------------ 숲

        void BuildForest()
        {
            // 쓰러진 나무(밤마다 한쪽만 켜짐)
            var logColor = new Color(0.36f, 0.25f, 0.18f);
            var northLog = Spawner.Block("FallenLog_North", new Vector2(30f, 7f), new Vector2(1.3f, 8f), logColor, true);
            var southLog = Spawner.Block("FallenLog_South", new Vector2(30f, -7f), new Vector2(1.3f, 8f), logColor, true);

            foreach (var p in new[]
                     {
                         new Vector2(23.5f, 9.5f), new Vector2(27f, 4.6f), new Vector2(36f, 9.8f), new Vector2(40f, 4.5f),
                         new Vector2(23.5f, -9.5f), new Vector2(27f, -4.6f), new Vector2(36f, -9.8f), new Vector2(40f, -4.5f),
                         new Vector2(51f, -10f), new Vector2(43f, 10f)
                     })
                Spawner.Tree(p);

            // 오웬의 오두막
            Spawner.Block("Hut", new Vector2(47.5f, -4.5f), new Vector2(4f, 3f), new Color(0.38f, 0.3f, 0.22f), true);
            Spawner.Block("Hut_Roof", new Vector2(47.5f, -2.8f), new Vector2(4.6f, 0.7f), new Color(0.25f, 0.18f, 0.14f), false);
            Spawner.Block("Hut_Window", new Vector2(46.3f, -4.2f), new Vector2(0.8f, 0.7f), new Color(1f, 0.8f, 0.45f), false);
            Spawner.Npc("owen", new Vector2(47.5f, -6.8f), new Color(0.7f, 0.6f, 0.5f), new Color(0.3f, 0.35f, 0.25f));

            // 낡은 우체통 (편지 3의 받는 곳)
            var mailbox = new GameObject("NPC_old_mailbox");
            mailbox.transform.SetParent(worldRoot.transform, false);
            mailbox.transform.position = new Vector2(49f, 8f);
            Spawner.Character(mailbox, new Color(0.7f, 0.2f, 0.2f), new Color(0.45f, 0.12f, 0.12f), 0.9f);
            mailbox.AddComponent<BoxCollider2D>().size = new Vector2(0.8f, 0.8f);
            mailbox.AddComponent<NpcInteractable>().npcId = "old_mailbox";
            var mailGlow = Spawner.Circle("Mailbox_Glow", new Vector2(49f, 8f), 3f, new Color(0.7f, 0.85f, 1f, 0.12f), false, 900);
            WorldVisuals.I.Register(mailGlow, "!lamp_lit");

            // 단서
            var sack = Clue("flour_sack", new Vector2(34f, 7f), new Color(0.95f, 0.95f, 0.88f));
            Clue("old_sign", new Vector2(44.5f, 4.5f), new Color(0.6f, 0.45f, 0.3f));

            var director = new GameObject("NightDirector").AddComponent<NightDirector>();
            director.transform.SetParent(worldRoot.transform, false);
            director.northLog = northLog;
            director.southLog = southLog;
            director.flourSack = sack.transform;
            director.flourSackNorth = new Vector2(34f, 7f);
            director.flourSackSouth = new Vector2(34f, -7f);
            director.northSpawns = new[] { new Vector2(25f, 7f), new Vector2(33f, 8.5f), new Vector2(38f, 6.5f) };
            director.southSpawns = new[] { new Vector2(25f, -7f), new Vector2(33f, -8.5f), new Vector2(38f, -6.5f) };
            director.eastSpawns = new[] { new Vector2(45f, 0f), new Vector2(49.5f, 2f), new Vector2(44f, -8.5f) };
            director.bossSpawn = new Vector2(46f, 6f);
        }

        ClueInteractable Clue(string id, Vector2 pos, Color color)
        {
            var go = new GameObject("Clue_" + id);
            go.transform.SetParent(worldRoot.transform, false);
            go.transform.position = pos;
            var vis = new GameObject("Visual");
            vis.transform.SetParent(go.transform, false);
            vis.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
            var sr = vis.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            var sparkle = new GameObject("Sparkle");
            sparkle.transform.SetParent(go.transform, false);
            sparkle.transform.localScale = Vector3.one * 1.4f;
            var sp = sparkle.AddComponent<SpriteRenderer>();
            sp.sprite = SpriteFactory.Ring;
            sp.color = new Color(1f, 1f, 0.7f, 0.5f);
            go.AddComponent<YSort>();
            var clue = go.AddComponent<ClueInteractable>();
            clue.clueId = id;
            clue.SetVisual(sparkle.transform);
            return clue;
        }

        // ------------------------------------------------------------------ 플레이어 / 카메라

        PlayerController BuildPlayer()
        {
            var go = new GameObject("Player");
            go.transform.SetParent(worldRoot.transform, false);
            go.transform.position = PlayerSpawn;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.AddComponent<CircleCollider2D>().radius = 0.35f;

            go.AddComponent<Health>();
            var body = Spawner.Character(go, new Color(0.98f, 0.88f, 0.62f), new Color(0.2f, 0.35f, 0.7f), 0.85f);

            // 우편가방
            var bag = new GameObject("Bag");
            bag.transform.SetParent(go.transform, false);
            bag.transform.localPosition = new Vector3(0.32f, -0.12f, 0f);
            bag.transform.localScale = new Vector3(0.3f, 0.32f, 1f);
            var bagSr = bag.AddComponent<SpriteRenderer>();
            bagSr.sprite = SpriteFactory.Square;
            bagSr.color = new Color(0.55f, 0.35f, 0.2f);
            bagSr.sortingOrder = 2;

            var slashGo = new GameObject("Slash");
            slashGo.transform.SetParent(worldRoot.transform, false);
            slashGo.transform.localScale = new Vector3(1.3f, 0.3f, 1f);
            var slash = slashGo.AddComponent<SpriteRenderer>();
            slash.sprite = SpriteFactory.Square;
            slash.color = new Color(1f, 1f, 0.85f, 0.85f);
            slash.sortingOrder = 800;

            var player = go.AddComponent<PlayerController>();
            player.Init(body, slash);
            player.RespawnPoint = PlayerSpawn;
            return player;
        }

        void SetupCamera(Transform target)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.04f, 0.09f);
            cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = target;
            follow.SnapToTarget();
        }
    }
}
