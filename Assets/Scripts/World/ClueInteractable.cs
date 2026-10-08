using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>탐험 중 찾는 단서. 처음 조사하면 플래그를 세운다.</summary>
    public class ClueInteractable : Interactable
    {
        public string clueId;
        Vector3 baseScale;
        Transform visual;

        ClueDef Def => GameData.GetClue(clueId);
        bool Found => Def != null && GameState.HasFlag(Def.setFlag);

        public override string DisplayName => Def != null ? Def.displayName : clueId;
        public override string Prompt => DisplayName + " 조사하기";

        public void SetVisual(Transform t)
        {
            visual = t;
            baseScale = t.localScale;
        }

        void Update()
        {
            // 아직 찾지 않은 단서는 살짝 반짝여서 눈에 띄게 한다.
            if (visual == null) return;
            float pulse = Found ? 1f : 1f + 0.15f * Mathf.Sin(Time.time * 5f);
            visual.localScale = baseScale * pulse;
        }

        public override void Interact(PlayerController player)
        {
            var def = Def;
            if (def == null) return;
            if (Found)
            {
                DialogueSystem.I.Show(def.revisitLines != null && def.revisitLines.Length > 0 ? def.revisitLines : def.lines);
                return;
            }
            DialogueSystem.I.Show(def.lines, () =>
            {
                GameState.SetFlag(def.setFlag);
                GameState.Save();
                HUD.Toast("단서를 찾았다: " + def.displayName + "  [Tab] 지도 확인");
            });
        }
    }
}
