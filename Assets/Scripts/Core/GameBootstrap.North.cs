using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 섬의 북쪽 띠 (y 11 ~ 33). 옛 철길이 동서로 가로지른다.
    ///   x -50 ~ -17 : 북쪽 갯바위. 해안 절벽 틈(x -31 ~ -28)으로 들어간다(해안과 함께 우체국 등불 뒤 열림).
    ///                 물웅덩이, 옛 우편 선착장.
    ///   x -16 ~ 17 : 폐역(세 번째 지역). 마을 북동쪽 틈(x 11 ~ 13)으로 처음부터 갈 수 있다.
    ///                 울타리로 둘러친 철길을 건널목 두 곳으로만 건널 수 있고, 밤마다 한쪽 건널목이 잔해로 막힌다.
    ///                 건너편 승강장에 역사·대합실(도라)·옛 우편 열차(편지 10의 받는 곳)가 있다.
    ///   x  17 ~ 52 : 북쪽 숲. 철길이 숲속으로 이어져 무너진 터널에서 끝난다. 옛 신호소 공터,
    ///                 아래쪽 숲 북쪽 길로 내려가는 샛길(x 37.5 ~ 40.5).
    /// </summary>
    public partial class GameBootstrap
    {
        public static readonly Vector2 StationPos = new Vector2(0f, 26f);

        const float FenceSouthY = 19.6f, FenceNorthY = 23.6f, RailY = 21f;
        static readonly Vector2 CrossingWest = new Vector2(-9.8f, 21.6f), CrossingEast = new Vector2(12f, 21.6f);
        const float CrossingHalf = 1.2f;

        void BuildNorthColliders()
        {
            // 옛 섬과 북쪽 띠 사이의 능선: 세 군데만 트여 있다(해안 절벽 틈, 마을 북동쪽, 숲 샛길).
            Spawner.Collider("Ridge_W", new Vector2(-40.5f, 11.5f), new Vector2(19f, 1f));
            Spawner.Collider("Ridge_C", new Vector2(-8.5f, 11.5f), new Vector2(39f, 1f));
            Spawner.Collider("Ridge_E1", new Vector2(25.25f, 11.5f), new Vector2(24.5f, 1f));
            Spawner.Collider("Ridge_E2", new Vector2(46.25f, 11.5f), new Vector2(11.5f, 1f));
            // 북쪽 갯바위와 폐역 사이는 절벽, 폐역과 북쪽 숲 사이는 생울타리(철길만 트여 있다).
            Spawner.Collider("NorthCliff", new Vector2(-16.5f, 22f), new Vector2(1f, 22f));
            Spawner.Collider("NorthHedge_S", new Vector2(17f, (11f + FenceSouthY) * 0.5f), new Vector2(1f, FenceSouthY - 11f));
            Spawner.Collider("NorthHedge_N", new Vector2(17f, (FenceNorthY + 33f) * 0.5f), new Vector2(1f, 33f - FenceNorthY));

            // 철길 울타리(건널목 두 곳은 비운다)
            foreach (var y in new[] { FenceSouthY, FenceNorthY })
            {
                float[] xs = { -16.5f, CrossingWest.x - CrossingHalf, CrossingWest.x + CrossingHalf, CrossingEast.x - CrossingHalf, CrossingEast.x + CrossingHalf, 17f };
                for (int i = 0; i < xs.Length; i += 2)
                    Spawner.Collider("RailFence", new Vector2((xs[i] + xs[i + 1]) * 0.5f, y), new Vector2(xs[i + 1] - xs[i], 0.4f));
            }

            // 북쪽 갯바위의 물웅덩이
            foreach (var r in TidePools)
                Spawner.Collider("TidePool", r.center, r.size - new Vector2(0.6f, 0.6f));
        }

        static readonly Rect[] TidePools = { new Rect(-40f, 17f, 4f, 3f), new Rect(-27f, 26f, 5f, 3f) };

        void BuildNorth()
        {
            var visuals = WorldVisuals.I;
            BuildStation(visuals);
            BuildNorthForest();
            BuildNorthShore();
        }

        // ------------------------------------------------------------------ 폐역

        void BuildStation(WorldVisuals visuals)
        {
            // 철길: 폐역에서 북쪽 숲 터널 앞까지
            for (int x = -16; x < 46; x++)
                Spawner.Prop("Rail", new Vector2(x, RailY), Art.Rail, null, default, -985);

            // 울타리 그림
            for (float x = -16f; x < 17f; x += 1f)
            {
                if (Mathf.Abs(x + 0.5f - CrossingWest.x) < CrossingHalf || Mathf.Abs(x + 0.5f - CrossingEast.x) < CrossingHalf) continue;
                Spawner.Prop("RailFence", new Vector2(x + 0.5f, FenceSouthY - 0.2f), Art.RailFence);
                Spawner.Prop("RailFence", new Vector2(x + 0.5f, FenceNorthY - 0.2f), Art.RailFence);
            }

            // 역사와 승강장
            Spawner.Building("Station", StationPos + new Vector2(0f, 1f), new Vector2(3.8f, 2f),
                useAssets ? GameAssets.Tile(House, 19, 0, 4, 3) : Art.Building("station", new Art.BuildingLook
                {
                    widthTiles = 5, heightTiles = 4, wallPx = 34, wall = "#4a5048", roof = "#2e3a34", windows = 2, windowsLit = false, door = true, sign = 1
                }));
            if (useAssets)
            {
                var plank = GameAssets.Tile(House, 9, 15, 4, 3);
                for (float x = -6f; x <= 6f; x += 4f) Spawner.Prop("Platform", new Vector2(x, FenceNorthY + 0.1f), plank, null, default, -970);
            }
            // 승강장 등불: 우편 열차가 다시 움직이면(teo_sent) 켜진다.
            foreach (var x in new[] { -6.5f, 6.5f })
            {
                visuals.Register(Group("StationLamp_Off", () => Spawner.Lamp(new Vector2(x, 24.4f), false)), "!teo_sent");
                visuals.Register(Group("StationLamp_On", () => Spawner.Lamp(new Vector2(x, 24.4f), true)), "teo_sent");
            }

            // 옛 우편 열차(편지 10의 받는 곳)와 녹슨 화물차
            var mailCar = Spawner.Prop("NPC_mail_train", new Vector2(-3f, RailY), Art.RailCar(true), new Vector2(3.8f, 1.2f), new Vector2(0f, 0.6f));
            var car = mailCar.AddComponent<NpcInteractable>();
            car.npcId = "mail_train";
            car.rangeScale = 1.8f;
            Spawner.Prop("FreightCar", new Vector2(4.5f, RailY), Art.RailCar(false), new Vector2(3.8f, 1.2f), new Vector2(0f, 0.6f));
            visuals.Register(Spawner.Glow(new Vector2(-3f, 22.4f), 4f, new Color(1f, 0.85f, 0.55f, 0.3f)).gameObject, "teo_sent");

            // 밤마다 한쪽 건널목을 막는 잔해
            director.stationWestBlock = CrossingRubble("Rubble_West", CrossingWest.x);
            director.stationEastBlock = CrossingRubble("Rubble_East", CrossingEast.x);
            director.stationSpawns = new[]
            {
                new Vector2(-6f, 15.5f), new Vector2(4f, 17f), new Vector2(-12f, 17.5f), new Vector2(-11f, 27.5f), new Vector2(11f, 26f)
            };

            // 역 입구 표지판
            var sign = Spawner.Prop("StationSign", new Vector2(13.8f, 17.6f), useAssets ? GameAssets.Tile(Nature, 5, 8) : Art.Signpost);
            sign.AddComponent<NpcInteractable>().npcId = "station_sign";

            // 폐역에 자란 마른 나무와 바위
            var dead = useAssets ? GameAssets.Tile(Nature, 4, 0, 2, 2) : Art.PineTree(1);
            foreach (var p in new[] { new Vector2(-13f, 14.5f), new Vector2(-5f, 13.2f), new Vector2(7f, 15f), new Vector2(-12.5f, 29.5f), new Vector2(9.5f, 29f), new Vector2(15f, 26.5f) })
                Spawner.Tree(p, dead, 0.4f);
            var rock = RockSprite(false);
            foreach (var p in new[] { new Vector2(-2f, 16.5f), new Vector2(3.5f, 13f), new Vector2(-14f, 25.5f) })
                Spawner.Prop("Rock", p, rock, new Vector2(1f, 0.5f), new Vector2(0f, 0.25f));

            // 역무실 사물함(크루아상), 승강장 뒤
            var locker = Chest("chest_station", new Vector2(-8.5f, 27.6f), "croissant", null);
            locker.count = 3;
            locker.displayName = "역무실 사물함";
            locker.openLine = "녹슨 사물함 안에 누군가 넣어 둔 빵 봉투가 있다. 아직 먹을 만하다.";
        }

        /// <summary>건널목 하나를 위아래 울타리 사이까지 통째로 막는 잔해 더미.</summary>
        GameObject CrossingRubble(string name, float x)
        {
            var group = new GameObject(name);
            group.transform.SetParent(worldRoot.transform, false);
            group.transform.position = new Vector3(x, FenceSouthY, 0f);
            var col = group.AddComponent<BoxCollider2D>();
            col.size = new Vector2(CrossingHalf * 2f + 0.2f, FenceNorthY - FenceSouthY + 0.8f);
            col.offset = new Vector2(0f, (FenceNorthY - FenceSouthY) * 0.5f);
            var previous = Spawner.Root;
            Spawner.Root = group.transform;
            Spawner.Prop("Rubble", new Vector2(x, FenceSouthY - 0.3f), Art.Rubble);
            Spawner.Prop("Rubble", new Vector2(x + 0.2f, FenceSouthY + 1.8f), Art.Rubble);
            Spawner.Root = previous;
            return group;
        }

        // ------------------------------------------------------------------ 북쪽 숲

        void BuildNorthForest()
        {
            // 무너진 터널(철길의 끝)
            var arch = useAssets ? GameAssets.Tile(House, 29, 20, 3, 3) : RockSprite(true);
            Spawner.Prop("Tunnel", new Vector2(47.6f, 20f), arch, new Vector2(3f, 1.4f), new Vector2(0f, 0.7f));
            WorldVisuals.I.Register(Spawner.Prop("TunnelRubble", new Vector2(47.6f, 20.2f), RockSprite(true), new Vector2(2.4f, 1f), new Vector2(0f, 0.5f)), "!tunnel_open");

            // 옛 신호소 공터
            Spawner.Building("SignalBox", new Vector2(28f, 28.6f), new Vector2(2.6f, 1.6f),
                useAssets ? GameAssets.Tile(House, 0, 7, 3, 3) : Art.Building("signal", new Art.BuildingLook
                {
                    widthTiles = 3, heightTiles = 3, wallPx = 24, wall = "#5a4a3a", roof = "#3a3a2a", windows = 1, windowsLit = false, door = true
                }));
            var box = Chest("chest_signal", new Vector2(30.4f, 27.6f), "croissant", null);
            box.count = 2;
            box.displayName = "신호소 공구함";
            box.openLine = "공구함 안에 기름종이에 싼 빵이 들어 있다. 옛 신호수의 야식이었을까.";

            director.northForestSpawns = new[]
            {
                new Vector2(24f, 21.5f), new Vector2(33f, 21.5f), new Vector2(42f, 15f), new Vector2(30f, 14f), new Vector2(45f, 27f), new Vector2(21f, 28f)
            };

            // 나무: 길과 공터를 피해 흩어 심는다.
            var avoid = new[]
            {
                new Rect(17f, 19.2f, 31f, 4.8f),   // 철길
                new Rect(36.8f, 7f, 4.4f, 14f),    // 샛길
                new Rect(24.5f, 23f, 8f, 8f),      // 신호소 공터
                new Rect(44f, 17f, 8f, 8f),        // 터널 앞
            };
            int v = 0;
            for (float y = 13.5f; y < 32f; y += 2.6f)
                for (float x = 19f; x < 52f; x += 2.4f, v++)
                {
                    var p = new Vector2(x + (PixelCanvas.Hash(v, 1, 900) - 0.5f) * 1.4f, y + (PixelCanvas.Hash(v, 2, 900) - 0.5f) * 1.2f);
                    if (PixelCanvas.Hash(v, 3, 900) > 0.55f || InAny(avoid, p)) continue;
                    if (useAssets) Spawner.Tree(p, ForestTreeSprite(v), 0.4f);
                    else Spawner.Tree(p, true, v);
                }
        }

        static bool InAny(Rect[] rects, Vector2 p)
        {
            foreach (var r in rects)
                if (r.Contains(p)) return true;
            return false;
        }

        // ------------------------------------------------------------------ 북쪽 갯바위

        void BuildNorthShore()
        {
            if (useAssets)
            {
                var plank = GameAssets.Tile(House, 9, 15, 4, 3);
                Spawner.Prop("Pier", new Vector2(-44f, 27.4f), plank, null, default, -970);
                Spawner.Prop("Pier", new Vector2(-48f, 27.4f), plank, null, default, -970);
            }
            var pierBox = Chest("chest_pier", new Vector2(-45.2f, 29.4f), "croissant", null);
            pierBox.count = 2;
            pierBox.displayName = "선착장 우편 상자";
            pierBox.openLine = "옛 우편 선착장의 상자. 바닷바람에 말라 단단해진 빵이 들어 있다.";

            director.coastNorthSpawns = new[]
            {
                new Vector2(-36f, 16f), new Vector2(-24f, 18.5f), new Vector2(-33f, 27f), new Vector2(-21f, 29.5f), new Vector2(-43f, 20.5f)
            };

            var big = RockSprite(true);
            var small = RockSprite(false);
            foreach (var p in new[] { new Vector2(-35f, 14f), new Vector2(-22f, 16.5f), new Vector2(-30f, 30.5f), new Vector2(-20f, 27f), new Vector2(-38f, 24f) })
                Spawner.Prop("Rock", p, big, new Vector2(3f, 0.9f), new Vector2(0f, 0.45f));
            foreach (var p in new[] { new Vector2(-26f, 20f), new Vector2(-42f, 15f), new Vector2(-33f, 21.5f), new Vector2(-24f, 31f) })
                Spawner.Prop("Rock", p, small, new Vector2(1f, 0.5f), new Vector2(0f, 0.25f));

            // 폐역과 갯바위 사이 절벽(그림)
            for (float y = 11.5f; y < 33f; y += 1.6f)
                Spawner.Prop("Cliff", new Vector2(-16.6f, y), big);
        }
    }
}
