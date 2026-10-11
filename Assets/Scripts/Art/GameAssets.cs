using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 캐릭터 시트: 열 = 방향(아래, 위, 왼쪽, 오른쪽), 행 = 프레임(0~3 걷기, 4 공격 …).
    /// Mana Seed 캐릭터는 서 있는 그림이 따로 있고(stand), 걷기가 6프레임이며, 칼 공격 같은 이름 붙은 동작(actions)을 가진다.
    /// </summary>
    public class CharacterSheet
    {
        public const int Down = 0, Up = 1, Left = 2, Right = 3;

        readonly Sprite[,] frames;
        readonly Sprite[] stand;
        readonly Dictionary<string, Sprite[,]> actions = new Dictionary<string, Sprite[,]>();
        readonly Dictionary<string, float[]> weights = new Dictionary<string, float[]>();
        public int Rows { get; }
        public float WalkFps { get; } = 8f;

        public CharacterSheet(Sprite[,] frames, int rows)
        {
            this.frames = frames;
            Rows = rows;
        }

        public CharacterSheet(Sprite[,] walk, int walkFrames, Sprite[] stand, float walkFps)
        {
            frames = walk;
            Rows = walkFrames;
            this.stand = stand;
            WalkFps = walkFps;
            WalkFramesOverride = walkFrames;
        }

        int WalkFramesOverride;

        public Sprite Get(int dir, int row) => frames[Mathf.Clamp(dir, 0, 3), Mathf.Clamp(row, 0, Rows - 1)];
        public Sprite Stand(int dir) => stand != null ? stand[Mathf.Clamp(dir, 0, 3)] : Get(dir, 0);

        /// <summary>걷기에 쓰는 행 수(옛 시트는 최대 4).</summary>
        public int WalkFrames => WalkFramesOverride > 0 ? WalkFramesOverride : Mathf.Min(4, Rows);
        public bool HasAttackRow => WalkFramesOverride == 0 && Rows > 4;

        /// <summary>이름 붙은 동작. frameWeights 는 프레임마다 보여 주는 시간 비율(없으면 똑같이).</summary>
        public void AddAction(string name, Sprite[,] dirFrames, float[] frameWeights = null)
        {
            actions[name] = dirFrames;
            if (frameWeights != null) weights[name] = frameWeights;
        }

        public bool HasAction(string name) => actions.ContainsKey(name);

        /// <summary>동작 name 을 t(0~1) 만큼 진행했을 때의 그림.</summary>
        public Sprite Action(string name, int dir, float t)
        {
            var f = actions[name];
            int n = f.GetLength(1);
            int i = n - 1;
            if (weights.TryGetValue(name, out var w))
            {
                float sum = 0f, total = 0f;
                foreach (var x in w) total += x;
                for (int k = 0; k < n && k < w.Length; k++)
                {
                    sum += w[k] / total;
                    if (t < sum) { i = k; break; }
                }
            }
            else i = Mathf.Clamp((int)(t * n), 0, n - 1);
            return f[Mathf.Clamp(dir, 0, 3), i];
        }
    }

    /// <summary>
    /// Assets/Resources/Art/NinjaAdventure 의 에셋(CC0, Pixel-boy &amp; AAA)을 불러와 자른다.
    /// 에셋이 없거나 임포트 설정이 잘못되면 Available 이 false 가 되고, 게임은 코드로 그린 그림(Art)을 쓴다.
    /// 타일 좌표는 시트의 왼쪽 위가 (0,0)이고 한 칸이 16 픽셀이다.
    /// </summary>
    public static class GameAssets
    {
        const string Root = "Art/NinjaAdventure/";
        const int T = 16;

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, CharacterSheet> sheets = new Dictionary<string, CharacterSheet>();
        static bool? available;

        /// <summary>밤 분위기를 위해 낮 색감의 에셋에 곱하는 색.</summary>
        public static readonly Color NightTint = new Color(0.5f, 0.56f, 0.82f);
        public static readonly Color CharacterTint = Color.white; // 캐릭터는 밤 색을 입히지 않아 배경에서 또렷하게 보이게 한다
        public static readonly Color SeaTint = new Color(0.32f, 0.42f, 0.78f);

        public static bool Available
        {
            get
            {
                if (!available.HasValue)
                {
                    var floor = Texture("Tilesets/TilesetFloor");
                    available = floor != null && floor.width == 352;
                    if (floor != null && !available.Value)
                        Debug.LogWarning("[달빛 우체국] 타일셋 크기가 예상과 다릅니다(" + floor.width + "). " +
                                         "메뉴 「달빛 우체국 > 아트 다시 가져오기」를 눌러 주세요. 지금은 코드로 그린 그림을 씁니다.");
                }
                return available.Value;
            }
        }

        public static Texture2D Texture(string path)
        {
            if (textures.TryGetValue(path, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>(Root + path);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            textures[path] = tex;
            return tex;
        }

        /// <summary>픽셀 단위 영역(왼쪽 위 기준)을 스프라이트로 자른다.</summary>
        public static Sprite Region(string path, int x, int y, int w, int h, float pivotX = 0.5f, float pivotY = 0f)
        {
            string key = path + ":" + x + "," + y + "," + w + "," + h + ":" + pivotX + "," + pivotY;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = Texture(path);
            if (tex == null || x + w > tex.width || y + h > tex.height) return null;
            s = Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), new Vector2(pivotX, pivotY), T, 0, SpriteMeshType.FullRect);
            sprites[key] = s;
            return s;
        }

        /// <summary>타일 단위로 자른다. 바닥 타일은 pivot (0,0), 세워 두는 물건은 (0.5,0).</summary>
        public static Sprite Tile(string sheet, int tx, int ty, int w = 1, int h = 1, float pivotX = 0.5f, float pivotY = 0f) =>
            Region("Tilesets/" + sheet, tx * T, ty * T, w * T, h * T, pivotX, pivotY);

        public static Sprite GroundTile(string sheet, int tx, int ty) => Tile(sheet, tx, ty, 1, 1, 0f, 0f);

        /// <summary>가로로 이어진 애니메이션 프레임.</summary>
        public static Sprite[] Strip(string path, int frameW, int frameH, int count, float pivotX = 0.5f, float pivotY = 0.5f)
        {
            var tex = Texture(path);
            if (tex == null) return null;
            var result = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Region(path, i * frameW, 0, frameW, frameH, pivotX, pivotY);
                if (result[i] == null) return null;
            }
            return result;
        }

        // ---------------------------------------------------------------- Mana Seed 캐릭터 (Seliel the Shaper)

        /// <summary>Resources/Art/ManaSeed: 페이퍼돌 층을 미리 겹쳐 만든 캐릭터 페이지(512x512, 64x64 칸 8x8).</summary>
        const string ManaRoot = "Art/ManaSeed/";
        /// <summary>Mana Seed 캐릭터는 키가 32픽셀이라, 1 유닛 = 32 픽셀로 잘라 지금 캐릭터와 같은 키(1 유닛)로 맞춘다.</summary>
        public const float ManaPpu = 32f;
        /// <summary>칸 안에서 발밑의 높이(아래에서 20픽셀).</summary>
        const float ManaPivotY = 20f / 64f;
        /// <summary>시트의 방향 순서(아래, 위, 오른쪽, 왼쪽) → 게임의 방향(아래, 위, 왼쪽, 오른쪽).</summary>
        static readonly int[] ManaRowOf = { 0, 1, 3, 2 };

        static Texture2D ManaTexture(string file)
        {
            string key = "~mana/" + file;
            if (textures.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>(ManaRoot + file);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            textures[key] = tex;
            return tex;
        }

        static Sprite ManaCell(Texture2D tex, int col, int row)
        {
            const int C = 64;
            return Sprite.Create(tex, new Rect(col * C, tex.height - (row + 1) * C, C, C), new Vector2(0.5f, ManaPivotY), ManaPpu, 0, SpriteMeshType.FullRect);
        }

        /// <summary>block 안의 4방향 x frames 칸(방향은 행, 프레임은 열).</summary>
        static Sprite[,] ManaBlock(Texture2D tex, int col0, int row0, params int[] cols)
        {
            var f = new Sprite[4, cols.Length];
            for (int dir = 0; dir < 4; dir++)
                for (int i = 0; i < cols.Length; i++)
                    f[dir, i] = ManaCell(tex, col0 + cols[i], row0 + ManaRowOf[dir]);
            return f;
        }

        static CharacterSheet ManaSheet(string actor)
        {
            var p1 = ManaTexture(actor + "_p1");
            if (p1 == null) return null;
            var stand = new Sprite[4];
            for (int dir = 0; dir < 4; dir++) stand[dir] = ManaCell(p1, 0, ManaRowOf[dir]);
            // 걷기: 4~7행의 앞 6칸, 135ms 씩
            var sheet = new CharacterSheet(ManaBlock(p1, 0, 4, 0, 1, 2, 3, 4, 5), 6, stand, 7.4f);
            // 달리기(회피): 1,2,7,4,5,8 번째 칸
            sheet.AddAction("run", ManaBlock(p1, 0, 4, 0, 1, 6, 3, 4, 7), new[] { 80f, 55f, 125f, 80f, 55f, 125f });

            var one1 = ManaTexture(actor + "_pONE1");
            var one3 = ManaTexture(actor + "_pONE3");
            if (one1 != null && one3 != null)
            {
                // 칼과 방패: 1·2타 베기, 3타 찌르기, 방패 치기. 160/65/65/200ms 비율로 공격 시간에 맞춰 늘이고 줄인다.
                var attack = new[] { 160f, 65f, 65f, 200f };
                sheet.AddAction("slash1", ManaBlock(one3, 0, 0, 0, 1, 2, 3), attack);
                sheet.AddAction("slash2", ManaBlock(one3, 4, 0, 0, 1, 2, 3), attack);
                sheet.AddAction("thrust", ManaBlock(one3, 0, 4, 0, 1, 2, 3), attack);
                sheet.AddAction("bash", ManaBlock(one3, 4, 4, 0, 1, 2, 3), attack);
                sheet.AddAction("evade", ManaBlock(one1, 4, 0, 0));
                sheet.AddAction("hurt", ManaBlock(one1, 5, 0, 0));
            }
            return sheet;
        }

        // ---------------------------------------------------------------- Cainos 바닥·식물·소품 (32x32)

        /// <summary>
        /// Resources/Art/Cainos: Cainos 「Pixel Art Top Down - Basic」. 재배포가 금지라 저장소에는 넣지 않는다
        /// (Tools/import_art.py 가 드라이브의 zip 에서 만든다). 없으면 CainosAvailable 이 false 이고 Ninja Adventure 그림을 쓴다.
        /// </summary>
        const string CainosRoot = "Art/Cainos/";
        static bool? cainos;
        public static bool CainosAvailable => cainos ?? (cainos = CainosTexture("Grass") != null).Value;

        static Texture2D CainosTexture(string file)
        {
            string key = "~cainos/" + file;
            if (textures.TryGetValue(key, out var tex) && tex != null) return tex;
            tex = Resources.Load<Texture2D>(CainosRoot + file);
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
            }
            textures[key] = tex;
            return tex;
        }

        /// <summary>Cainos 시트의 픽셀 영역(왼쪽 위 기준). 1 유닛 = 32 픽셀.</summary>
        public static Sprite Cainos(string file, int x, int y, int w, int h, float pivotX = 0.5f, float pivotY = 0f)
        {
            string key = "~cainos/" + file + ":" + x + "," + y + "," + w + "," + h + ":" + pivotX + "," + pivotY;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var tex = CainosTexture(file);
            if (tex == null || x + w > tex.width || y + h > tex.height) return null;
            s = Sprite.Create(tex, new Rect(x, tex.height - y - h, w, h), new Vector2(pivotX, pivotY), 32f, 0, SpriteMeshType.FullRect);
            sprites[key] = s;
            return s;
        }

        /// <summary>Cainos 바닥 타일 한 칸(32x32, 왼쪽 아래 기준).</summary>
        public static Sprite CainosTile(string file, int tx, int ty) => Cainos(file, tx * 32, ty * 32, 32, 32, 0f, 0f);

        /// <summary>Cainos 나무 세 종류(밑동 가운데가 기준점).</summary>
        public static Sprite CainosTree(int v)
        {
            switch (((v % 3) + 3) % 3)
            {
                case 0: return Cainos("Plant", 24, 14, 113, 139);
                case 1: return Cainos("Plant", 161, 17, 97, 136);
                default: return Cainos("Plant", 295, 31, 79, 120);
            }
        }

        /// <summary>Cainos 덤불 여섯 종류.</summary>
        public static Sprite CainosBush(int v)
        {
            switch (((v % 6) + 6) % 6)
            {
                case 0: return Cainos("Plant", 156, 190, 38, 32);
                case 1: return Cainos("Plant", 216, 185, 47, 42);
                case 2: return Cainos("Plant", 282, 186, 39, 45);
                case 3: return Cainos("Plant", 346, 190, 40, 35);
                case 4: return Cainos("Plant", 38, 198, 22, 19);
                default: return Cainos("Plant", 98, 195, 27, 25);
            }
        }

        /// <summary>Cainos 풀포기 열네 종류(14번째 줄부터의 작은 그림).</summary>
        public static Sprite CainosTuft(int v)
        {
            int i = ((v % 14) + 14) % 14;
            return Cainos("Plant", (i % 4) * 32, 384 + (i / 4) * 32, 32, 32, 0.5f, 0f);
        }

        // ---------------------------------------------------------------- 몬스터 (Matseb2611 「Pixel Monsters Free Pack」)

        /// <summary>
        /// Resources/Art/Monsters: 3x4 걷기 시트(열 = 걸음 0·1·2, 행 = 아래·왼쪽·오른쪽·위). 재배포 조건이 적혀 있지 않아
        /// 저장소에는 넣지 않고 Tools/import_art.py 로 가져온다. 몬스터들은 그림 크기가 제각각이라 1 유닛 = 44 픽셀로 맞춘다.
        /// </summary>
        const string MonsterRoot = "Art/Monsters/";
        const float MonsterPpu = 44f;
        /// <summary>시트의 행(아래, 왼쪽, 오른쪽, 위) → 게임의 방향(아래, 위, 왼쪽, 오른쪽).</summary>
        static readonly int[] MonsterRowOf = { 0, 3, 1, 2 };

        public static CharacterSheet MonsterSheet(string name)
        {
            string key = "~monster/" + name;
            if (sheets.TryGetValue(key, out var cached)) return cached;
            var tex = Resources.Load<Texture2D>(MonsterRoot + name);
            CharacterSheet sheet = null;
            if (tex != null)
            {
                tex.filterMode = FilterMode.Point;
                tex.wrapMode = TextureWrapMode.Clamp;
                int fw = tex.width / 3, fh = tex.height / 4;
                // 발밑: 프레임 아래에서 비어 있는 줄 수(그림마다 잰 값)
                int gap = name == "Goblin" ? 1 : name == "Mosquito" ? 8 : name == "EvilHead" ? 9 : name == "Skull" ? 9 : 4;
                var pivot = new Vector2(0.5f, gap / (float)fh);
                System.Func<int, int, Sprite> cell = (c, r) =>
                    Sprite.Create(tex, new Rect(c * fw, tex.height - (r + 1) * fh, fw, fh), pivot, MonsterPpu, 0, SpriteMeshType.FullRect);
                var walk = new Sprite[4, 4];
                var stand = new Sprite[4];
                for (int dir = 0; dir < 4; dir++)
                {
                    int row = MonsterRowOf[dir];
                    stand[dir] = cell(1, row);
                    walk[dir, 0] = cell(0, row);
                    walk[dir, 1] = stand[dir];
                    walk[dir, 2] = cell(2, row);
                    walk[dir, 3] = stand[dir];
                }
                sheet = new CharacterSheet(walk, 4, stand, 7f);
            }
            sheets[key] = sheet;
            return sheet;
        }

        /// <summary>Mana Seed 초상화(서 있는 그림의 머리를 키운 것). 없으면 null.</summary>
        public static Texture2D ManaFace(string actor) => ManaTexture(actor + "_face");

        public static CharacterSheet Sheet(string actor)
        {
            if (sheets.TryGetValue(actor, out var sheet)) return sheet;
            sheet = ManaSheet(actor);
            if (sheet != null)
            {
                sheets[actor] = sheet;
                return sheet;
            }
            var tex = Texture("Actors/" + actor);
            if (tex != null && tex.width == 64 && tex.height % T == 0)
            {
                int rows = tex.height / T;
                var frames = new Sprite[4, rows];
                for (int dir = 0; dir < 4; dir++)
                    for (int row = 0; row < rows; row++)
                        frames[dir, row] = Region("Actors/" + actor, dir * T, row * T, T, T, 0.5f, 0f);
                sheet = new CharacterSheet(frames, rows);
            }
            sheets[actor] = sheet;
            return sheet;
        }

        public static Texture2D Faceset(string actor) => ManaFace(actor) ?? Texture("Facesets/" + actor);

        /// <summary>대화창 초상화: 말하는 사람 이름 → 얼굴 그림.</summary>
        public static Texture2D PortraitFor(string speaker)
        {
            if (!Available || string.IsNullOrEmpty(speaker)) return null;
            switch (speaker)
            {
                case "미라": return Faceset("Woman");
                case "오웬": return Faceset("Hunter");
                case "노아": return Faceset("Child");
                case "세린": return Faceset("Princess");
                case "도라": return Faceset("OldWoman");
                case "테오": return Faceset("OldMan");
                case "바우": return Faceset("OldMan2");
                case "유나": return Faceset("SorcererBlack");
                default: return null;
            }
        }

        /// <summary>얼굴 그림을 다른 주민과 함께 쓰는 주민의 색(월드의 캐릭터 색과 같다).</summary>
        public static Color PortraitTint(string speaker)
        {
            switch (speaker)
            {
                default: return Color.white;
            }
        }


        public static AudioClip Sfx(string name) => Resources.Load<AudioClip>(Root + "Audio/Sfx/" + name);
        public static AudioClip Music(string name) => Resources.Load<AudioClip>(Root + "Audio/Music/" + name);

        // ---------------------------------------------------------------- 자주 쓰는 그림

        public static Texture2D ItemIcon(string icon) => string.IsNullOrEmpty(icon) ? null : Texture("Items/" + icon);

        /// <summary>아이템 그림을 월드에 놓을 스프라이트로(발밑 기준).</summary>
        public static Sprite ItemSprite(string icon)
        {
            var tex = ItemIcon(icon);
            return tex == null ? null : Region("Items/" + icon, 0, 0, tex.width, tex.height, 0.5f, 0f);
        }
        public static Sprite ChestClosed => Region("Items/Chest", 0, 0, 16, 16);
        public static Sprite ChestOpen => Region("Items/Chest", 16, 0, 16, 16);

        public static Sprite[] SlashFrames => Strip("FX/Slash", 26, 32, 5);
        public static Sprite[] SmokeFrames => Strip("FX/Smoke", 30, 14, 8);
        public static Sprite[] BossFrames => Strip("Actors/GiantSpiritIdle", 50, 50, 5, 0.5f, 0f);
        public static Sprite[] SlimeFrames => Strip("Actors/GiantSlimeIdle", 62, 52, 5, 0.5f, 0f);
        public static Sprite[] SquidIdleFrames => Strip("Actors/SquidIdle", 76, 79, 4, 0.5f, 0f);
        public static Sprite[] SquidShootFrames => Strip("Actors/SquidShoot", 76, 79, 5, 0.5f, 0f);
    }
}
