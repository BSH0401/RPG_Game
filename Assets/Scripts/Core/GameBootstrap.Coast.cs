using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 서쪽 해안과 등대 (x -50 ~ -17).
    /// 우체국 등불(lamp_lit)이 켜지면 마을 서쪽의 무너진 바위가 치워지고 해안길이 열린다.
    /// 네 번째 편지를 들고 가면 등대 앞 바다에서 두 번째 보스 「심해 먹물 문어」가 나타난다.
    /// </summary>
    public partial class GameBootstrap
    {
        public static readonly Vector2 LighthousePos = new Vector2(-43.5f, 5f);

        void BuildCoast()
        {
            var visuals = WorldVisuals.I;

            // 마을 서쪽 입구를 막은 바위 (우체국 등불이 켜지면 치워진다)
            visuals.Register(Group("CoastGate_Rocks", () =>
            {
                Spawner.Collider("CoastGate", new Vector2(-16.5f, 0f), new Vector2(1.2f, 4f));
                var rock = RockSprite(true);
                for (int k = 0; k < 2; k++) Spawner.Prop("GateRock", new Vector2(-16.5f, -2f + k * 2f), rock);
                var talk = Spawner.Prop("GateSign", new Vector2(-15.2f, 1.6f), useAssets ? GameAssets.Tile("TilesetNature", 5, 8) : Art.Signpost);
                talk.AddComponent<NpcInteractable>().npcId = "coast_rocks";
            }), "!lamp_lit");

            // 등대
            Spawner.Building("Lighthouse", LighthousePos + new Vector2(0f, 0.8f), new Vector2(2.4f, 1.6f), Art.Lighthouse(false));
            visuals.Register(Group("Lighthouse_Lit", () =>
            {
                var lit = Spawner.Prop("LighthouseLit", LighthousePos, Art.Lighthouse(true));
                lit.GetComponent<YSort>().offset = 1;
                Spawner.Glow(LighthousePos + new Vector2(0f, 6f), 3f, new Color(1f, 0.95f, 0.7f, 0.9f));
                var beam = Spawner.Glow(LighthousePos + new Vector2(0f, 6f), 30f, new Color(1f, 0.92f, 0.6f, 0.18f));
                beam.gameObject.AddComponent<LighthouseBeam>();
            }), "lighthouse_lit");
            visuals.Register(Spawner.Glow(LighthousePos + new Vector2(0f, 6f), 2f, new Color(0.6f, 0.3f, 0.9f, 0.5f)).gameObject, "!lighthouse_lit");

            // 등대지기 세린: 등대 앞에 있다가, 불을 켠 뒤에는 마을(노아 곁)로 온다.
            // 폭풍 경보(편지 7)를 들고 있는 밤에는 등대 불을 지키러 다시 등대에 가 있다.
            visuals.Register(Spawner.Npc("keeper", LighthousePos + new Vector2(1.8f, -0.9f), Art.Mira, 1f, "Princess").gameObject, "!lighthouse_lit|carrying:storm_warning");
            visuals.Register(Spawner.Npc("keeper", new Vector2(-2.6f, 4.3f), Art.Mira, 1f, "Princess").gameObject, "lighthouse_lit,!carrying:storm_warning");

            // 바위와 유목 (충돌 있음). 위쪽 가장자리는 절벽 바위 줄.
            var big = RockSprite(true);
            var small = RockSprite(false);
            for (float x = -45f; x < -18f; x += 3.1f)
                Spawner.Prop("Cliff", new Vector2(x, 9.4f + Mathf.Repeat(x, 2f) * 0.4f), big, new Vector2(3.2f, 0.9f), new Vector2(0f, 0.45f));
            foreach (var p in new[]
                     {
                         new Vector2(-30f, 3.2f), new Vector2(-24f, -3.4f), new Vector2(-36.5f, -2.5f), new Vector2(-21f, 5.5f),
                         new Vector2(-33f, 7f), new Vector2(-27f, -0.2f)
                     })
                Spawner.Prop("Rock", p, small, new Vector2(1f, 0.5f), new Vector2(0f, 0.25f));
            if (useAssets)
            {
                var driftwood = GameAssets.Tile("TilesetNature", 4, 0, 2, 2);
                Spawner.Tree(new Vector2(-38.5f, 7.6f), driftwood, 0.4f);
                Spawner.Tree(new Vector2(-22.5f, 8.2f), driftwood, 0.4f);
            }

            // 해안 바다의 달빛 반짝임
            var amb = new GameObject("CoastAmbience").transform;
            amb.SetParent(worldRoot.transform, false);
            for (int i = 0; i < 18; i++)
            {
                float x = Mathf.Lerp(-49f, -18f, PixelCanvas.Hash(i, 7, 700));
                float y = Mathf.Lerp(-10.6f, -6.8f, PixelCanvas.Hash(i, 8, 700));
                Firefly.Spawn(amb, new Vector2(x, y), new Color(0.75f, 0.85f, 1f), 0.25f, 0.8f);
            }
            Spawner.Glow(new Vector2(-33f, -8.6f), 18f, new Color(0.45f, 0.55f, 1f, 0.1f));
        }

        Sprite RockSprite(bool large)
        {
            if (useAssets)
            {
                // 바위는 16픽셀 칸에 딱 맞지 않아 픽셀 영역으로 자른다(큰 것 = 회색 바위 무더기, 작은 것 = 갈색 바위).
                var s = large ? GameAssets.Region("Tilesets/TilesetNature", 257, 83, 61, 44)
                              : GameAssets.Region("Tilesets/TilesetNature", 210, 132, 30, 28);
                if (s != null) return s;
            }
            return Art.Rock(large);
        }
    }

    /// <summary>등대 불빛이 천천히 숨 쉬듯 밝아졌다 어두워진다.</summary>
    public class LighthouseBeam : MonoBehaviour
    {
        SpriteRenderer sr;
        Color baseColor;

        void Start()
        {
            sr = GetComponent<SpriteRenderer>();
            baseColor = sr.color;
        }

        void Update()
        {
            float k = 0.6f + 0.4f * Mathf.Sin(Time.time * 1.2f);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * k);
        }
    }
}
