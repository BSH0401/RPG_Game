using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 코드로 픽셀 아트를 그리는 작은 캔버스. (0,0)은 왼쪽 아래.
    /// 실제 아트가 준비되면 이 캔버스로 만든 스프라이트를 .png 스프라이트로 바꾸기만 하면 된다.
    /// </summary>
    public class PixelCanvas
    {
        public const int PixelsPerUnit = 16;

        public readonly int W;
        public readonly int H;
        readonly Color32[] px;

        public PixelCanvas(int width, int height)
        {
            W = width;
            H = height;
            px = new Color32[width * height];
        }

        public bool In(int x, int y) => x >= 0 && y >= 0 && x < W && y < H;

        public void Set(int x, int y, Color32 c)
        {
            if (In(x, y)) px[y * W + x] = c;
        }

        public Color32 Get(int x, int y) => In(x, y) ? px[y * W + x] : new Color32(0, 0, 0, 0);

        public bool Filled(int x, int y) => Get(x, y).a > 0;

        public void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    Set(xx, yy, c);
        }

        public void HLine(int x, int y, int w, Color32 c) => Rect(x, y, w, 1, c);
        public void VLine(int x, int y, int h, Color32 c) => Rect(x, y, 1, h, c);

        public void Ellipse(float cx, float cy, float rx, float ry, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>이미 칠해진 픽셀만 다시 칠한다(명암 넣기용).</summary>
        public void EllipseOver(float cx, float cy, float rx, float ry, Color32 c)
        {
            int x0 = Mathf.FloorToInt(cx - rx), x1 = Mathf.CeilToInt(cx + rx);
            int y0 = Mathf.FloorToInt(cy - ry), y1 = Mathf.CeilToInt(cy + ry);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!Filled(x, y)) continue;
                    float dx = (x + 0.5f - cx) / rx;
                    float dy = (y + 0.5f - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) Set(x, y, c);
                }
        }

        /// <summary>문자열 도안을 찍는다. rows[0]이 맨 위 줄. '.' 과 ' ' 는 투명.</summary>
        public void Stamp(string[] rows, int left, int bottom, System.Func<char, Color32?> palette)
        {
            for (int r = 0; r < rows.Length; r++)
            {
                int y = bottom + rows.Length - 1 - r;
                for (int i = 0; i < rows[r].Length; i++)
                {
                    char ch = rows[r][i];
                    if (ch == '.' || ch == ' ') continue;
                    var c = palette(ch);
                    if (c.HasValue) Set(left + i, y, c.Value);
                }
            }
        }

        /// <summary>칠해진 영역 바깥쪽 한 칸에 외곽선을 두른다.</summary>
        public void Outline(Color32 c)
        {
            var copy = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (copy[y * W + x].a > 0) continue;
                    bool edge = (x > 0 && copy[y * W + x - 1].a > 0) || (x < W - 1 && copy[y * W + x + 1].a > 0)
                             || (y > 0 && copy[(y - 1) * W + x].a > 0) || (y < H - 1 && copy[(y + 1) * W + x].a > 0);
                    if (edge) px[y * W + x] = c;
                }
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }

        /// <summary>pivot 은 0~1 비율. 캐릭터·건물·나무는 발밑(아래 가운데)을 기준점으로 쓴다.</summary>
        public Sprite ToSprite(float pivotX = 0.5f, float pivotY = 0f) =>
            Sprite.Create(ToTexture(), new Rect(0, 0, W, H), new Vector2(pivotX, pivotY), PixelsPerUnit);

        // ---------------------------------------------------------------- 색·난수 도우미

        public static Color32 Hex(string hex)
        {
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            byte r = System.Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = System.Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = System.Convert.ToByte(hex.Substring(4, 2), 16);
            byte a = hex.Length >= 8 ? System.Convert.ToByte(hex.Substring(6, 2), 16) : (byte)255;
            return new Color32(r, g, b, a);
        }

        public static Color32 Shade(Color32 c, float factor) => new Color32(
            (byte)Mathf.Clamp(c.r * factor, 0, 255), (byte)Mathf.Clamp(c.g * factor, 0, 255),
            (byte)Mathf.Clamp(c.b * factor, 0, 255), c.a);

        public static Color32 Mix(Color32 a, Color32 b, float t) => new Color32(
            (byte)Mathf.Lerp(a.r, b.r, t), (byte)Mathf.Lerp(a.g, b.g, t), (byte)Mathf.Lerp(a.b, b.b, t), (byte)Mathf.Lerp(a.a, b.a, t));

        /// <summary>좌표로 정해지는 0~1 난수. 같은 입력이면 항상 같은 값.</summary>
        public static float Hash(int x, int y, int seed = 0)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>격자 칸마다 정한 난수를 부드럽게 이은 값 노이즈(0~1).</summary>
        public static float ValueNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float tx = x - xi, ty = y - yi;
            tx = tx * tx * (3 - 2 * tx);
            ty = ty * ty * (3 - 2 * ty);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }
    }
}
