using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>우체국 창구: 편지 받기, 체력 회복.</summary>
    public class PostOfficeCounter : Interactable
    {
        public override string DisplayName => "우체국 창구";
        public override string Prompt => "우체국 창구 (편지 받기 · 휴식)";
        public override void Interact(PlayerController player) => LetterManager.HandleCounter(player);
    }
}
