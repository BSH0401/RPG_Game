using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>캐릭터 시트: 열 = 방향(아래, 위, 왼쪽, 오른쪽), 행 = 프레임(0~3 걷기, 4 공격 …).</summary>
    public class CharacterSheet
    {
        public const int Down = 0, Up = 1, Left = 2, Right = 3;

        readonly Sprite[,] frames;
        public int Rows { get; }

        public CharacterSheet(Sprite[,] frames, int rows)
        {
            this.frames = frames;
            Rows = rows;
        }

        public Sprite Get(int dir, int row) => frames[Mathf.Clamp(dir, 0, 3), Mathf.Clamp(row, 0, Rows - 1)];

        /// <summary>걷기에 쓰는 행 수(최대 4).</summary>
        public int WalkFrames => Mathf.Min(4, Rows);
        public bool HasAttackRow => Rows > 4;
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

        public static CharacterSheet Sheet(string actor)
        {
            if (sheets.TryGetValue(actor, out var sheet)) return sheet;
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

        public static Texture2D Faceset(string actor) => Texture("Facesets/" + actor);

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
