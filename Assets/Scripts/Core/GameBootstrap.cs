using System.Collections;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 프로토타입 월드를 코드로 만든다. 빈 씬에서 Play 를 누르면 자동으로 생성된다.
    ///
    /// 지도 (1 유닛 = 1 타일 = 16 픽셀)
    ///   x -16 ~ 17 : 마을 (우체국, 빵집, 바다)
    ///   x  17      : 산울타리, 가운데(y -2~2)에 숲으로 가는 문
    ///   x  17 ~ 42 : 숲길. 가운데 덤불이 막고 있어 북쪽 길 / 남쪽 길로 나뉜다. 밤마다 한쪽 길이 쓰러진 나무로 막힌다.
    ///   x  42 ~ 52 : 숲 끝. 오웬의 오두막, 녹슨 표지판, 낡은 우체통.
    ///
    /// 그림은 Art(코드로 그린 픽셀 아트)에서 가져온다. 실제 아트와 Tilemap 씬으로 옮길 때는
    /// 이 클래스는 씬의 참조를 연결하는 역할만 남기면 된다.
    /// </summary>
    public partial class GameBootstrap : MonoBehaviour
    {
        /// <summary>false 로 바꾸면 빈 씬에서 자동 생성되지 않는다(직접 만든 씬을 쓸 때).</summary>
        public static bool AutoStart = true;

        static GameBootstrap instance;
        GameObject worldRoot;
        bool useAssets;

        static readonly Rect WorldBounds = new Rect(-16f, -11f, 68f, 22f);
        static readonly Vector2 PlayerSpawn = new Vector2(0f, 2.8f);
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
            systems.AddComponent<Sound>();
            systems.AddComponent<Juice>();

            // Assets/Resources/Art/NinjaAdventure 가 있으면 그 그림을, 없으면 코드로 그린 그림을 쓴다.
            useAssets = GameAssets.Available;
            Spawner.PropTint = useAssets ? GameAssets.NightTint : Color.white;
            Spawner.CharacterTint = useAssets ? GameAssets.CharacterTint : Color.white;

            BuildMapAndColliders(hud);
            if (useAssets)
            {
                BuildTileGround();
                BuildAssetScenery();
            }
            else
            {
                BuildPaintedGround();
                BuildCodeHedges();
                BuildBorders();
            }
            BuildVillage();
            BuildForest();
            BuildAmbience();
            var player = BuildPlayer();
            SetupCamera(player.transform);

            if (GameState.Night == 0)
                HUD.Toast("우체국 창구(E)에서 첫 편지를 받자.");
        }

        // ------------------------------------------------------------------ 바닥

        static readonly Rect VillageArea = new Rect(-16f, -11f, 33f, 22f);
        static readonly Rect ForestArea = new Rect(17f, -11f, 35f, 22f);
        static readonly Rect ThicketArea = new Rect(20f, -3f, 22f, 6f);
        static readonly Rect SeaArea = new Rect(-16f, -11f, 24f, 3f);

        void BuildMapAndColliders(HUD hud)
        {
            hud.regions.Add(new HUD.MapRegion { area = VillageArea, color = new Color(0.2f, 0.28f, 0.4f), label = "마을" });
            hud.regions.Add(new HUD.MapRegion { area = ForestArea, color = new Color(0.1f, 0.22f, 0.2f), label = "동쪽 숲" });
            hud.regions.Add(new HUD.MapRegion { area = ThicketArea, color = new Color(0.04f, 0.1f, 0.08f), label = "덤불" });
            hud.regions.Add(new HUD.MapRegion { area = SeaArea, color = new Color(0.1f, 0.18f, 0.38f), label = "밤바다" });

            // 충돌: 바깥 경계, 바다, 울타리, 덤불
            Spawner.Collider("Wall_N", new Vector2(18f, 11.5f), new Vector2(70f, 1f));
            Spawner.Collider("Wall_S", new Vector2(18f, -11.5f), new Vector2(70f, 1f));
            Spawner.Collider("Wall_W", new Vector2(-16.5f, 0f), new Vector2(1f, 24f));
            Spawner.Collider("Wall_E", new Vector2(52.5f, 0f), new Vector2(1f, 24f));
            Spawner.Collider("Sea", new Vector2(-4f, -9.6f), new Vector2(24f, 2.8f));
            Spawner.Collider("Fence_N", new Vector2(17f, 6.5f), new Vector2(1f, 9f));
            Spawner.Collider("Fence_S", new Vector2(17f, -6.5f), new Vector2(1f, 9f));
            Spawner.Collider("Thicket", ThicketArea.center, ThicketArea.size);
        }

        void BuildPaintedGround()
        {
            var village = VillageArea;
            var forest = ForestArea;
            var water = SeaArea;

            var g = new GroundPainter(WorldBounds);
            g.Fill(village, GroundPainter.Grass);
            g.Fill(forest, GroundPainter.ForestFloor);

            // 바닷가: 모래 → 물거품 → 바다
            g.Fill(new Rect(-16f, -8.2f, 24.6f, 1.3f), GroundPainter.Sand, true, 1);
            g.Fill(water, GroundPainter.Water);
            g.Fill(new Rect(-16f, -8.15f, 24f, 0.2f), GroundPainter.Foam, true, 2);

            // 마을 돌길과 광장
            g.Fill(new Rect(-1.2f, -6.5f, 2.4f, 12.3f), GroundPainter.Cobble, true, 3);
            g.Fill(new Rect(-14f, -1.2f, 33f, 2.4f), GroundPainter.Cobble, true, 4);
            g.FillCircle(new Vector2(0f, 3.4f), 2.6f, GroundPainter.Cobble, 5);
            g.Fill(new Rect(-9.6f, 1f, 1.2f, 3.6f), GroundPainter.Cobble, true, 6);
            g.Fill(new Rect(8.4f, 1f, 1.2f, 4f), GroundPainter.Cobble, true, 7);

            // 숲의 흙길
            g.Fill(new Rect(17f, -1.6f, 3.4f, 3.2f), GroundPainter.Dirt, true, 8);
            g.Fill(new Rect(17.8f, -8.4f, 2.4f, 16.8f), GroundPainter.Dirt, true, 9);
            g.Fill(new Rect(18f, 5.9f, 26.4f, 2.3f), GroundPainter.Dirt, true, 10);
            g.Fill(new Rect(18f, -8.2f, 26.4f, 2.3f), GroundPainter.Dirt, true, 11);
            g.Fill(new Rect(42.2f, -8.4f, 2.4f, 16.8f), GroundPainter.Dirt, true, 12);
            g.Fill(new Rect(44f, -8f, 5f, 1.8f), GroundPainter.Dirt, true, 13);
            g.Fill(new Rect(44f, 6.6f, 5.6f, 1.8f), GroundPainter.Dirt, true, 14);
            g.Build(worldRoot.transform, -1000);
        }

        void BuildCodeHedges()
        {
            // 산울타리와 덤불은 1타일 높이 띠로 쌓아서, 캐릭터와 앞뒤가 자연스럽게 겹치게 한다.
            for (int y = 2; y < 11; y++) Hedge("Fence", new Vector2(17f, y), 1, y);
            for (int y = -11; y < -2; y++) Hedge("Fence", new Vector2(17f, y), 1, y + 50);
            for (int y = -3; y < 3; y++) Hedge("Thicket", new Vector2(31f, y), 22, y + 100);
        }

        void Hedge(string name, Vector2 bottomCenter, int widthTiles, int seed)
        {
            int variant = ((seed % 4) + 4) % 4;
            var sprite = Art.Foliage(name + widthTiles + "_" + variant, widthTiles, 1, variant + widthTiles * 7);
            Spawner.Prop(name, bottomCenter, sprite);
        }

        /// <summary>월드 가장자리를 나무와 덤불로 감싸 빈 배경이 보이지 않게 한다.</summary>
        void BuildBorders()
        {
            int i = 0;
            for (float x = -15f; x <= 51.5f; x += 2.3f, i++)
                Spawner.Tree(new Vector2(x + (i % 2) * 0.6f, 11.1f + (i % 3) * 0.25f), x > 17f || i % 3 == 0, i);
            for (float x = 17.5f; x < 52f; x += 1f)
                Hedge("Border", new Vector2(x, -11.9f), 1, (int)x + 200);
            for (float y = -11f; y < 11f; y += 1f)
            {
                Hedge("Border", new Vector2(52.6f, y), 1, (int)y + 300);
                if (y > -8f) Hedge("Border", new Vector2(-16.6f, y), 1, (int)y + 400);
            }
        }

        // ------------------------------------------------------------------ 마을

        void BuildVillage()
        {
            var visuals = WorldVisuals.I;

            if (useAssets) BuildVillageBuildingsFromAssets(visuals);
            else BuildVillageBuildingsCode(visuals);

            // 우체국 등불: 마지막 편지를 배달하면 켜진다.
            visuals.Register(Group("PostLamp_Off", () => Spawner.Lamp(new Vector2(3.2f, 5.3f), false)), "!lamp_lit");
            visuals.Register(Group("PostLamp_On", () =>
            {
                Spawner.Lamp(new Vector2(3.2f, 5.3f), true);
                Spawner.Glow(new Vector2(3.2f, 6.5f), 9f, new Color(1f, 0.85f, 0.5f, 0.25f));
            }), "lamp_lit");

            Spawner.Lamp(new Vector2(-3.6f, 1.6f), true);
            Spawner.Lamp(new Vector2(5.6f, 1.6f), true);
            Spawner.Lamp(new Vector2(-4f, -2.4f), false);
            Spawner.Lamp(new Vector2(15.8f, 2.2f), true);
            Spawner.Lamp(new Vector2(15.8f, -2.8f), true);

            int v = 0;
            foreach (var p in new[]
                     {
                         new Vector2(-14f, 8.5f), new Vector2(14f, 8.8f), new Vector2(-14.5f, 3f), new Vector2(14.5f, -8.6f),
                         new Vector2(-5.5f, 9f), new Vector2(5f, 9.4f), new Vector2(-14f, -6.5f), new Vector2(4.5f, -3.4f)
                     })
            {
                if (useAssets) Spawner.Tree(p, VillageTreeSprite(v), 0.45f);
                else Spawner.Tree(p, false, v);
                v++;
            }

            // 주민: 조건에 따라 서 있는 위치가 바뀐다(같은 id 의 NPC 를 조건별로 하나씩 둔다).
            visuals.Register(Spawner.Npc("mira", new Vector2(-9f, 3.4f), Art.Mira, 1f, "Woman").gameObject, "!reply_softened");
            // 답장의 마지막 줄을 부드럽게 전하면, 미라는 숲 쪽 문 앞에 나와 서 있다.
            visuals.Register(Spawner.Npc("mira", new Vector2(14.5f, 0.6f), Art.Mira, 1f, "Woman").gameObject, "reply_softened");
            visuals.Register(Spawner.Npc("noah", new Vector2(4.5f, -7.2f), Art.Noah, 0.82f, "Child").gameObject, "!lamp_lit");
            visuals.Register(Spawner.Npc("noah", new Vector2(1.8f, 4.3f), Art.Noah, 0.82f, "Child").gameObject, "lamp_lit");
        }

        /// <summary>build 안에서 만든 오브젝트를 하나의 부모로 묶는다(조건부 표시용).</summary>
        GameObject Group(string name, System.Action build)
        {
            var group = new GameObject(name);
            group.transform.SetParent(worldRoot.transform, false);
            var previous = Spawner.Root;
            Spawner.Root = group.transform;
            build();
            Spawner.Root = previous;
            return group;
        }

        void BuildVillageBuildingsCode(WorldVisuals visuals)
        {
            // 우체국
            Spawner.Building("PostOffice", PostOfficePos, new Vector2(7f, 3.5f), Art.Building("postoffice", new Art.BuildingLook
            {
                widthTiles = 7, heightTiles = 5, wallPx = 40, wall = "#8a5a4a", roof = "#47386a", brick = true,
                windows = 2, windowsLit = true, door = true, sign = 1
            }));
            var counter = Spawner.Prop("Counter", new Vector2(0f, 4.75f), Art.Counter);
            counter.AddComponent<PostOfficeCounter>().rangeScale = 1.2f;

            // 빵집: 첫 편지 배달 후 다시 문을 연다.
            var bakeryCenter = new Vector2(-9f, 6f);
            var bakerySize = new Vector2(5f, 3f);
            Spawner.Collider("Bakery_Collider", bakeryCenter, bakerySize);
            var closed = new Art.BuildingLook
            {
                widthTiles = 5, heightTiles = 4, wallPx = 34, wall = "#9a8266", roof = "#7a3a34",
                windows = 2, windowsLit = false, door = true, boarded = true, sign = 2
            };
            var open = closed;
            open.windowsLit = true;
            open.boarded = false;
            visuals.Register(Spawner.Building("Bakery_Closed", bakeryCenter, bakerySize, Art.Building("bakery_closed", closed), false), "!bakery_open");
            visuals.Register(Spawner.Building("Bakery_Open", bakeryCenter, bakerySize, Art.Building("bakery_open", open), false), "bakery_open");
            visuals.Register(Group("Bakery_Stall", () =>
            {
                MakeBreadStall(Spawner.Prop("Stall", new Vector2(-5.4f, 3f), Art.BakeryStall, new Vector2(2.4f, 0.5f), new Vector2(0f, 0.25f)));
                Spawner.Glow(new Vector2(-5.4f, 4f), 5f, new Color(1f, 0.75f, 0.45f, 0.3f));
                Spawner.Glow(new Vector2(-10.4f, 5.6f), 3f, new Color(1f, 0.8f, 0.45f, 0.35f));
                Spawner.Glow(new Vector2(-7.6f, 5.6f), 3f, new Color(1f, 0.8f, 0.45f, 0.35f));
            }), "bakery_open");

            // 그 밖의 집
            Spawner.Building("House_A", new Vector2(9f, 6.5f), new Vector2(4f, 3f), Art.Building("house_a", new Art.BuildingLook
            {
                widthTiles = 4, heightTiles = 4, wallPx = 32, wall = "#5a6478", roof = "#2e4a5a", windows = 1, windowsLit = true, door = true
            }));
            Spawner.Building("House_B", new Vector2(-11f, -4f), new Vector2(4f, 3f), Art.Building("house_b", new Art.BuildingLook
            {
                widthTiles = 4, heightTiles = 4, wallPx = 32, wall = "#6a5a72", roof = "#4a2e4a", windows = 1, windowsLit = false, door = true
            }));
            Spawner.Building("House_C", new Vector2(11f, -5f), new Vector2(4f, 2.6f), Art.Building("house_c", new Art.BuildingLook
            {
                widthTiles = 4, heightTiles = 4, wallPx = 30, wall = "#56706a", roof = "#3a3a5a", windows = 1, windowsLit = true, door = true
            }));
            Spawner.Glow(new Vector2(9f, 6.2f), 3.5f, new Color(1f, 0.8f, 0.45f, 0.25f));
            Spawner.Glow(new Vector2(11f, -4.6f), 3f, new Color(1f, 0.8f, 0.45f, 0.25f));

        }

        // ------------------------------------------------------------------ 숲

        void BuildForest()
        {
            // 쓰러진 나무(밤마다 한쪽만 켜짐). 땅에 누워 있으므로 캐릭터보다 항상 아래에 그린다.
            var northLog = useAssets ? AssetLog("FallenLog_North", new Vector2(30f, 7f))
                : Spawner.Prop("FallenLog_North", new Vector2(30f, 7f), Art.Log, new Vector2(1.2f, 8f), Vector2.zero, -300);
            var southLog = useAssets ? AssetLog("FallenLog_South", new Vector2(30f, -7f))
                : Spawner.Prop("FallenLog_South", new Vector2(30f, -7f), Art.Log, new Vector2(1.2f, 8f), Vector2.zero, -300);

            int v = 0;
            foreach (var p in new[]
                     {
                         new Vector2(23.5f, 9.3f), new Vector2(27f, 4.3f), new Vector2(36f, 9.6f), new Vector2(40f, 4.3f),
                         new Vector2(23.5f, -9.9f), new Vector2(27f, -4.9f), new Vector2(36f, -10f), new Vector2(40f, -4.9f),
                         new Vector2(51f, -9.8f), new Vector2(45.5f, 9.6f), new Vector2(50.5f, 4.5f), new Vector2(45f, -1.8f)
                     })
            {
                if (useAssets) Spawner.Tree(p, ForestTreeSprite(v), 0.4f);
                else Spawner.Tree(p, v % 3 != 0, v);
                v++;
            }

            // 오웬의 오두막
            if (useAssets)
                Spawner.Building("Hut", new Vector2(47.5f, -4.75f), new Vector2(3.6f, 2.5f), GameAssets.Tile("TilesetHouse", 25, 14, 4, 5));
            else
                Spawner.Building("Hut", new Vector2(47.5f, -4.5f), new Vector2(4f, 3f), Art.Building("hut", new Art.BuildingLook
                {
                    widthTiles = 4, heightTiles = 4, wallPx = 30, wall = "#6a5038", roof = "#6e6644", thatch = true,
                    windows = 1, windowsLit = true, door = true
                }));
            Spawner.Glow(new Vector2(47.5f, -5f), 4f, new Color(1f, 0.78f, 0.45f, 0.3f));
            Spawner.Npc("owen", new Vector2(47.5f, -6.9f), Art.Owen, 1f, "Hunter");

            // 낡은 우체통 (편지 3의 받는 곳)
            var mailbox = Spawner.Prop("NPC_old_mailbox", new Vector2(49f, 7.6f), Art.Mailbox, new Vector2(0.8f, 0.5f), new Vector2(0f, 0.25f));
            mailbox.AddComponent<NpcInteractable>().npcId = "old_mailbox";
            WorldVisuals.I.Register(Spawner.Glow(new Vector2(49f, 8.4f), 3.5f, new Color(0.65f, 0.8f, 1f, 0.35f)).gameObject, "!lamp_lit");

            // 상자: 숲 끝 오두막 뒤(반딧불이 병), 북서쪽 숲의 잠긴 상자(은빛 봉인 인장 — 오웬의 열쇠 필요)
            Chest("chest_east", new Vector2(50.8f, -8.6f), "firefly_jar", null);
            Chest("chest_locked", new Vector2(21f, 9.3f), "seal_stamp", "hut_key");

            // 단서
            var sack = Clue("flour_sack", new Vector2(34f, 7f), Art.FlourSack);
            Clue("old_sign", new Vector2(44.6f, 4.4f), useAssets ? GameAssets.Tile("TilesetNature", 5, 8) : Art.Signpost);

            var director = new GameObject("NightDirector").AddComponent<NightDirector>();
            director.transform.SetParent(worldRoot.transform, false);
            director.northLog = northLog;
            director.southLog = southLog;
            director.flourSack = sack.transform;
            director.flourSackNorth = new Vector2(34f, 7f);
            director.flourSackSouth = new Vector2(34f, -7f);
            director.northSpawns = new[] { new Vector2(25f, 7f), new Vector2(33f, 8.5f), new Vector2(38f, 6.5f) };
            director.southSpawns = new[] { new Vector2(25f, -7f), new Vector2(33f, -8.5f), new Vector2(38f, -6.5f) };
            director.eastSpawns = new[] { new Vector2(45f, 0.5f), new Vector2(49.5f, 2f), new Vector2(44f, -8.5f) };
            director.bossSpawn = new Vector2(46f, 6f);
        }

        void Chest(string id, Vector2 pos, string itemId, string requireItem)
        {
            Sprite closed = useAssets ? GameAssets.ChestClosed : null, open = useAssets ? GameAssets.ChestOpen : null;
            if (closed == null || open == null)
            {
                closed = Art.Chest(false);
                open = Art.Chest(true);
            }
            var go = Spawner.Prop("Chest_" + id, pos, closed, new Vector2(0.8f, 0.4f), new Vector2(0f, 0.2f));
            Spawner.Glow(pos + new Vector2(0f, 0.4f), 1.6f, new Color(1f, 0.9f, 0.5f, 0.3f), go.transform);
            var pickup = go.AddComponent<ItemPickup>();
            pickup.pickupId = id;
            pickup.displayName = requireItem != null ? "잠긴 상자" : "낡은 상자";
            pickup.itemId = itemId;
            pickup.requireItem = requireItem;
            pickup.openLine = requireItem != null ? "오웬의 열쇠가 딸깍 돌아간다. 상자를 열었다." : "이끼 낀 상자를 열었다.";
            pickup.lockedLine = "단단히 잠긴 상자다. 자물쇠에 오두막 문양이 새겨져 있다.";
            pickup.emptyLine = "빈 상자다.";
            pickup.visual = go.GetComponent<SpriteRenderer>();
            pickup.closedSprite = closed;
            pickup.openSprite = open;
        }

        /// <summary>빵집 진열대: 밤마다 크루아상 2개를 가져갈 수 있다.</summary>
        static void MakeBreadStall(GameObject stall)
        {
            var pickup = stall.AddComponent<ItemPickup>();
            pickup.pickupId = "bread_stall";
            pickup.displayName = "빵 진열대";
            pickup.itemId = "croissant";
            pickup.count = 2;
            pickup.perNight = true;
            pickup.verb = "확인하기";
            pickup.openLine = "미라: \"오늘 구운 거야. 배달 가는 길에 먹어!\"";
            pickup.emptyLine = "오늘 몫은 이미 받았다. 다음 밤에 또 구워 둔다고 한다.";
        }

        ClueInteractable Clue(string id, Vector2 pos, Sprite sprite)
        {
            var go = Spawner.Prop("Clue_" + id, pos, sprite);
            go.GetComponent<YSort>().isStatic = false; // 밤마다 위치가 바뀔 수 있다
            var sparkle = Spawner.Glow(pos + new Vector2(0f, 0.5f), 1.8f, new Color(1f, 0.95f, 0.6f, 0.5f), go.transform);
            var clue = go.AddComponent<ClueInteractable>();
            clue.clueId = id;
            clue.SetVisual(sparkle.transform);
            return clue;
        }

        // ------------------------------------------------------------------ 분위기

        void BuildAmbience()
        {
            var amb = new GameObject("Ambience").transform;
            amb.SetParent(worldRoot.transform, false);

            // 숲의 반딧불이
            for (int i = 0; i < 26; i++)
            {
                float x = Mathf.Lerp(18f, 51f, PixelCanvas.Hash(i, 1, 500));
                float y = Mathf.Lerp(-10f, 10f, PixelCanvas.Hash(i, 2, 500));
                if (x > 20f && x < 42f && y > -3f && y < 3f) y += y >= 0f ? 3.5f : -3.5f;
                Firefly.Spawn(amb, new Vector2(x, y), new Color(0.8f, 1f, 0.55f), 1.2f, 1f);
            }
            // 마을 나무 근처의 반딧불이
            foreach (var p in new[] { new Vector2(-13f, 7f), new Vector2(13f, 7.5f), new Vector2(-12f, 1f), new Vector2(6f, 8f) })
                Firefly.Spawn(amb, p, new Color(1f, 0.95f, 0.6f), 1f, 1f);
            // 밤바다의 달빛 반짝임
            for (int i = 0; i < 14; i++)
            {
                float x = Mathf.Lerp(-15f, 7f, PixelCanvas.Hash(i, 3, 501));
                float y = Mathf.Lerp(-10.7f, -8.6f, PixelCanvas.Hash(i, 4, 501));
                Firefly.Spawn(amb, new Vector2(x, y), new Color(0.75f, 0.85f, 1f), 0.25f, 0.8f);
            }
            Spawner.Glow(new Vector2(-4f, -9.6f), 16f, new Color(0.45f, 0.55f, 1f, 0.12f));
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
            var playerSheet = useAssets ? GameAssets.Sheet("Inspector") : null;
            var body = playerSheet != null ? Spawner.Character(go, playerSheet) : Spawner.Character(go, Art.Player);
            // 배달부 주변을 은은하게 밝히는 손등불
            var playerLight = Spawner.Glow(PlayerSpawn, 4.5f, new Color(1f, 0.9f, 0.7f, 0.12f), go.transform);

            var slashGo = new GameObject("Slash");
            slashGo.transform.SetParent(worldRoot.transform, false);
            var slash = slashGo.AddComponent<SpriteRenderer>();
            slash.sprite = Art.Slash;
            slash.sortingOrder = 800;

            var player = go.AddComponent<PlayerController>();
            player.Init(body, slash);
            player.lightGlow = playerLight;
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
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.07f);
            cam.transform.position = new Vector3(target.position.x, target.position.y, -10f);

            var follow = cam.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = target;
            follow.SnapToTarget();

            // 밤의 어둠: 화면 가장자리를 어둡게 덮는다(빛 스프라이트는 이보다 위에 그린다).
            var old = cam.transform.Find("NightOverlay");
            if (old != null) Destroy(old.gameObject);
            var overlay = new GameObject("NightOverlay");
            overlay.transform.SetParent(cam.transform, false);
            overlay.transform.localPosition = new Vector3(0f, 0f, 10f);
            overlay.transform.localScale = Vector3.one * 40f;
            var sr = overlay.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Darkness;
            sr.sortingOrder = 1000;
            overlay.AddComponent<NightOverlay>();
        }
    }
}
