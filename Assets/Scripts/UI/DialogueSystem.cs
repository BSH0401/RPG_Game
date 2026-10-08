using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 대화창과 선택지. 대화가 열려 있는 동안 플레이어와 적은 멈춘다.
    /// E / Space / Enter / 클릭으로 넘기고, 선택지는 숫자키(1~3) 또는 클릭으로 고른다.
    /// </summary>
    public class DialogueSystem : MonoBehaviour
    {
        public static DialogueSystem I { get; private set; }
        public static bool IsOpen => I != null && I.open;
        /// <summary>대화가 닫힌 프레임. 같은 키 입력이 다른 행동(재대화, 회피)으로 이어지지 않게 막는 데 쓴다.</summary>
        public static int LastClosedFrame { get; private set; } = -1;

        readonly List<DialogueLine> lines = new List<DialogueLine>();
        string[] choices;
        Action<int> onChoice;
        Action onDone;
        int index;
        bool open;
        int openedFrame = -1;

        void Awake() => I = this;

        public void Show(IList<DialogueLine> newLines, Action done = null) => Show(newLines, null, null, done);

        public void Show(IList<DialogueLine> newLines, string[] choiceTexts, Action<int> chosen, Action done = null)
        {
            lines.Clear();
            if (newLines != null)
                foreach (var l in newLines)
                    if (l != null) lines.Add(l);

            bool hasChoices = choiceTexts != null && choiceTexts.Length > 0;
            if (lines.Count == 0 && !hasChoices)
            {
                done?.Invoke();
                return;
            }

            choices = hasChoices ? choiceTexts : null;
            onChoice = chosen;
            onDone = done;
            index = 0;
            open = true;
            openedFrame = Time.frameCount;
        }

        bool AtChoices => choices != null && index >= lines.Count - 1;

        void Update()
        {
            if (!open || Time.frameCount == openedFrame) return;

            if (AtChoices)
            {
                for (int i = 0; i < choices.Length; i++)
                {
                    if (GameInput.ChoicePressed(i))
                    {
                        Choose(i);
                        return;
                    }
                }
                return;
            }

            if (GameInput.AdvancePressed)
            {
                Sound.Play("Talk", 0.35f);
                index++;
                if (index >= lines.Count) Close();
            }
        }

        void Choose(int i)
        {
            var callback = onChoice;
            onDone = null;
            Close();
            callback?.Invoke(i);
        }

        void Close()
        {
            open = false;
            LastClosedFrame = Time.frameCount;
            var done = onDone;
            onDone = null;
            onChoice = null;
            choices = null;
            done?.Invoke();
        }

        void OnGUI()
        {
            if (!open) return;
            Ui.Ensure();
            GUI.depth = -10;

            float s = Ui.S;
            float w = Mathf.Min(Screen.width - 40f * s, 900f * s);
            float h = 170f * s;
            var box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 20f * s, w, h);
            Ui.Panel(box);

            if (lines.Count > 0)
            {
                var line = lines[Mathf.Clamp(index, 0, lines.Count - 1)];
                float pad = 24f * s;
                if (!string.IsNullOrEmpty(line.speaker))
                {
                    // 말하는 사람 이름표
                    var tag = new Rect(box.x + pad, box.y - 22f * s, Mathf.Max(120f * s, line.speaker.Length * 22f * s + 30f * s), 40f * s);
                    Ui.Panel(tag);
                    GUI.Label(new Rect(tag.x + 14f * s, tag.y + 6f * s, tag.width - 20f * s, 30f * s), line.speaker, Ui.Title);
                }
                float textX = box.x + pad;
                var portrait = GameAssets.PortraitFor(line.speaker);
                if (portrait != null)
                {
                    // 말하는 사람 얼굴 (38x38 픽셀 그림을 3배로)
                    float size = 114f * s;
                    var frame = new Rect(box.x + pad, box.y + (box.height - size) * 0.5f + 6f * s, size, size);
                    Ui.Fill(new Rect(frame.x - 3f * s, frame.y - 3f * s, frame.width + 6f * s, frame.height + 6f * s), new Color(0.78f, 0.66f, 0.42f, 0.9f));
                    GUI.DrawTexture(frame, portrait);
                    textX = frame.xMax + 20f * s;
                }
                GUI.Label(new Rect(textX, box.y + 32f * s, box.xMax - pad - textX, box.height - 44f * s), line.text, Ui.Text);
            }

            if (AtChoices)
            {
                float bh = 44f * s;
                float gap = 8f * s;
                float top = box.y - (bh + gap) * choices.Length - 30f * s;
                for (int i = 0; i < choices.Length; i++)
                {
                    var r = new Rect(box.x + w * 0.15f, top + i * (bh + gap), w * 0.7f, bh);
                    if (GUI.Button(r, (i + 1) + ". " + choices[i], Ui.Button))
                    {
                        Choose(i);
                        return;
                    }
                }
            }
            else
            {
                GUI.Label(new Rect(box.xMax - 150f * s, box.yMax - 34f * s, 140f * s, 30f * s), "E / Space >>", Ui.Small);
            }
        }
    }
}
