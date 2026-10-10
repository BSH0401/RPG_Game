using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 코드로 그린 픽셀 아트 모음(밤 팔레트). 한 번 만든 스프라이트는 캐시해서 재사용한다.
    /// 실제 아트로 교체할 때는 각 함수가 Resources 의 스프라이트를 돌려주게 바꾸면 된다.
    /// </summary>
    public static class Art
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (!cache.TryGetValue(key, out var s) || s == null)
            {
                s = make();
                cache[key] = s;
            }
            return s;
        }

        static Color32 H(string hex) => PixelCanvas.Hex(hex);

        static readonly Color32 Ink = PixelCanvas.Hex("#12121e");

        // ================================================================ 캐릭터

        // 16x20 사람 도안. o 외곽선, h 모자, H 모자챙, s 피부, e 눈, b 옷, B 옷 그림자, w 옷깃, p 바지
        static readonly string[] Person =
        {
            "................",
            ".....oooooo.....",
            "....ohhhhhho....",
            "...ohhhhhhhho...",
            "..oHHHHHHHHHHo..",
            "...osssssssso...",
            "...osesssseso...",
            "...osssssssso...",
            "....osssssso....",
            "...obbbbbbbbo...",
            "..obbbbwwbbbbo..",
            "..obbbbbbbbbbo..",
            ".osbbbbbbbbbbso.",
            ".osBBBBBBBBBBso.",
            "..oBBBBBBBBBBo..",
            "...oppppppppo...",
            "...opppoopppo...",
            "...oppo..oppo...",
            "...ooo....ooo...",
            "................",
        };

        public struct PersonLook
        {
            public string hat, brim, skin, cloth, collar, pants;
            public bool bag, beard, apron;
        }

        static Sprite PersonSprite(string key, PersonLook look) => Cached(key, () =>
        {
            var c = new PixelCanvas(16, 20);
            Color32 hat = H(look.hat), brim = H(look.brim), skin = H(look.skin), cloth = H(look.cloth);
            Color32 clothDark = PixelCanvas.Shade(cloth, 0.75f), collar = H(look.collar), pants = H(look.pants);
            c.Stamp(Person, 0, 0, ch =>
            {
                switch (ch)
                {
                    case 'o': return Ink;
                    case 'h': return hat;
                    case 'H': return brim;
                    case 's': return skin;
                    case 'e': return H("#1d1b2e");
                    case 'b': return cloth;
                    case 'B': return clothDark;
                    case 'w': return collar;
                    case 'p': return pants;
                    default: return null;
                }
            });
            // 모자 하이라이트
            c.HLine(6, 17, 3, PixelCanvas.Shade(hat, 1.25f));
            if (look.apron)
            {
                c.Rect(5, 6, 6, 5, H("#efe8da"));
                c.HLine(5, 6, 6, H("#cfc6b4"));
            }
            if (look.beard)
            {
                var beard = H("#8c7c6a");
                c.HLine(4, 12, 8, beard);
                c.HLine(5, 11, 6, beard);
                c.HLine(6, 10, 4, beard);
            }
            if (look.bag)
            {
                // 어깨끈 + 오른쪽 허리의 우편가방
                var strap = H("#6a4428");
                for (int i = 0; i < 5; i++) c.Set(5 + i, 10 - i + 0, strap);
                c.Rect(11, 5, 4, 4, H("#9a6436"));
                c.HLine(11, 8, 4, H("#b97c46"));
                c.Set(13, 6, H("#e8d38a"));
                c.Rect(11, 4, 4, 1, Ink);
                c.VLine(15, 5, 4, Ink);
            }
            return c.ToSprite();
        });

        public static Sprite Player => PersonSprite("player", new PersonLook
        {
            hat = "#34589c", brim = "#243d70", skin = "#f2c9a0", cloth = "#4067b0", collar = "#e9e4d4", pants = "#363250", bag = true
        });

        public static Sprite Mira => PersonSprite("mira", new PersonLook
        {
            hat = "#f1ece0", brim = "#d2c9b6", skin = "#f0c4a2", cloth = "#c4675a", collar = "#f1ece0", pants = "#5a4840", apron = true
        });

        public static Sprite Noah => PersonSprite("noah", new PersonLook
        {
            hat = "#e08a3a", brim = "#b4672a", skin = "#f2c9a0", cloth = "#6aa6d8", collar = "#d8ecf8", pants = "#3d4a66"
        });

        public static Sprite Owen => PersonSprite("owen", new PersonLook
        {
            hat = "#3f5c3b", brim = "#2b412a", skin = "#e0b48e", cloth = "#7b6249", collar = "#a89070", pants = "#3a3430", beard = true
        });

        public static Sprite Dora => PersonSprite("dora", new PersonLook
        {
            hat = "#c9c4cf", brim = "#a39eab", skin = "#e8bea0", cloth = "#5b4a7a", collar = "#d8cfe0", pants = "#3a3048"
        });

        public static Sprite Teo => PersonSprite("teo", new PersonLook
        {
            hat = "#d8b860", brim = "#a88a40", skin = "#c99a74", cloth = "#3d6a8a", collar = "#e6eef2", pants = "#4a4036", beard = true
        });

        public static Sprite ShadowRat => Cached("rat", () =>
        {
            var c = new PixelCanvas(22, 18);
            var body = H("#3b2b58");
            c.Ellipse(4, 12.5f, 2.6f, 3.2f, body);
            c.Ellipse(18, 12.5f, 2.6f, 3.2f, body);
            c.Ellipse(4, 12.5f, 1.2f, 1.8f, H("#7a3c6c"));
            c.Ellipse(18, 12.5f, 1.2f, 1.8f, H("#7a3c6c"));
            c.Ellipse(11, 7, 9, 6.5f, body);
            c.EllipseOver(9, 9, 6, 4, H("#4c3a70"));
            c.EllipseOver(11, 2.5f, 9, 2.5f, H("#2a1f40"));
            // 눈
            c.Rect(6, 7, 3, 3, H("#ff6a8c"));
            c.Rect(13, 7, 3, 3, H("#ff6a8c"));
            c.Set(7, 8, H("#ffe0ea"));
            c.Set(14, 8, H("#ffe0ea"));
            // 이빨
            c.Set(10, 4, H("#e8e0f0"));
            c.Set(12, 4, H("#e8e0f0"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite InkBoss => Cached("boss", () =>
        {
            var c = new PixelCanvas(44, 44);
            var ink = H("#1d1430");
            c.Ellipse(22, 22, 18, 15, ink);
            c.Ellipse(12, 33, 7, 7, ink);
            c.Ellipse(30, 35, 8, 7, ink);
            c.Ellipse(21, 37, 6, 5, ink);
            // 흘러내리는 먹물
            int[] dripX = { 8, 15, 24, 31, 37 }, dripLen = { 7, 10, 6, 9, 5 };
            for (int i = 0; i < dripX.Length; i++)
            {
                int x = dripX[i], len = dripLen[i];
                c.Rect(x, 9 - len, 3, len + 2, ink);
                c.Ellipse(x + 1.5f, 9 - len, 2.2f, 2.2f, ink);
            }
            c.EllipseOver(17, 28, 10, 7, H("#33255a"));
            c.EllipseOver(14, 31, 4, 3, H("#4a3780"));
            // 눈 셋
            float[] eyeX = { 14f, 28f, 21f }, eyeY = { 22f, 23f, 30f }, eyeR = { 3f, 3.4f, 2.2f };
            for (int i = 0; i < eyeX.Length; i++)
            {
                float x = eyeX[i], y = eyeY[i], r = eyeR[i];
                c.Ellipse(x, y, r, r * 0.8f, H("#c890ff"));
                c.Ellipse(x, y, r * 0.45f, r * 0.45f, H("#fff2ff"));
            }
            c.HLine(15, 14, 14, H("#0c0816"));
            c.Outline(H("#080510"));
            return c.ToSprite();
        });

        public static Sprite Mailbox => Cached("mailbox", () =>
        {
            var c = new PixelCanvas(14, 24);
            c.Rect(5, 0, 4, 10, H("#4a3a30"));
            c.Rect(2, 9, 10, 11, H("#b8342f"));
            c.Ellipse(7, 19.5f, 5, 3, H("#b8342f"));
            c.Rect(2, 9, 2, 11, H("#8a2522"));
            c.Rect(4, 17, 6, 1, H("#2a0e10"));
            c.Rect(4, 12, 6, 2, H("#f0e6c8"));
            c.Set(5, 20, H("#e06a5a"));
            c.Set(6, 21, H("#e06a5a"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        // ================================================================ 건물

        public struct BuildingLook
        {
            public int widthTiles, heightTiles, wallPx;
            public string wall, roof;
            public bool brick, thatch;
            public int windows;
            public bool windowsLit, door, boarded;
            /// <summary>0 없음, 1 우체국(봉투), 2 빵집(빵)</summary>
            public int sign;
        }

        public static Sprite Building(string key, BuildingLook b) => Cached(key, () =>
        {
            int w = b.widthTiles * 16, h = b.heightTiles * 16, wallH = b.wallPx;
            var c = new PixelCanvas(w, h);
            Color32 wall = H(b.wall), wallDark = PixelCanvas.Shade(wall, 0.78f), wallLight = PixelCanvas.Shade(wall, 1.12f);
            Color32 roof = H(b.roof), roofDark = PixelCanvas.Shade(roof, 0.72f), roofLight = PixelCanvas.Shade(roof, 1.25f);

            // 벽
            c.Rect(1, 0, w - 2, wallH, wall);
            for (int y = 0; y < wallH; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    if (b.brick)
                    {
                        bool mortar = y % 4 == 0 || (x + (y / 4 % 2) * 4) % 8 == 0;
                        if (mortar) c.Set(x, y, wallDark);
                        else if (PixelCanvas.Hash(x / 8, y / 4, 3) > 0.7f) c.Set(x, y, wallLight);
                    }
                    else if (y % 4 == 0) c.Set(x, y, wallDark);
                    else if (PixelCanvas.Hash(x, y, 5) > 0.975f) c.Set(x, y, wallDark);
                }
            c.Rect(1, 0, w - 2, 2, H("#3a3a48"));
            c.VLine(1, 0, wallH, wallDark);
            c.VLine(w - 2, 0, wallH, wallDark);

            // 지붕 (벽 위쪽 전체)
            int roofBottom = wallH - 3;
            c.Rect(0, roofBottom, w, h - roofBottom, roof);
            for (int y = roofBottom; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int ry = y - roofBottom;
                    if (b.thatch)
                    {
                        if (PixelCanvas.Hash(x, y / 2, 9) > 0.75f) c.Set(x, y, roofDark);
                        else if (PixelCanvas.Hash(x, y / 2, 11) > 0.85f) c.Set(x, y, roofLight);
                    }
                    else
                    {
                        if (ry % 5 == 0) c.Set(x, y, roofDark);
                        else if (ry % 5 == 1 && (x + (ry / 5) * 3) % 6 == 0) c.Set(x, y, roofDark);
                    }
                }
            c.Rect(0, h - 3, w, 3, roofLight);
            c.HLine(0, h - 4, w, roof);
            c.Rect(0, roofBottom, w, 2, roofDark);
            c.HLine(1, roofBottom - 1, w - 2, H("#1c1a26")); // 처마 그림자
            c.VLine(0, roofBottom, h - roofBottom, roofDark);
            c.VLine(w - 1, roofBottom, h - roofBottom, roofDark);

            // 창문
            int doorX = w / 2;
            var slots = new List<int>();
            for (int i = 0; i < b.windows; i++)
            {
                float t = (i + 1f) / (b.windows + 1f);
                int x = Mathf.RoundToInt(w * t);
                if (b.door && Mathf.Abs(x - doorX) < 14) x += x < doorX ? -10 : 10;
                slots.Add(x);
            }
            int winY = Mathf.Max(6, wallH / 2 - 4);
            foreach (int x in slots) Window(c, x - 5, winY, b.windowsLit);

            // 문
            if (b.door)
            {
                int dx = doorX - 6;
                c.Rect(dx - 1, 0, 14, 21, H("#2a1c18"));
                c.Rect(dx, 0, 12, 20, H("#6b4630"));
                for (int x = dx + 3; x < dx + 12; x += 4) c.VLine(x, 0, 20, H("#55371f"));
                c.Set(dx + 9, 9, H("#e6c46a"));
                if (b.boarded)
                {
                    var plank = H("#8a6a48");
                    for (int i = 0; i < 14; i++)
                    {
                        c.Set(dx - 1 + i, 4 + i, plank);
                        c.Set(dx - 1 + i, 5 + i, plank);
                        c.Set(dx - 1 + i, 17 - i, plank);
                        c.Set(dx - 1 + i, 18 - i, plank);
                    }
                }
            }

            // 간판
            if (b.sign > 0)
            {
                int sx = doorX - 8, sy = 22;
                c.Rect(sx, sy, 16, 10, H("#2a1c18"));
                c.Rect(sx + 1, sy + 1, 14, 8, H("#d9b77a"));
                if (b.sign == 1)
                {
                    c.Rect(sx + 3, sy + 2, 10, 6, H("#f6f1e2"));
                    for (int i = 0; i < 5; i++)
                    {
                        c.Set(sx + 3 + i, sy + 7 - i / 2 - (i > 2 ? 0 : 0), H("#b8342f"));
                        c.Set(sx + 12 - i, sy + 7 - i / 2, H("#b8342f"));
                    }
                }
                else
                {
                    c.Ellipse(sx + 8, sy + 5, 5, 2.6f, H("#c08040"));
                    c.HLine(sx + 5, sy + 6, 6, H("#e8b070"));
                }
            }
            return c.ToSprite();
        });

        static void Window(PixelCanvas c, int x, int y, bool lit)
        {
            c.Rect(x - 1, y - 1, 12, 13, H("#2a1c18"));
            Color32 glass = lit ? H("#ffd37a") : H("#26304c");
            Color32 glassHi = lit ? H("#fff0b8") : H("#3c4a70");
            c.Rect(x, y, 10, 11, glass);
            c.Rect(x + 1, y + 7, 3, 3, glassHi);
            c.VLine(x + 5, y, 11, H("#2a1c18"));
            c.HLine(x, y + 5, 10, H("#2a1c18"));
            c.HLine(x - 1, y - 2, 12, H("#7a6a5a"));
        }

        // ================================================================ 자연물

        public static Sprite RoundTree(int variant) => Cached("tree" + variant, () =>
        {
            var c = new PixelCanvas(48, 64);
            var trunk = H("#4a3426");
            c.Rect(20, 0, 8, 22, trunk);
            c.VLine(22, 2, 18, H("#3a281e"));
            c.VLine(26, 4, 14, H("#5c4232"));
            c.Rect(18, 0, 3, 3, trunk);
            c.Rect(27, 0, 3, 3, trunk);

            Color32 dark = H(variant % 2 == 0 ? "#173a3a" : "#1b3a30");
            Color32 mid = H(variant % 2 == 0 ? "#22504c" : "#285040");
            Color32 light = H(variant % 2 == 0 ? "#33706a" : "#3c6e52");
            float o = (variant % 3) - 1;
            c.Ellipse(24, 38, 21, 17, dark);
            c.Ellipse(13 + o, 31, 11, 10, dark);
            c.Ellipse(35 - o, 31, 11, 10, dark);
            c.Ellipse(24, 52, 14, 11, dark);
            c.EllipseOver(21, 42, 17, 14, mid);
            c.EllipseOver(18, 47, 10, 8, light);
            c.EllipseOver(12, 35, 5, 4, light);
            for (int y = 18; y < 64; y++)
                for (int x = 0; x < 48; x++)
                    if (c.Filled(x, y) && y > 20 && PixelCanvas.Hash(x, y, variant + 20) > 0.9f)
                        c.Set(x, y, PixelCanvas.Hash(x, y, 77) > 0.5f ? dark : light);
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite PineTree(int variant) => Cached("pine" + variant, () =>
        {
            var c = new PixelCanvas(40, 64);
            c.Rect(17, 0, 6, 12, H("#3e2c22"));
            Color32 dark = H("#132e2c"), mid = H("#1c423a"), light = H("#2b5a4a");
            for (int layer = 0; layer < 4; layer++)
            {
                int baseY = 8 + layer * 12;
                int half = 18 - layer * 4;
                for (int y = 0; y < 18; y++)
                {
                    int span = Mathf.RoundToInt(half * (1f - y / 18f));
                    for (int x = 20 - span; x <= 20 + span; x++)
                    {
                        var col = x < 20 - span / 3 ? mid : dark;
                        if (x < 20 - span / 2 && y > 2) col = light;
                        c.Set(x, baseY + y, col);
                    }
                }
            }
            for (int y = 8; y < 64; y++)
                for (int x = 0; x < 40; x++)
                    if (c.Filled(x, y) && PixelCanvas.Hash(x, y, variant + 40) > 0.92f) c.Set(x, y, dark);
            c.Outline(Ink);
            return c.ToSprite();
        });

        /// <summary>덤불·울타리처럼 넓은 영역을 채우는 잎 덩어리. 크기는 타일 단위.</summary>
        public static Sprite Foliage(string key, int widthTiles, int heightTiles, int seed) => Cached(key, () =>
        {
            int w = widthTiles * 16, h = heightTiles * 16;
            var c = new PixelCanvas(w, h + 10);
            Color32 dark = H("#102a28"), mid = H("#19403a"), light = H("#2a5e50");
            int count = w * h / 70 + 4;
            for (int i = 0; i < count; i++)
            {
                float x = PixelCanvas.Hash(i, 1, seed) * w;
                float y = PixelCanvas.Hash(i, 2, seed) * h + 2;
                float r = 5 + PixelCanvas.Hash(i, 3, seed) * 6;
                c.Ellipse(x, y, r, r * 0.85f, dark);
            }
            for (int i = 0; i < count; i++)
            {
                float x = PixelCanvas.Hash(i, 1, seed) * w;
                float y = PixelCanvas.Hash(i, 2, seed) * h + 2;
                float r = 5 + PixelCanvas.Hash(i, 3, seed) * 6;
                c.EllipseOver(x - r * 0.25f, y + r * 0.3f, r * 0.65f, r * 0.55f, mid);
                c.EllipseOver(x - r * 0.4f, y + r * 0.45f, r * 0.3f, r * 0.25f, light);
            }
            // 가장자리가 직선이 되도록 바깥 영역 정리
            c.Outline(Ink);
            return c.ToSprite(0.5f, 0f);
        });

        public static Sprite Log => Cached("log", () =>
        {
            var c = new PixelCanvas(22, 132);
            var bark = H("#5a3e2a");
            c.Rect(2, 4, 18, 124, bark);
            for (int x = 3; x < 20; x += 3)
                for (int y = 4; y < 128; y++)
                    if (PixelCanvas.Hash(x, y / 3, 13) > 0.35f) c.Set(x, y, H("#45301f"));
            c.VLine(5, 4, 124, H("#6e4d34"));
            c.Ellipse(11, 127, 9, 4, H("#a07a50"));
            c.Ellipse(11, 127, 6, 2.5f, H("#8a6440"));
            c.Ellipse(11, 127, 2.5f, 1.2f, H("#a07a50"));
            c.Ellipse(11, 4, 9, 4, bark);
            // 부러진 가지
            c.Rect(19, 70, 3, 2, bark);
            c.Rect(0, 40, 3, 2, bark);
            c.Outline(Ink);
            return c.ToSprite(0.5f, 0.5f);
        });

        // ================================================================ 소품

        public static Sprite LampPost(bool lit) => Cached("lamp" + lit, () =>
        {
            var c = new PixelCanvas(10, 30);
            c.Rect(3, 0, 4, 2, H("#2a2a36"));
            c.Rect(4, 2, 2, 20, H("#34344a"));
            c.Rect(2, 21, 6, 2, H("#2a2a36"));
            c.Rect(2, 23, 6, 5, lit ? H("#ffe08a") : H("#4a4e60"));
            if (lit) c.Rect(3, 24, 2, 2, H("#fffbe0"));
            c.Rect(1, 28, 8, 1, H("#2a2a36"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite Counter => Cached("counter", () =>
        {
            var c = new PixelCanvas(32, 16);
            c.Rect(0, 0, 32, 10, H("#7a5236"));
            c.Rect(0, 8, 32, 3, H("#a6764e"));
            for (int x = 4; x < 32; x += 8) c.VLine(x, 0, 8, H("#5e3e28"));
            // 종과 편지 더미
            c.Ellipse(24, 13, 3, 2.5f, H("#e2c060"));
            c.Set(24, 16, H("#e2c060"));
            c.Rect(5, 11, 8, 3, H("#f1ead8"));
            c.Rect(6, 13, 7, 2, H("#e0d6c0"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite BakeryStall => Cached("stall", () =>
        {
            var c = new PixelCanvas(40, 32);
            c.Rect(3, 0, 2, 22, H("#4a3426"));
            c.Rect(35, 0, 2, 22, H("#4a3426"));
            c.Rect(1, 6, 38, 8, H("#8a5c3a"));
            c.Rect(1, 12, 38, 2, H("#a87448"));
            for (int i = 0; i < 4; i++) c.Ellipse(7 + i * 8.5f, 15.5f, 3.2f, 2.2f, H("#d08a48"));
            for (int i = 0; i < 4; i++) c.HLine(5 + i * 8 + 1, 16, 3, H("#f0c080"));
            for (int x = 0; x < 40; x++)
            {
                bool red = (x / 5) % 2 == 0;
                c.VLine(x, 22, 8, red ? H("#c0443a") : H("#efe6d2"));
                if (x % 5 == 2) c.Set(x, 21, red ? H("#c0443a") : H("#efe6d2"));
            }
            c.HLine(0, 29, 40, H("#8a2a24"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite FlourSack => Cached("sack", () =>
        {
            var c = new PixelCanvas(18, 14);
            c.Ellipse(9, 6, 6, 5.5f, H("#e8e1cf"));
            c.EllipseOver(10, 4, 5, 3, H("#c9c0aa"));
            c.Rect(7, 10, 4, 3, H("#e8e1cf"));
            c.HLine(7, 11, 4, H("#8a6a48"));
            c.Rect(7, 4, 3, 3, H("#b0423a"));
            c.Outline(Ink);
            // 쏟아진 밀가루
            int[] flourX = { 15, 16, 14, 1, 2, 17 }, flourY = { 1, 2, 0, 1, 0, 0 };
            for (int i = 0; i < flourX.Length; i++) c.Set(flourX[i], flourY[i], H("#f4f0e4"));
            return c.ToSprite();
        });

        public static Sprite Signpost => Cached("sign", () =>
        {
            var c = new PixelCanvas(20, 24);
            c.Rect(9, 0, 3, 16, H("#4a3426"));
            c.Rect(2, 12, 15, 9, H("#7a5a3c"));
            c.Rect(16, 14, 3, 5, H("#7a5a3c"));
            c.Set(19, 16, H("#7a5a3c"));
            c.HLine(4, 18, 10, H("#3a2a1e"));
            c.HLine(4, 15, 8, H("#3a2a1e"));
            c.Set(5, 13, H("#9a6a3a"));
            c.Set(14, 20, H("#9a6a3a"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        /// <summary>
        /// 등대(가로 3칸, 세로 7칸). 돌 받침 + 빨강·흰 줄무늬 탑 + 난간 + 등불 방 + 지붕.
        /// lit 이면 등불 방이 노랗게 빛난다.
        /// </summary>
        public static Sprite Lighthouse(bool lit) => Cached("lighthouse" + lit, () =>
        {
            var c = new PixelCanvas(48, 112);
            Color32 stone = H("#8a8a98"), stoneDark = H("#6a6a7a"), red = H("#c0443a"), redDark = H("#962f2a"),
                white = H("#efe8da"), whiteDark = H("#c9c0b0"), rail = H("#3a3448"), roof = H("#b03a32");

            // 돌 받침
            c.Rect(4, 0, 40, 16, stone);
            for (int y = 0; y < 16; y++)
                for (int x = 4; x < 44; x++)
                    if (y % 5 == 0 || (x + (y / 5) * 4) % 8 == 0) c.Set(x, y, stoneDark);
            c.Rect(20, 0, 8, 12, H("#2a2030"));
            c.Rect(21, 11, 6, 2, H("#2a2030"));
            c.Set(26, 6, H("#e8c060"));

            // 탑: 위로 갈수록 좁아지는 줄무늬
            for (int y = 16; y < 86; y++)
            {
                float t = (y - 16) / 70f;
                int half = Mathf.RoundToInt(Mathf.Lerp(15f, 10f, t));
                bool redBand = ((y - 16) / 12) % 2 == 0;
                for (int x = 24 - half; x < 24 + half; x++)
                {
                    bool shade = x > 24 + half / 3;
                    c.Set(x, y, redBand ? (shade ? redDark : red) : (shade ? whiteDark : white));
                }
            }
            // 작은 창
            c.Rect(22, 40, 4, 6, H("#2a2030"));
            c.Rect(22, 64, 4, 6, lit ? H("#ffd37a") : H("#2a2030"));

            // 난간
            c.Rect(10, 86, 28, 3, rail);
            for (int x = 11; x < 38; x += 3) c.VLine(x, 89, 3, rail);
            c.HLine(10, 92, 28, rail);

            // 등불 방
            c.Rect(14, 92, 20, 12, rail);
            c.Rect(16, 93, 16, 10, lit ? H("#ffe08a") : H("#2c3450"));
            if (lit) c.Rect(18, 95, 5, 5, H("#fffbe0"));
            c.VLine(21, 93, 10, rail);
            c.VLine(27, 93, 10, rail);

            // 지붕
            for (int y = 104; y < 111; y++)
            {
                int half = Mathf.RoundToInt(Mathf.Lerp(12f, 2f, (y - 104) / 7f));
                c.HLine(24 - half, y, half * 2, roof);
            }
            c.Set(24, 111, rail);
            c.Outline(Ink);
            return c.ToSprite();
        });

        public static Sprite Rock(bool large) => Cached("rock" + large, () =>
        {
            int w = large ? 32 : 18, h = large ? 26 : 14;
            var c = new PixelCanvas(w, h);
            c.Ellipse(w / 2f, h / 2f - 1, w / 2f - 1, h / 2f - 1, H("#6e6a74"));
            c.EllipseOver(w / 2f - 2, h / 2f + 1, w / 3f, h / 3f, H("#8a8692"));
            c.EllipseOver(w / 2f + 3, 3, w / 3f, 3, H("#55515c"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        /// <summary>물속으로 잠긴 보스가 남기는 먹물 웅덩이.</summary>
        public static Sprite InkPuddle => Cached("puddle", () =>
        {
            var c = new PixelCanvas(20, 10);
            c.Ellipse(10, 5, 9.5f, 4.5f, H("#2a1a40"));
            c.EllipseOver(9, 6, 6, 2.2f, H("#4a2f70"));
            c.Set(6, 7, H("#9a7ad0"));
            c.Set(13, 5, H("#9a7ad0"));
            return c.ToSprite(0.5f, 0.3f);
        });

        /// <summary>땅속을 파고드는 적의 흙더미.</summary>
        public static Sprite Mound => Cached("mound", () =>
        {
            var c = new PixelCanvas(16, 8);
            c.Ellipse(8, 3, 7.5f, 3.5f, H("#5a4030"));
            c.EllipseOver(7, 4.5f, 5, 2, H("#7a5840"));
            c.Set(4, 5, H("#9a7858"));
            c.Set(11, 4, H("#9a7858"));
            c.Outline(Ink);
            return c.ToSprite(0.5f, 0.3f);
        });

        public static Sprite Chest(bool open) => Cached("chest" + open, () =>
        {
            var c = new PixelCanvas(16, 14);
            c.Rect(1, 0, 14, 8, H("#8a5a32"));
            c.HLine(1, 4, 14, H("#6a4224"));
            if (open)
            {
                c.Rect(1, 8, 14, 5, H("#3a2418"));
                c.Rect(1, 12, 14, 2, H("#a87040"));
            }
            else
            {
                c.Rect(1, 8, 14, 4, H("#a87040"));
                c.Rect(7, 6, 2, 3, H("#e8c060"));
            }
            c.VLine(3, 0, 12, H("#e8c060"));
            c.VLine(12, 0, 12, H("#e8c060"));
            c.Outline(Ink);
            return c.ToSprite();
        });

        // ================================================================ 효과·조명

        public static Sprite Shadow => Cached("shadow", () =>
        {
            var c = new PixelCanvas(16, 6);
            c.Ellipse(8, 3, 7.5f, 2.6f, new Color32(0, 0, 0, 90));
            return c.ToSprite(0.5f, 0.5f);
        });

        /// <summary>
        /// 에셋 캐릭터 발밑의 작은 그림자(12x4 픽셀). 에셋 캐릭터는 발 아래 외곽선이 없어서
        /// 어두운 바닥에 묻혀 보이므로, 발 바로 아래에 진한 그림자를 깔아 "서 있는" 느낌을 낸다.
        /// </summary>
        public static Sprite FootShadow => Cached("footShadow", () =>
        {
            var c = new PixelCanvas(12, 4);
            c.Ellipse(6, 2, 6, 2, new Color32(0, 0, 0, 140));
            return c.ToSprite(0.5f, 0.5f);
        });

        /// <summary>부드러운 원형 빛. 1 유닛 지름.</summary>
        public static Sprite Glow => Cached("glow", () =>
        {
            const int n = 64;
            var c = new PixelCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float a = Mathf.Clamp01(1f - d);
                    c.Set(x, y, new Color32(255, 255, 255, (byte)(a * a * 255)));
                }
            return Sprite.Create(c.ToTexture(), new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        });

        /// <summary>화면 가장자리를 어둡게 하는 밤 그림자. 가운데가 투명하다. 1 유닛 크기.</summary>
        public static Sprite Darkness => Cached("dark", () =>
        {
            const int n = 128;
            var c = new PixelCanvas(n, n);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n - 0.5f, dy = (y + 0.5f) / n - 0.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                    float t = Mathf.Clamp01((d - 0.18f) / 0.55f);
                    t = t * t * (3 - 2 * t);
                    c.Set(x, y, new Color32(6, 8, 24, (byte)(t * 200)));
                }
            var tex = c.ToTexture();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        });

        public static Sprite Slash => Cached("slash", () =>
        {
            var c = new PixelCanvas(32, 32);
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++)
                {
                    float dx = x + 0.5f - 16, dy = y + 0.5f - 16;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (ang < 25 || ang > 155) continue;
                    if (d >= 11 && d <= 14.5f) c.Set(x, y, d > 13 ? new Color32(255, 255, 255, 255) : new Color32(255, 240, 190, 200));
                }
            return c.ToSprite(0.5f, 0.5f);
        });

        public static Sprite Dot => Cached("dot", () =>
        {
            var c = new PixelCanvas(3, 3);
            c.Set(1, 1, new Color32(255, 255, 255, 255));
            c.Set(0, 1, new Color32(255, 255, 255, 120));
            c.Set(2, 1, new Color32(255, 255, 255, 120));
            c.Set(1, 0, new Color32(255, 255, 255, 120));
            c.Set(1, 2, new Color32(255, 255, 255, 120));
            return c.ToSprite(0.5f, 0.5f);
        });

        // ================================================================ UI 아이콘

        static readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();

        static Texture2D Icon(string key, string[] rows, System.Func<char, Color32?> palette)
        {
            if (icons.TryGetValue(key, out var t) && t != null) return t;
            var c = new PixelCanvas(rows[0].Length, rows.Length);
            c.Stamp(rows, 0, 0, palette);
            t = c.ToTexture();
            icons[key] = t;
            return t;
        }

        static readonly string[] HeartRows =
        {
            ".oo...oo.",
            "oxxo.oxxo",
            "oxhxoxxxo",
            "oxxxxxxxo",
            ".oxxxxxo.",
            "..oxxxo..",
            "...oxo...",
            "....o....",
        };

        public static Texture2D HeartFull => Icon("heart", HeartRows, ch =>
            ch == 'o' ? Ink : ch == 'x' ? H("#e8405a") : ch == 'h' ? H("#ffb0bc") : (Color32?)null);

        public static Texture2D HeartEmpty => Icon("heartEmpty", HeartRows, ch =>
            ch == 'o' ? Ink : (ch == 'x' || ch == 'h') ? H("#3a3048") : (Color32?)null);

        public static Texture2D Envelope => Icon("envelope", new[]
        {
            "oooooooooooo",
            "owwwwwwwwwwo",
            "orwwwwwwwwro",
            "owrwwwwwwrwo",
            "owwrwwwwrwwo",
            "owwwrrrrwwwo",
            "owwwwwwwwwwo",
            "oooooooooooo",
        }, ch => ch == 'o' ? Ink : ch == 'w' ? H("#f3ecd8") : ch == 'r' ? H("#b8342f") : (Color32?)null);
    }
}
