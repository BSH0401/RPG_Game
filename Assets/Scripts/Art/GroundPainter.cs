using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 월드 전체 바닥을 한 장의 픽셀 텍스처로 칠한다(1 유닛 = 16 픽셀).
    /// 나중에 Tilemap 으로 바꾸기 전까지의 바닥 표현.
    /// </summary>
    public class GroundPainter
    {
        public delegate Color32 Shader(int x, int y);

        const int P = PixelCanvas.PixelsPerUnit;
        readonly PixelCanvas canvas;
        readonly Rect bounds;

        public GroundPainter(Rect worldBounds)
        {
            bounds = worldBounds;
            canvas = new PixelCanvas(Mathf.RoundToInt(worldBounds.width * P), Mathf.RoundToInt(worldBounds.height * P));
        }

        int PxX(float wx) => Mathf.RoundToInt((wx - bounds.xMin) * P);
        int PxY(float wy) => Mathf.RoundToInt((wy - bounds.yMin) * P);

        /// <summary>사각형 영역을 칠한다. ragged 면 가장자리 2픽셀을 들쭉날쭉하게 해서 경계가 자연스럽게 보인다.</summary>
        public void Fill(Rect world, Shader shader, bool ragged = false, int seed = 0)
        {
            int x0 = PxX(world.xMin), x1 = PxX(world.xMax), y0 = PxY(world.yMin), y1 = PxY(world.yMax);
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    if (ragged)
                    {
                        int edge = Mathf.Min(Mathf.Min(x - x0, x1 - 1 - x), Mathf.Min(y - y0, y1 - 1 - y));
                        if (edge < 2 && PixelCanvas.Hash(x, y, seed + 91) < 0.55f - edge * 0.2f) continue;
                    }
                    canvas.Set(x, y, shader(x, y));
                }
        }

        public void FillCircle(Vector2 center, float radius, Shader shader, int seed = 0)
        {
            int cx = PxX(center.x), cy = PxY(center.y);
            int r = Mathf.RoundToInt(radius * P);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r) continue;
                    if (d > r - 2 && PixelCanvas.Hash(x, y, seed + 17) < 0.5f) continue;
                    canvas.Set(x, y, shader(x, y));
                }
        }

        public GameObject Build(Transform parent, int sortingOrder)
        {
            var go = new GameObject("Ground");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(bounds.xMin, bounds.yMin, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = canvas.ToSprite(0f, 0f);
            sr.sortingOrder = sortingOrder;
            return go;
        }

        // ---------------------------------------------------------------- 바닥 무늬

        static readonly Color32 GrassA = PixelCanvas.Hex("#24434a"), GrassB = PixelCanvas.Hex("#2b4e52"),
            GrassTuft = PixelCanvas.Hex("#3a6662"), GrassDark = PixelCanvas.Hex("#1d363e");
        static readonly Color32[] Flowers = { PixelCanvas.Hex("#b8a8e0"), PixelCanvas.Hex("#e8e0f0"), PixelCanvas.Hex("#e0a0b8") };

        public static Color32 Grass(int x, int y)
        {
            float n = PixelCanvas.ValueNoise(x / 24f, y / 24f, 1);
            var c = PixelCanvas.Mix(GrassDark, GrassB, n);
            if (PixelCanvas.Hash(x, y, 2) > 0.6f) c = PixelCanvas.Mix(c, GrassA, 0.5f);
            // 풀잎 'v' 모양
            if (PixelCanvas.Hash(x / 6, y / 6, 3) > 0.82f)
            {
                int lx = x % 6, ly = y % 6;
                if ((ly == 1 && (lx == 1 || lx == 3)) || (ly == 0 && lx == 2)) c = GrassTuft;
            }
            if (PixelCanvas.Hash(x, y, 4) > 0.9993f) c = PixelCanvas.Mix(c, Flowers[(x + y) % Flowers.Length], 0.7f);
            return c;
        }

        static readonly Color32 ForestA = PixelCanvas.Hex("#15292a"), ForestB = PixelCanvas.Hex("#1c3533"),
            Leaf1 = PixelCanvas.Hex("#2c3e30"), Leaf2 = PixelCanvas.Hex("#3a3a2c"), Moss = PixelCanvas.Hex("#25483e");

        public static Color32 ForestFloor(int x, int y)
        {
            float n = PixelCanvas.ValueNoise(x / 20f, y / 20f, 5);
            var c = PixelCanvas.Mix(ForestA, ForestB, n);
            float h = PixelCanvas.Hash(x, y, 6);
            if (h > 0.985f) c = Leaf1;
            else if (h > 0.975f) c = Leaf2;
            float moss = PixelCanvas.ValueNoise(x / 9f, y / 9f, 8);
            if (moss > 0.72f) c = PixelCanvas.Mix(c, Moss, Mathf.Clamp01((moss - 0.72f) * 4f) * 0.45f);
            return c;
        }

        static readonly Color32 StoneA = PixelCanvas.Hex("#4b5468"), StoneB = PixelCanvas.Hex("#566079"),
            StoneHi = PixelCanvas.Hex("#6a7590"), Mortar = PixelCanvas.Hex("#323849");

        public static Color32 Cobble(int x, int y)
        {
            int row = y / 6;
            int ox = x + (row % 2) * 4;
            int cx = ox / 8;
            int lx = ox % 8, ly = y % 6;
            if (lx == 0 || ly == 0) return Mortar;
            var c = PixelCanvas.Hash(cx, row, 12) > 0.5f ? StoneA : StoneB;
            if (ly == 4 && lx < 4) c = StoneHi;
            return c;
        }

        static readonly Color32 DirtA = PixelCanvas.Hex("#3d3436"), DirtB = PixelCanvas.Hex("#463c3c"),
            Pebble = PixelCanvas.Hex("#5e5658");

        public static Color32 Dirt(int x, int y)
        {
            var c = PixelCanvas.Mix(DirtA, DirtB, PixelCanvas.ValueNoise(x / 10f, y / 10f, 14));
            if (PixelCanvas.Hash(x, y, 15) > 0.985f) c = Pebble;
            return c;
        }

        static readonly Color32 WaterA = PixelCanvas.Hex("#16264a"), WaterB = PixelCanvas.Hex("#1c3060"),
            Wave = PixelCanvas.Hex("#36508a"), Moonlight = PixelCanvas.Hex("#7a90c8");

        public static Color32 Water(int x, int y)
        {
            var c = PixelCanvas.Mix(WaterA, WaterB, PixelCanvas.ValueNoise(x / 30f, y / 8f, 16));
            int seg = (x + (y / 5) * 11) % 26;
            if (y % 5 == 0 && seg < 6) c = Wave;
            if (y % 5 == 0 && seg < 2 && PixelCanvas.Hash(x / 26, y, 17) > 0.6f) c = Moonlight;
            return c;
        }

        static readonly Color32 SandA = PixelCanvas.Hex("#6a6470"), SandB = PixelCanvas.Hex("#7a7480");

        public static Color32 Sand(int x, int y) =>
            PixelCanvas.Hash(x, y, 18) > 0.8f ? SandB : (PixelCanvas.Hash(x, y, 19) > 0.97f ? PixelCanvas.Hex("#9a94a0") : SandA);

        public static Color32 Foam(int x, int y) =>
            PixelCanvas.Hash(x / 2, y, 20) > 0.45f ? PixelCanvas.Hex("#a8b8d8") : PixelCanvas.Hex("#5a6e9a");
    }
}
