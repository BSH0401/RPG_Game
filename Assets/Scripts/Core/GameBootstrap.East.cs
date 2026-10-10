using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 2부의 지역: 북쪽 숲 끝 무너진 터널 너머 (x 53 ~ 86). 터널이 뚫리면(tunnel_open) 철길을 따라 들어간다.
    ///   y 11 ~ 33 : 별빛 고개. 고개 역(역장 바우)과 작은 마을, 남북으로 흐르는 계곡.
    ///               계곡은 흔들다리 두 개로만 건너고, 밤마다 한쪽 다리가 끊어진다. 건너편 꼭대기에 천문대(별지기 유나).
    ///   y -11 ~ 11 : 옛 채석장. 고개 마을 남쪽 틈(x 63 ~ 65)으로 내려간다. 먹물 웅덩이, 채석장 오두막,
    ///               동쪽 끝에 옛 갱도(우편 창고)와 세 번째 보스.
    /// </summary>
    public partial class GameBootstrap
    {
        const float EastX0 = 52.5f, EastX1 = 86f;
        static readonly Vector2 PassStationPos = new Vector2(61f, 25.6f);
        static readonly Vector2 ObservatoryPos = new Vector2(80.5f, 29.4f);
        static readonly Vector2 MineGatePos = new Vector2(80f, 6f);
        /// <summary>계곡(셀 x 69~70). 흔들다리는 y 15~17, 27~29.</summary>
        const float StreamX = 70f;
        static readonly Rect[] InkPools = { new Rect(66f, -5f, 4f, 3f), new Rect(57f, 3f, 3f, 2f) };

        void BuildEastColliders()
        {
            // 옛 섬과 동쪽 사이의 벽: 철길 자리(y 19.5 ~ 23.5)만 트여 있고, 그 틈은 터널 잔해가 막고 있다.
            Spawner.Collider("Wall_E_S", new Vector2(EastX0, (-11.5f + 19.5f) * 0.5f), new Vector2(1f, 31f));
            Spawner.Collider("Wall_E_N", new Vector2(EastX0, (23.5f + 33.5f) * 0.5f), new Vector2(1f, 10f));
            Spawner.Collider("Wall_E2", new Vector2(EastX1 + 0.5f, 11f), new Vector2(1f, 46f));
            // 고개와 채석장 사이 능선(가운데 틈 x 62.6 ~ 65.4)
            Spawner.Collider("Ridge_East_W", new Vector2((EastX0 + 62.6f) * 0.5f, 11.5f), new Vector2(62.6f - EastX0, 1f));
            Spawner.Collider("Ridge_East_E", new Vector2((65.4f + EastX1) * 0.5f, 11.5f), new Vector2(EastX1 - 65.4f, 1f));
            // 계곡(흔들다리 자리는 비우고, 다리는 NightDirector 가 밤마다 하나를 막는다)
            Spawner.Collider("Stream", new Vector2(StreamX, 13f), new Vector2(2f, 4f));
            Spawner.Collider("Stream", new Vector2(StreamX, 22f), new Vector2(2f, 10f));
            Spawner.Collider("Stream", new Vector2(StreamX, 31f), new Vector2(2f, 4f));
            foreach (var r in InkPools)
                Spawner.Collider("InkPool", r.center, r.size - new Vector2(0.6f, 0.6f));
        }

        void BuildEast()
        {
            var visuals = WorldVisuals.I;
            BuildTunnelCut(visuals);
            BuildPass(visuals);
            BuildQuarry(visuals);
        }

        /// <summary>북쪽 숲 끝의 무너진 터널: 곡괭이로 잔해를 치우면(tunnel_open) 동쪽으로 길이 열린다.</summary>
        void BuildTunnelCut(WorldVisuals visuals)
        {
            visuals.Register(Group("TunnelCut_Rubble", () =>
            {
                Spawner.Collider("TunnelCut", new Vector2(EastX0, 21.5f), new Vector2(1.6f, 4.2f));
                var big = RockSprite(true);
                Spawner.Prop("CutRock", new Vector2(52.2f, 19.4f), big);
                Spawner.Prop("CutRock", new Vector2(52.6f, 21.2f), big);
                Spawner.Prop("CutRock", new Vector2(52.2f, 22.8f), big);
                var talk = Spawner.Prop("NPC_tunnel_rubble", new Vector2(50.6f, 21.4f), Art.Rubble);
                talk.AddComponent<NpcInteractable>().npcId = "tunnel_rubble";
            }), "!tunnel_open");
            visuals.Register(Group("TunnelCut_Open", () =>
            {
                Spawner.Glow(new Vector2(52.5f, 21.5f), 4f, new Color(0.7f, 0.8f, 1f, 0.22f));
                Spawner.Lamp(new Vector2(51f, 23.8f), true);
            }), "tunnel_open");
        }

        // ------------------------------------------------------------------ 별빛 고개

        void BuildPass(WorldVisuals visuals)
        {
            // 철길: 터널을 지나 고개 역의 멈춤 턱까지
            for (int x = 46; x < 66; x++)
                Spawner.Prop("Rail", new Vector2(x, RailY), Art.Rail, null, default, -985);
            Spawner.Prop("BufferStop", new Vector2(66.4f, RailY - 0.4f), Art.Rubble, new Vector2(0.8f, 0.6f), new Vector2(0f, 0.3f));

            // 고개 역과 승강장
            Spawner.Building("PassStation", PassStationPos, new Vector2(3.8f, 2f),
                useAssets ? GameAssets.Tile(House, 19, 0, 4, 3) : Art.Building("pass_station", new Art.BuildingLook
                {
                    widthTiles = 5, heightTiles = 4, wallPx = 34, wall = "#56504a", roof = "#3a3046", windows = 2, windowsLit = true, door = true, sign = 1
                }));
            Spawner.Glow(PassStationPos + new Vector2(0f, -0.4f), 4f, new Color(1f, 0.8f, 0.45f, 0.25f));
            if (useAssets)
            {
                var plank = GameAssets.Tile(House, 9, 15, 4, 3);
                foreach (var x in new[] { 57f, 61f }) Spawner.Prop("Platform", new Vector2(x, 22.6f), plank, null, default, -970);
            }
            Spawner.Lamp(new Vector2(56.2f, 24.2f), true);
            Spawner.Lamp(new Vector2(65.6f, 24.2f), true);

            // 역장 바우: 처음엔 고개 역 승강장, 첫 열차가 다니게 되면(train_runs) 마을 쪽 폐역 승강장으로 내려온다.
            var bauTint = GameAssets.BauTint;
            visuals.Register(Spawner.Npc("bau", new Vector2(58.6f, 23.9f), Art.Owen, 1f, "Hunter", bauTint).gameObject, "!train_runs");
            visuals.Register(Spawner.Npc("bau", StationPos + new Vector2(3.4f, -1.2f), Art.Owen, 1f, "Hunter", bauTint).gameObject, "train_runs");

            // 고개 마을의 집 두 채
            Spawner.Building("PassHouse_A", new Vector2(56f, 29.6f), new Vector2(3.8f, 2f),
                useAssets ? GameAssets.Tile(House, 4, 0, 4, 3) : Art.Building("pass_house_a", new Art.BuildingLook
                {
                    widthTiles = 4, heightTiles = 4, wallPx = 32, wall = "#6a5a50", roof = "#3e3a5a", windows = 1, windowsLit = true, door = true
                }));
            Spawner.Building("PassHouse_B", new Vector2(65.6f, 30f), new Vector2(2.8f, 2f),
                useAssets ? GameAssets.Tile(House, 23, 0, 3, 3) : Art.Building("pass_house_b", new Art.BuildingLook
                {
                    widthTiles = 4, heightTiles = 4, wallPx = 30, wall = "#5a6a62", roof = "#4a3a3a", windows = 1, windowsLit = true, door = true
                }));
            Spawner.Glow(new Vector2(56f, 29.2f), 3.5f, new Color(1f, 0.8f, 0.45f, 0.25f));
            Spawner.Glow(new Vector2(65.6f, 29.6f), 3f, new Color(1f, 0.8f, 0.45f, 0.25f));
            var shed = Chest("chest_pass", new Vector2(59.4f, 30.4f), "croissant", null);
            shed.count = 2;
            shed.displayName = "고개 마을 장작 창고";
            shed.openLine = "장작 사이에 보자기로 싼 빵이 있다. '배달부 몫'이라고 적힌 쪽지와 함께.";

            // 흔들다리 두 개(밤마다 하나가 끊어진다)
            director.passSouthBridge = Bridge("Bridge_South", 16f, visuals);
            director.passNorthBridge = Bridge("Bridge_North", 28f, visuals);

            // 천문대와 망원경
            Spawner.Building("Observatory", ObservatoryPos, new Vector2(2.8f, 1.8f),
                useAssets ? GameAssets.Tile(House, 0, 11, 3, 3) : Art.Building("observatory", new Art.BuildingLook
                {
                    widthTiles = 3, heightTiles = 4, wallPx = 26, wall = "#c8c8d8", roof = "#5a5a7a", windows = 1, windowsLit = true, door = true
                }));
            Spawner.Glow(ObservatoryPos + new Vector2(0f, 1.8f), 6f, new Color(0.7f, 0.8f, 1f, 0.25f));
            Spawner.Glow(ObservatoryPos + new Vector2(0f, 3f), 1.6f, new Color(0.85f, 0.9f, 1f, 0.8f));
            var telescope = Spawner.Prop("Telescope", ObservatoryPos + new Vector2(2.8f, -1.6f), Art.Signpost, new Vector2(0.5f, 0.3f), new Vector2(0f, 0.15f));
            telescope.AddComponent<NpcInteractable>().npcId = "telescope";

            // 별지기 유나: 천문대 앞. 노아의 생일 카드(편지 17)를 들고 있는 밤에는 노아도 여기 와 있다.
            var yunaTint = GameAssets.YunaTint;
            visuals.Register(Spawner.Npc("yuna", ObservatoryPos + new Vector2(-1.4f, -2.6f), Art.Mira, 1f, "Princess", yunaTint).gameObject, "");
            visuals.Register(Spawner.Npc("noah", ObservatoryPos + new Vector2(1.4f, -2.8f), Art.Noah, 0.82f, "Child").gameObject, "carrying:noah_birthday");

            director.passSpawns = new[]
            {
                new Vector2(57f, 15f), new Vector2(66f, 25.5f), new Vector2(74f, 20f), new Vector2(78f, 14f), new Vector2(83f, 22f), new Vector2(73.5f, 30.5f)
            };

            // 나무: 고개 마을과 계곡 건너편에 흩어 심는다.
            var avoid = new[]
            {
                new Rect(53f, 18.5f, 15f, 14f),    // 고개 마을과 철길
                new Rect(62f, 11f, 4f, 9f),        // 채석장으로 내려가는 길
                new Rect(64f, 14f, 14f, 4f),       // 남쪽 다리 길
                new Rect(64f, 26f, 14f, 4f),       // 북쪽 다리 길
                new Rect(68.5f, 11f, 3f, 22f),     // 계곡
                new Rect(74f, 14f, 4f, 16f),       // 건너편 오르막
                new Rect(76f, 23f, 10f, 10f),      // 천문대
            };
            int v = 0;
            for (float y = 12.5f; y < 32.5f; y += 2.6f)
                for (float x = 54f; x < 86f; x += 2.4f, v++)
                {
                    var p = new Vector2(x + (PixelCanvas.Hash(v, 1, 910) - 0.5f) * 1.4f, y + (PixelCanvas.Hash(v, 2, 910) - 0.5f) * 1.2f);
                    if (PixelCanvas.Hash(v, 3, 910) > 0.5f || InAny(avoid, p)) continue;
                    if (useAssets) Spawner.Tree(p, ForestTreeSprite(v), 0.4f);
                    else Spawner.Tree(p, true, v);
                }
        }

        /// <summary>계곡을 건너는 흔들다리. 끊어진 밤에는 다리 판자 대신 잔해가 보이고 충돌로 막힌다.</summary>
        GameObject Bridge(string name, float y, WorldVisuals visuals)
        {
            var group = new GameObject(name);
            group.transform.SetParent(worldRoot.transform, false);
            group.transform.position = new Vector3(StreamX, y, 0f);
            var previous = Spawner.Root;
            Spawner.Root = group.transform;
            // 끊어진 모습: 막는 충돌 + 양쪽 끝의 잔해
            var broken = new GameObject("Broken");
            broken.transform.SetParent(group.transform, false);
            broken.transform.position = new Vector3(StreamX, y, 0f);
            broken.AddComponent<BoxCollider2D>().size = new Vector2(2.4f, 2.2f);
            Spawner.Root = broken.transform;
            Spawner.Prop("BridgeRubble", new Vector2(StreamX - 1.4f, y - 0.8f), Art.Rubble);
            Spawner.Prop("BridgeRubble", new Vector2(StreamX + 1.4f, y - 0.8f), Art.Rubble);
            Spawner.Root = previous;
            // 이어진 다리(밤마다 broken 과 반대로 켜진다)
            var intact = new GameObject(name + "_Intact");
            intact.transform.SetParent(worldRoot.transform, false);
            Spawner.Root = intact.transform;
            if (useAssets) Spawner.Prop("BridgeDeck", new Vector2(StreamX, y - 1.2f), GameAssets.Tile(House, 9, 15, 4, 3), null, default, -970);
            else
                for (float x = StreamX - 1.5f; x <= StreamX + 1.5f; x += 1f)
                    for (float yy = y - 0.5f; yy <= y + 0.5f; yy += 1f)
                        Spawner.Prop("BridgeDeck", new Vector2(x, yy), Art.Rail, null, default, -970);
            Spawner.Root = previous;
            group.AddComponent<BridgeVisual>().intact = intact;
            intact.SetActive(!group.activeSelf); // OnEnable 은 intact 를 넣기 전에 불렸다
            return group;
        }

        // ------------------------------------------------------------------ 옛 채석장

        void BuildQuarry(WorldVisuals visuals)
        {
            // 먹물 웅덩이(그림자가 스며 나오는 곳). 그림자가 잠잠해지면(shadows_calm) 달빛이 비친다.
            foreach (var r in InkPools)
            {
                visuals.Register(Spawner.Glow(r.center, r.width + 2f, new Color(0.5f, 0.3f, 0.9f, 0.3f)).gameObject, "!shadows_calm");
                visuals.Register(Spawner.Glow(r.center, r.width + 2f, new Color(0.75f, 0.85f, 1f, 0.25f)).gameObject, "shadows_calm");
            }

            // 채석장 오두막(석탄 자루가 있는 곳)과 바위
            Spawner.Building("QuarryHut", new Vector2(58f, -6.2f), new Vector2(2.6f, 1.6f),
                useAssets ? GameAssets.Tile(House, 0, 7, 3, 3) : Art.Building("quarry_hut", new Art.BuildingLook
                {
                    widthTiles = 3, heightTiles = 3, wallPx = 24, wall = "#5a4a3a", roof = "#3a3a2a", windows = 1, windowsLit = false, door = true
                }));
            var big = RockSprite(true);
            var small = RockSprite(false);
            foreach (var p in new[] { new Vector2(55f, 7f), new Vector2(61f, 0.5f), new Vector2(71f, 6f), new Vector2(74f, -3f), new Vector2(84f, -8f), new Vector2(68f, -9f), new Vector2(84f, 9f) })
                Spawner.Prop("Rock", p, big, new Vector2(3f, 0.9f), new Vector2(0f, 0.45f));
            foreach (var p in new[] { new Vector2(59f, 8f), new Vector2(66f, 3f), new Vector2(77f, -8.5f), new Vector2(54.5f, -2f), new Vector2(72f, 1.5f), new Vector2(83f, 3f) })
                Spawner.Prop("Rock", p, small, new Vector2(1f, 0.5f), new Vector2(0f, 0.25f));
            // 채석장 동쪽의 절벽(갱도가 있는 바위벽)
            for (float x = 74f; x < 86f; x += 2.6f)
                if (Mathf.Abs(x + 1.3f - MineGatePos.x) > 2.4f)
                    Spawner.Prop("QuarryCliff", new Vector2(x + 1.3f, 7.4f), big, new Vector2(2.8f, 0.9f), new Vector2(0f, 0.45f));

            // 옛 갱도 = 폭풍 밤의 우편을 넣어 둔 우편 창고(편지 16의 받는 곳)
            var arch = useAssets ? GameAssets.Tile(House, 29, 20, 3, 3) : RockSprite(true);
            Spawner.Prop("MineArch", MineGatePos + new Vector2(0f, 0.6f), arch, new Vector2(3f, 0.6f), new Vector2(0f, 0.3f));
            var gate = Spawner.Prop("NPC_mine_gate", MineGatePos, Art.Mailbox, new Vector2(0.6f, 0.3f), new Vector2(0f, 0.15f));
            gate.AddComponent<NpcInteractable>().npcId = "mine_gate";
            visuals.Register(Spawner.Glow(MineGatePos + new Vector2(0f, 1f), 5f, new Color(0.55f, 0.35f, 0.9f, 0.35f)).gameObject, "!shadows_calm");
            visuals.Register(Spawner.Glow(MineGatePos + new Vector2(0f, 1f), 5f, new Color(1f, 0.85f, 0.55f, 0.3f)).gameObject, "shadows_calm");

            director.quarrySpawns = new[]
            {
                new Vector2(56f, 2f), new Vector2(63f, -3f), new Vector2(70f, 3.5f), new Vector2(76f, -6f), new Vector2(82f, -2f), new Vector2(60f, -9.5f)
            };
            director.bosses = new[]
            {
                director.bosses[0], director.bosses[1],
                new BossSpec { letterId = "unsent_letters", defeatFlag = "boss_quarry_defeated", spawn = MineGatePos + new Vector2(-1f, -5f), kind = EnemyKind.Wraith },
            };
        }

        /// <summary>
        /// 고개와 채석장의 바닥 그림(에셋이 없을 때). 에셋이 있으면 GameBootstrap.Assets 의 타일 바닥이 같은 영역을 칠한다.
        /// </summary>
        static void PaintEast(GroundPainter g)
        {
            g.Fill(new Rect(EastX0, 11f, EastX1 - EastX0, 22f), GroundPainter.Grass);
            g.Fill(new Rect(EastX0, -11f, EastX1 - EastX0, 22f), GroundPainter.Cobble);
            g.Fill(new Rect(69f, 11f, 2f, 22f), GroundPainter.Water, true, 40);
            foreach (var r in InkPools) g.Fill(r, GroundPainter.Water, true, 41);
            g.Fill(new Rect(46f, 19.8f, 21f, 3.4f), GroundPainter.Dirt, true, 42);
            g.Fill(new Rect(62.8f, 11f, 2.4f, 18f), GroundPainter.Dirt, true, 43);
            g.Fill(new Rect(65f, 15f, 10f, 2f), GroundPainter.Dirt, true, 44);
            g.Fill(new Rect(65f, 27f, 10f, 2f), GroundPainter.Dirt, true, 45);
            g.Fill(new Rect(75f, 15f, 2f, 14f), GroundPainter.Dirt, true, 46);
        }
    }

    /// <summary>흔들다리: 끊어진 모습(이 오브젝트)이 켜지면 이어진 다리 그림은 꺼진다.</summary>
    public class BridgeVisual : MonoBehaviour
    {
        public GameObject intact;
        void OnEnable() { if (intact != null) intact.SetActive(false); }
        void OnDisable() { if (intact != null) intact.SetActive(true); }
    }
}
