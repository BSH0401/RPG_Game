using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>주민. 들고 있는 편지의 받는 사람이면 배달, 아니면 상황에 맞는 대사를 한다.</summary>
    public class NpcInteractable : Interactable
    {
        public string npcId;

        NpcDef Def => GameData.GetNpc(npcId);

        public override string DisplayName => Def != null ? Def.displayName : npcId;

        public bool IsRecipientOfCarriedLetter
        {
            get
            {
                var letter = LetterManager.Carrying;
                return letter != null && letter.recipientId == npcId;
            }
        }

        public override string Prompt => IsRecipientOfCarriedLetter ? DisplayName + "에게 편지 전하기" : Josa.Gwa(DisplayName) + " 대화";

        public override void Interact(PlayerController player)
        {
            var anim = GetComponent<SpriteAnimator>();
            if (anim != null) anim.FaceToward(player.transform.position);
            if (LetterManager.TryDeliver(npcId)) return;
            if (RequestManager.TryHandle(npcId)) return;

            var def = Def;
            if (def?.talks != null)
            {
                foreach (var talk in def.talks)
                {
                    if (GameState.Check(talk.condition))
                    {
                        var give = talk.giveItem;
                        int count = talk.giveCount > 0 ? talk.giveCount : 1;
                        var flag = talk.setFlag;
                        var toast = talk.toast;
                        DialogueSystem.I.Show(talk.lines, () =>
                        {
                            if (!string.IsNullOrEmpty(flag))
                            {
                                GameState.SetFlag(flag);
                                GameState.Save();
                                if (!string.IsNullOrEmpty(toast)) HUD.Toast(toast);
                            }
                            if (!string.IsNullOrEmpty(give)) Inventory.Give(give, count);
                        });
                        return;
                    }
                }
            }
            DialogueSystem.I.Show(new[] { new DialogueLine(DisplayName, "...") });
        }

        public static NpcInteractable Find(string id)
        {
            foreach (var it in All)
                if (it is NpcInteractable npc && npc.npcId == id) return npc;
            return null;
        }
    }
}
