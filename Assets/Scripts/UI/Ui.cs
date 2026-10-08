using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 프로토타입용 IMGUI 스타일. 해상도 720p 기준으로 크기를 맞춘다.
    /// 한글이 깨지면 Assets/Resources/Fonts/UIFont.ttf 에 한글 폰트를 넣으면 자동으로 사용한다.
    /// </summary>
    public static class Ui
    {
        public static GUIStyle Text, Title, Small, Center, Button;

        static float builtScale = -1f;
        static Font font;
        static bool fontLoaded;

        public static float S => Mathf.Max(0.6f, Screen.height / 720f);

        /// <summary>OnGUI 안에서만 호출할 것.</summary>
        public static void Ensure()
        {
            if (Text != null && Mathf.Approximately(builtScale, S)) return;
            builtScale = S;
            if (!fontLoaded)
            {
                font = Resources.Load<Font>("Fonts/UIFont");
                fontLoaded = true;
            }

            Text = Make(GUI.skin.label, 20, Color.white);
            Text.wordWrap = true;
            Title = Make(GUI.skin.label, 22, new Color(1f, 0.86f, 0.5f));
            Title.fontStyle = FontStyle.Bold;
            Small = Make(GUI.skin.label, 16, new Color(0.85f, 0.88f, 1f));
            Small.wordWrap = true;
            Center = Make(GUI.skin.label, 20, Color.white);
            Center.alignment = TextAnchor.MiddleCenter;
            Center.wordWrap = true;
            Button = Make(GUI.skin.button, 19, Color.white);
            Button.alignment = TextAnchor.MiddleLeft;
            Button.padding = new RectOffset(Px(14), Px(14), Px(6), Px(6));
        }

        static GUIStyle Make(GUIStyle source, int size, Color color)
        {
            var style = new GUIStyle(source) { fontSize = Px(size), richText = true };
            if (font != null) style.font = font;
            style.normal.textColor = color;
            style.hover.textColor = color;
            return style;
        }

        public static int Px(float v) => Mathf.RoundToInt(v * S);

        public static Rect R(float x, float y, float w, float h) => new Rect(x * S, y * S, w * S, h * S);

        public static void Fill(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        public static void Panel(Rect r) => Fill(r, new Color(0.04f, 0.05f, 0.12f, 0.85f));

        public static void Shadow(Rect r, string text, GUIStyle style)
        {
            var old = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
            style.normal.textColor = old;
            GUI.Label(r, text, style);
        }
    }
}
