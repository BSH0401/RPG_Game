using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 아트가 준비되기 전까지 쓰는 임시 도형 스프라이트. 모두 1 유닛 크기이며 색과 스케일로 구분한다.
    /// 나중에 픽셀 아트로 교체할 때는 Spawner/GameBootstrap 에서 이 스프라이트 대신 실제 스프라이트를 넣으면 된다.
    /// </summary>
    public static class SpriteFactory
    {
        static Sprite square, circle, ring;

        public static Sprite Square => square != null ? square : (square = Build(16, (x, y, s) => true));

        public static Sprite Circle => circle != null ? circle : (circle = Build(32, (x, y, s) =>
            Dist(x, y, s) <= s * 0.5f));

        public static Sprite Ring => ring != null ? ring : (ring = Build(64, (x, y, s) =>
        {
            float d = Dist(x, y, s);
            return d <= s * 0.5f && d >= s * 0.5f - 3f;
        }));

        static float Dist(int x, int y, int size)
        {
            float c = (size - 1) * 0.5f;
            return Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) + 0.5f;
        }

        static Sprite Build(int size, System.Func<int, int, int, bool> inside)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = inside(x, y, size) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
