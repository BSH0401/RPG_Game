using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 편지 5~12통째에 필요한 주민·단서·마을 변화.
    ///   도라(옛 우체국장): 마을 서남쪽 불 꺼진 집 앞. 첫 편지를 받으면 집에 불이 켜진다.
    ///   테오(유리병 줍는 사람): 서쪽 해안 북쪽의 유목 곁. 등대에 불이 켜진 뒤 떠밀려 온 편지를 모은다.
    ///   마지막 편지(festival)를 배달하면 광장에 축제 등불이 걸리고 주민들이 우체국 앞에 모인다.
    /// 단서는 그 편지를 받을 무렵에만 나타나서, 이야기보다 먼저 발견되지 않게 한다.
    /// </summary>
    public partial class GameBootstrap
    {
        void BuildStory()
        {
            var visuals = WorldVisuals.I;

            // 도라: 처음엔 불 꺼진 집 앞, 축제 날엔 우체국 앞
            var doraTint = GameAssets.DoraTint;
            visuals.Register(Spawner.Npc("dora", new Vector2(-8.6f, -6.2f), Art.Dora, 1f, "Woman", doraTint).gameObject, "!festival");
            visuals.Register(Spawner.Npc("dora", new Vector2(4.4f, 3.4f), Art.Dora, 1f, "Woman", doraTint).gameObject, "festival");
            visuals.Register(Group("DoraHouse_Lit", () =>
            {
                Spawner.Glow(new Vector2(-11f, -4.6f), 3.5f, new Color(1f, 0.8f, 0.45f, 0.3f));
                Spawner.Lamp(new Vector2(-8.4f, -4.4f), true);
            }), "dora_home");

            // 테오: 해안 입구 가까운 유목 곁의 모닥불(적이 나오는 곳과 떨어져 있다), 축제 날엔 마을로
            var teoTint = GameAssets.TeoTint;
            visuals.Register(Group("Teo_Camp", () =>
            {
                Spawner.Npc("teo", new Vector2(-19.2f, 7.3f), Art.Teo, 1f, "Hunter", teoTint);
                Spawner.Glow(new Vector2(-20.2f, 7.5f), 4f, new Color(1f, 0.6f, 0.3f, 0.3f));
            }), "!festival");
            visuals.Register(Spawner.Npc("teo", new Vector2(-8.4f, -2.4f), Art.Teo, 1f, "Hunter", teoTint).gameObject, "festival");

            // 단서
            Sprite sign = useAssets ? GameAssets.Tile("TilesetNature", 5, 8) : Art.Signpost;
            visuals.Register(Clue("duty_roster", new Vector2(-4.6f, 6.2f), sign).gameObject, "delivered:owen_reply");
            // 유리병 무더기: 병 하나는 작아서 잘 안 보이므로 몇 개를 늘어놓고 달빛을 비춘다.
            visuals.Register(Group("BottlePile", () =>
            {
                var bottle = ItemOr("FireflyJar", Art.FlourSack);
                Clue("bottle_pile", new Vector2(-40.5f, -4.6f), bottle);
                foreach (var p in new[] { new Vector2(-41.3f, -4.9f), new Vector2(-39.7f, -4.4f), new Vector2(-38.9f, -4.9f) })
                    Spawner.Prop("Bottle", p, bottle);
                Spawner.Glow(new Vector2(-40.2f, -4.2f), 4f, new Color(0.65f, 0.85f, 1f, 0.3f));
            }), "delivered:lighthouse_letter");
            visuals.Register(Clue("unsent_drafts", new Vector2(45.4f, -6.4f), ItemOr("PaperLetter", Art.Signpost)).gameObject, "delivered:storm_warning");

            // 축제: 광장과 길가에 등불이 걸린다.
            visuals.Register(Group("Festival_Lights", () =>
            {
                foreach (var p in new[]
                         {
                             new Vector2(-6f, -1.9f), new Vector2(6.5f, -1.9f), new Vector2(-12f, 1.7f), new Vector2(12f, 1.7f),
                             new Vector2(-2.4f, -5f), new Vector2(2.4f, -5f)
                         })
                    Spawner.Lamp(p, true);
                for (int i = 0; i < 9; i++)
                {
                    float a = i / 9f * Mathf.PI * 2f;
                    var c = Color.HSVToRGB(i / 9f, 0.45f, 1f);
                    c.a = 0.35f;
                    Spawner.Glow(new Vector2(0f, 3.4f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3.2f, 1.6f, c);
                }
                Spawner.Glow(new Vector2(0f, 3.4f), 10f, new Color(1f, 0.85f, 0.55f, 0.15f));
            }), "festival");
        }

        /// <summary>에셋의 아이템 그림을 월드 소품으로, 없으면 대체 그림.</summary>
        Sprite ItemOr(string icon, Sprite fallback)
        {
            var s = useAssets ? GameAssets.ItemSprite(icon) : null;
            return s != null ? s : fallback;
        }
    }
}
