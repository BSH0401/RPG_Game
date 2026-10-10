using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// Ninja Adventure 에셋으로 월드를 꾸미는 부분. (에셋이 없으면 GameBootstrap.cs 의 코드 그림을 쓴다)
    /// 타일 좌표는 각 타일셋 PNG 의 왼쪽 위가 (0,0)이고 한 칸이 16 픽셀이다.
    /// </summary>
    public partial class GameBootstrap
    {
        const string Floor = "TilesetFloor";
        const string Nature = "TilesetNature";
        const string House = "TilesetHouse";
        const string WaterSheet = "TilesetWater";

        const int GroundX0 = -50, GroundX1 = 86, GroundY0 = -11, GroundY1 = 33;
        const int EastMinX = 53; // 이 x 이상은 2부 지역(별빛 고개·옛 채석장)
        const int CoastMaxX = -17; // 이 x 이하는 해안(모래)

        /// <summary>길(흙길) 영역. 셀 단위(x, y, 너비, 높이). 모든 길은 가로·세로 2칸 이상이어야 모서리 타일이 맞는다.</summary>
        static readonly RectInt[] PathCells =
        {
            // 마을
            new RectInt(-16, -1, 33, 2),   // 동서 큰길 (서쪽 끝은 해안 입구)
            new RectInt(-1, -6, 2, 12),    // 남북 길
            new RectInt(-2, 2, 4, 3),      // 우체국 앞 마당
            new RectInt(-10, 1, 2, 3),     // 빵집 앞
            new RectInt(8, 1, 2, 4),       // 집 앞
            // 숲
            new RectInt(17, -1, 3, 2),     // 문
            new RectInt(18, -8, 2, 16),    // 서쪽 세로길
            new RectInt(18, 6, 26, 2),     // 북쪽 길
            new RectInt(18, -8, 26, 2),    // 남쪽 길
            new RectInt(42, -8, 2, 16),    // 동쪽 세로길
            new RectInt(44, -8, 5, 2),     // 오두막 앞
            new RectInt(44, 6, 6, 2),      // 우체통 앞
            // 북쪽 띠
            new RectInt(11, -1, 2, 21),    // 마을 → 폐역
            new RectInt(17, 20, 30, 3),    // 북쪽 숲 철길
            new RectInt(38, 6, 2, 15),     // 숲 북쪽 길 ↔ 철길 샛길
            new RectInt(27, 23, 2, 4),     // 신호소 가는 길
            new RectInt(25, 26, 6, 3),     // 신호소 공터
            // 2부: 터널 너머
            new RectInt(47, 20, 20, 3),    // 터널 → 고개 역 철길
            new RectInt(63, -2, 2, 22),    // 고개 마을 → 채석장
            new RectInt(63, 23, 2, 6),     // 고개 마을 → 북쪽 다리
            new RectInt(65, 15, 4, 2),     // 남쪽 다리 서쪽
            new RectInt(65, 27, 4, 2),     // 북쪽 다리 서쪽
            new RectInt(71, 15, 6, 2),     // 남쪽 다리 동쪽
            new RectInt(71, 27, 8, 2),     // 북쪽 다리 동쪽 → 천문대
            new RectInt(75, 15, 2, 14),    // 계곡 건너편 오르막
            new RectInt(65, -2, 16, 2),    // 채석장 큰길
            new RectInt(79, 0, 2, 5),      // 갱도 앞
            new RectInt(56, -5, 7, 2),     // 채석장 오두막 앞
        };

        /// <summary>폐역 마당의 자갈 바닥(셀 단위).</summary>
        static readonly RectInt StationGravel = new RectInt(-15, 12, 31, 19);
        /// <summary>옛 채석장 전체와 고개 역 앞마당의 자갈 바닥(셀 단위).</summary>
        static readonly RectInt[] EastGravel = { new RectInt(53, -11, 33, 22), new RectInt(55, 22, 12, 4) };
        /// <summary>별빛 고개의 계곡과 채석장의 먹물 웅덩이(셀 단위). 충돌은 GameBootstrap.East.</summary>
        static readonly RectInt[] EastWaterCells = { new RectInt(69, 11, 2, 22), new RectInt(66, -5, 4, 3), new RectInt(57, 3, 3, 2) };
        /// <summary>북쪽 갯바위의 물웅덩이(셀 단위). 충돌은 GameBootstrap.North 의 TidePools.</summary>
        static readonly RectInt[] TidePoolCells = { new RectInt(-40, 17, 4, 3), new RectInt(-27, 26, 5, 3) };

        static readonly RectInt SeaCells = new RectInt(-16, -11, 24, 3);
        static readonly RectInt CoastSeaSouth = new RectInt(-50, -11, 33, 5);
        static readonly RectInt CoastSeaWest = new RectInt(-50, -11, 4, 44);

        // ------------------------------------------------------------------ 바닥

        void BuildTileGround()
        {
            var root = new GameObject("Ground").transform;
            root.SetParent(worldRoot.transform, false);

            int w = GroundX1 - GroundX0, h = GroundY1 - GroundY0;
            var path = new bool[w, h];
            var sea = new bool[w, h];
            var gravel = new bool[w, h];
            foreach (var r in PathCells) Mark(path, r);
            Mark(gravel, StationGravel);
            foreach (var r in EastGravel) Mark(gravel, r);
            foreach (var r in EastWaterCells) Mark(sea, r);
            foreach (var r in TidePoolCells) Mark(sea, r);
            Mark(sea, SeaCells);
            Mark(sea, CoastSeaSouth);
            Mark(sea, CoastSeaWest);

            System.Func<int, int, bool> Path = (x, y) =>
                x >= GroundX0 && x < GroundX1 && y >= GroundY0 && y < GroundY1 && path[x - GroundX0, y - GroundY0];
            System.Func<int, int, bool> Gravel = (x, y) =>
                x >= GroundX0 && x < GroundX1 && y >= GroundY0 && y < GroundY1 && gravel[x - GroundX0, y - GroundY0];
            // 바다는 왼쪽·아래쪽 화면 밖으로 이어진다고 본다.
            System.Func<int, int, bool> Sea = (x, y) =>
                x < GroundX0 || y < GroundY0 || (x < GroundX1 && y < GroundY1 && sea[x - GroundX0, y - GroundY0]);

            for (int y = GroundY0; y < GroundY1; y++)
                for (int x = GroundX0; x < GroundX1; x++)
                {
                    bool forest = x >= 17 && x < EastMinX;
                    bool coast = x <= CoastMaxX;
                    if (Sea(x, y))
                    {
                        // 해안 바다는 모래 테두리, 마을 바다는 풀 테두리
                        Spawner.GroundTile(root, x, y, BlobTile(WaterSheet, 0, coast ? 0 : 6, Sea, x, y, false), -1000, GameAssets.SeaTint);
                        continue;
                    }

                    if (coast)
                    {
                        Spawner.GroundTile(root, x, y, SandTile(x, y), -1000);
                        if (PixelCanvas.Hash(x, y, 31) > 0.95f) Detail(root, x, y, 0);
                        continue;
                    }
                    Spawner.GroundTile(root, x, y, GrassTile(x, y, forest), -1000);
                    if (Gravel(x, y) && !Path(x, y))
                        Spawner.GroundTile(root, x, y, BlobTile(Floor, 11, 14, Gravel, x, y, true), -995);
                    if (Path(x, y))
                        Spawner.GroundTile(root, x, y, BlobTile(Floor, forest ? 11 : 0, 7, Path, x, y, true), -990);
                    else if (PixelCanvas.Hash(x, y, 31) > 0.94f)
                        Detail(root, x, y, 2);
                }
        }

        static void Mark(bool[,] grid, RectInt r)
        {
            for (int y = r.yMin; y < r.yMax; y++)
                for (int x = r.xMin; x < r.xMax; x++)
                {
                    int gx = x - GroundX0, gy = y - GroundY0;
                    if (gx >= 0 && gy >= 0 && gx < grid.GetLength(0) && gy < grid.GetLength(1)) grid[gx, gy] = true;
                }
        }

        static Sprite GrassTile(int x, int y, bool forest)
        {
            float hsh = PixelCanvas.Hash(x, y, 30);
            int ox = forest ? 11 : 0;
            if (hsh < 0.72f) return GameAssets.GroundTile(Floor, ox, 12);
            int variant = 1 + (int)(PixelCanvas.Hash(x, y, 32) * 3.99f); // 1~4: 풀 무늬
            if (forest) variant = Mathf.Min(variant, 3);
            return GameAssets.GroundTile(Floor, ox + variant, 12);
        }

        /// <summary>
        /// 3x3 "덩어리" 자동 타일. (ox, oy) 가 왼쪽 위 모서리 조각.
        /// 바깥 모서리·변·가운데, 그리고 안쪽 모서리(ox+5, oy+1 부근의 2x2)를 이웃에 따라 고른다.
        /// </summary>
        static Sprite BlobTile(string sheet, int ox, int oy, System.Func<int, int, bool> same, int x, int y, bool innerCorners)
        {
            bool n = same(x, y + 1), s = same(x, y - 1), e = same(x + 1, y), w = same(x - 1, y);
            int tx = ox + 1, ty = oy + 1;
            if (!n) ty = oy;
            else if (!s) ty = oy + 2;
            if (!w) tx = ox;
            else if (!e) tx = ox + 2;

            if (n && s && e && w && innerCorners)
            {
                if (!same(x + 1, y - 1)) return GameAssets.GroundTile(sheet, ox + 5, oy + 1);
                if (!same(x - 1, y - 1)) return GameAssets.GroundTile(sheet, ox + 6, oy + 1);
                if (!same(x + 1, y + 1)) return GameAssets.GroundTile(sheet, ox + 5, oy + 2);
                if (!same(x - 1, y + 1)) return GameAssets.GroundTile(sheet, ox + 6, oy + 2);
            }
            return GameAssets.GroundTile(sheet, tx, ty);
        }

        /// <summary>해안 바닥: 밝은 모래(물가 타일의 모래색과 같다).</summary>
        static Sprite SandTile(int x, int y)
        {
            float h = PixelCanvas.Hash(x, y, 34);
            if (h < 0.8f) return GameAssets.GroundTile(Floor, 1, 1);
            return GameAssets.GroundTile(Floor, h < 0.9f ? 8 : 5, 1);
        }

        void Detail(Transform root, int x, int y, int row)
        {
            // 장식: row 2 = 풀포기·꽃, row 0 = 불가사리·조개·자갈 (TilesetFloorDetail)
            int i = (int)(PixelCanvas.Hash(x, y, 33) * (row == 0 ? 4.99f : 7.99f));
            var sprite = GameAssets.Region("Tilesets/TilesetFloorDetail", i * 16, row * 16, 16, 16, 0.5f, 0f);
            if (sprite == null) return;
            var go = new GameObject("Detail");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x + 0.5f, y + 0.1f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Spawner.PropTint;
            sr.sortingOrder = -980;
        }

        // ------------------------------------------------------------------ 울타리 · 덤불 · 가장자리

        void BuildAssetScenery()
        {
            // 마을과 숲 사이: 키 작은 나무를 촘촘히 세운 생울타리
            var hedge = GameAssets.Tile(Nature, 0, 0, 2, 2);
            var conifer = GameAssets.Tile(Nature, 2, 0, 2, 2);
            for (float y = 2f; y < 11f; y += 1.3f) Spawner.Prop("Hedge", new Vector2(17f, y), Mathf.Repeat(y, 2.6f) < 1.3f ? hedge : conifer);
            for (float y = -11.4f; y < -2.4f; y += 1.3f) Spawner.Prop("Hedge", new Vector2(17f, y), Mathf.Repeat(y, 2.6f) < 1.3f ? hedge : conifer);

            // 숲 가운데 덤불: 소나무 무리를 겹쳐 세운다
            var cluster = GameAssets.Tile(Nature, 0, 2, 3, 3);
            int i = 0;
            for (float y = -3.2f; y < 2.5f; y += 1.7f)
                for (float x = 21.2f; x < 41.5f; x += 2.5f, i++)
                    Spawner.Prop("Thicket", new Vector2(x + (i % 2) * 0.6f, y), cluster);

            // 옛 섬과 북쪽 띠 사이: 침엽수 줄(능선의 세 틈은 비운다). 맨 위 가장자리도 침엽수 줄.
            i = 0;
            for (float x = -45.5f; x <= 86f; x += 1.7f, i++)
            {
                if (!InRidgeGap(x)) Spawner.Prop("Border", new Vector2(x, 10.6f + (i % 3) * 0.3f), i % 2 == 0 ? conifer : hedge);
                if (x > -17f) Spawner.Prop("Border", new Vector2(x, 32.6f + (i % 3) * 0.3f), i % 2 == 0 ? conifer : hedge);
            }
            // 폐역과 북쪽 숲 사이 생울타리(철길 자리는 비운다)
            for (float y = 11.4f; y < 33f; y += 1.3f)
                if (y < FenceSouthY - 0.4f || y > FenceNorthY + 0.2f)
                    Spawner.Prop("Hedge", new Vector2(17f, y), Mathf.Repeat(y, 2.6f) < 1.3f ? hedge : conifer);
            // 숲 아래쪽, 양옆 가장자리
            var bush = GameAssets.Tile(Nature, 0, 10);
            var bush2 = GameAssets.Tile(Nature, 1, 10);
            for (int x = 18; x < 86; x++) Spawner.Prop("Border", new Vector2(x + 0.5f, -11.4f), x % 2 == 0 ? bush : bush2);
            for (float y = -11f; y < 33f; y += 1.5f)
            {
                // 동쪽 터널 자리(y 19 ~ 23.5)는 비운다. 잔해가 막고 있다가 치우면 길이 된다.
                if (y < 18.6f || y > 23.6f) Spawner.Prop("Border", new Vector2(53f, y), conifer);
                Spawner.Prop("Border", new Vector2(87f, y), conifer);
                // 마을과 해안 사이 나무 울타리(가운데 길은 비운다)
                if (y > -8f && (y < -2.6f || y > 1.6f)) Spawner.Prop("Border", new Vector2(-17f, y), conifer);
            }
        }

        /// <summary>능선(y 11)에서 걸어 지나갈 수 있는 틈: 해안 절벽 틈, 마을 북동쪽, 숲 샛길.</summary>
        static bool InRidgeGap(float x) => (x > -32f && x < -27.5f) || (x > 10f && x < 14f) || (x > 36.6f && x < 41.4f) || (x > 62f && x < 66f);

        static Sprite VillageTreeSprite(int v)
        {
            switch (v % 3)
            {
                case 0: return GameAssets.Tile(Nature, 3, 18, 3, 3);
                case 1: return GameAssets.Tile(Nature, 6, 8, 2, 2);
                default: return GameAssets.Tile(Nature, 0, 0, 2, 2);
            }
        }

        static Sprite ForestTreeSprite(int v)
        {
            switch (v % 3)
            {
                case 0: return GameAssets.Tile(Nature, 6, 8, 2, 2);
                default: return GameAssets.Tile(Nature, 2, 0, 2, 2);
            }
        }

        /// <summary>쓰러진 통나무: 세로 통나무 조각 4개를 이어 붙여 길을 막는다.</summary>
        GameObject AssetLog(string name, Vector2 center)
        {
            var group = new GameObject(name);
            group.transform.SetParent(worldRoot.transform, false);
            group.transform.position = center;
            group.AddComponent<BoxCollider2D>().size = new Vector2(1.1f, 8f);
            var log = GameAssets.Tile(Nature, 13, 14, 1, 2);
            for (int k = 0; k < 4; k++)
            {
                var piece = new GameObject("Log");
                piece.transform.SetParent(group.transform, false);
                piece.transform.localPosition = new Vector3(0f, -4f + k * 2f, 0f);
                var sr = piece.AddComponent<SpriteRenderer>();
                sr.sprite = log;
                sr.color = Spawner.PropTint;
                sr.sortingOrder = -300;
            }
            return group;
        }

        // ------------------------------------------------------------------ 마을 건물

        void BuildVillageBuildingsFromAssets(WorldVisuals visuals)
        {
            // 우체국: 큰 초가 건물. 아래쪽 2.6칸만 막는다.
            Spawner.Building("PostOffice", new Vector2(0f, 5.75f + 1.3f), new Vector2(3.8f, 2.6f), GameAssets.Tile(House, 25, 7, 4, 7));
            var counter = Spawner.Prop("Counter", new Vector2(0f, 3.7f), GameAssets.Tile(House, 16, 16, 3, 2));
            counter.AddComponent<PostOfficeCounter>().rangeScale = 1.2f;

            // 빵집: 빵 간판이 달린 가게. 닫혀 있을 때는 어둡고 문에 판자.
            var bakeryBottom = new Vector2(-9f, 4.5f);
            Spawner.Collider("Bakery_Collider", bakeryBottom + new Vector2(0f, 1f), new Vector2(2.8f, 2f));
            var shop = GameAssets.Tile(House, 16, 0, 3, 3);
            visuals.Register(Group("Bakery_Closed", () =>
            {
                var b = Spawner.Prop("Bakery", bakeryBottom, shop);
                b.GetComponent<SpriteRenderer>().color = Spawner.PropTint * 0.7f + new Color(0f, 0f, 0f, 0.3f);
                var boards = Spawner.Prop("Boards", bakeryBottom + new Vector2(0f, 0.02f), GameAssets.Tile(House, 2, 3));
                boards.GetComponent<YSort>().offset = 1;
            }), "!bakery_open");
            visuals.Register(Group("Bakery_Open", () =>
            {
                Spawner.Prop("Bakery", bakeryBottom, shop);
                var stall = Spawner.Prop("Stall", new Vector2(-5.4f, 3f), GameAssets.Tile(House, 19, 16, 3, 2), new Vector2(2.6f, 0.5f), new Vector2(0f, 0.25f));
                MakeBreadStall(stall);
                AddOnTop(stall, GameAssets.Tile(House, 20, 14), new Vector2(-0.6f, 0.75f));
                AddOnTop(stall, GameAssets.Tile(House, 20, 14), new Vector2(0.6f, 0.75f));
                Spawner.Glow(new Vector2(-5.4f, 4f), 5f, new Color(1f, 0.75f, 0.45f, 0.3f));
                Spawner.Glow(new Vector2(-9f, 5.4f), 4f, new Color(1f, 0.8f, 0.45f, 0.35f));
            }), "bakery_open");

            // 그 밖의 집
            Spawner.Building("House_A", new Vector2(9f, 6f), new Vector2(3.8f, 2f), GameAssets.Tile(House, 8, 0, 4, 3));
            Spawner.Building("House_B", new Vector2(-11f, -4.5f), new Vector2(3.8f, 2f), GameAssets.Tile(House, 4, 0, 4, 3));
            Spawner.Building("House_C", new Vector2(11f, -5.3f), new Vector2(2.8f, 2f), GameAssets.Tile(House, 23, 0, 3, 3));
            Spawner.Glow(new Vector2(9f, 5.6f), 3.5f, new Color(1f, 0.8f, 0.45f, 0.25f));
            Spawner.Glow(new Vector2(11f, -5.6f), 3f, new Color(1f, 0.8f, 0.45f, 0.25f));
        }

        /// <summary>소품 위에 얹는 작은 그림(진열대 위의 빵 바구니 등). 부모보다 항상 앞에 그린다.</summary>
        static void AddOnTop(GameObject parent, Sprite sprite, Vector2 local)
        {
            var go = new GameObject("OnTop");
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = local;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = Spawner.PropTint;
            sr.sortingOrder = 1; // YSort 가 부모 순서 + 1 로 맞춘다
        }
    }
}
