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
            // 한글 폰트(Noto Sans KR)는 줄 높이가 커서, 한 줄 상자에서 글자 아랫부분이 잘리지 않게 넘쳐 그리게 한다.
            var style = new GUIStyle(source) { fontSize = Px(size), richText = true, clipping = TextClipping.Overflow };
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

        static readonly Color PanelBorder = new Color(0.78f, 0.66f, 0.42f, 0.9f);

        /// <summary>테두리가 있는 어두운 패널(편지지 느낌의 금색 테두리).</summary>
        public static void Panel(Rect r)
        {
            float b = Mathf.Max(2f, 2f * S);
            Fill(new Rect(r.x - b, r.y - b, r.width + b * 2, r.height + b * 2), new Color(0.01f, 0.01f, 0.04f, 0.9f));
            Fill(r, PanelBorder);
            Fill(new Rect(r.x + b, r.y + b, r.width - b * 2, r.height - b * 2), new Color(0.06f, 0.07f, 0.15f, 0.94f));
            Fill(new Rect(r.x + b, r.y + b, r.width - b * 2, b), new Color(1f, 1f, 1f, 0.06f));
        }

        public static void Icon(Rect r, Texture2D tex)
        {
            if (tex != null) GUI.DrawTexture(r, tex);
        }

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
