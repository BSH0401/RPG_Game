using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    // ---- 데이터 정의: Assets/Resources/Data/*.json 과 필드 이름이 일치해야 한다. ----

    [Serializable]
    public class DialogueLine
    {
        public string speaker;
        public string text;

        public DialogueLine() { }

        public DialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }
    }

    [Serializable]
    public class DeliveryChoice
    {
        public string text;
        public string setFlag;
        public DialogueLine[] lines;
    }

    [Serializable]
    public class LetterDef
    {
        public string id;
        public string title;
        public string senderName;
        public string recipientId;
        public string recipientName;
        /// <summary>받는 사람이 밝혀지기 전에 보이는 설명.</summary>
        public string recipientHint;
        /// <summary>이 플래그를 얻으면 받는 사람과 지도 위치가 공개된다. 비어 있으면 처음부터 공개.</summary>
        public string revealFlag;
        /// <summary>우체국 창구에서 이 편지를 받을 수 있는 조건.</summary>
        public string condition;
        /// <summary>배달할 수 있는 조건(예: 보스 처치). 만족하지 않으면 blockedLines 를 보여준다.</summary>
        public string deliverCondition;
        public DialogueLine[] blockedLines;
        public DialogueLine[] receiveLines;
        public DialogueLine[] deliverPrompt;
        public DeliveryChoice[] choices;
        public string deliveredFlag;
        public int rewardMaxHp;
        /// <summary>배달 보상 아이템 id (items.json).</summary>
        public string rewardItem;
        public string rewardText;
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string description;
        /// <summary>equipment(가지고 있으면 항상 효과), consumable(R 키로 사용), key(열쇠·이야기 물건)</summary>
        public string kind;
        /// <summary>maxHp, dodge(회피 %), tool(봉인끈 %), light(시야), heal(회복)</summary>
        public string effect;
        public float value;
        /// <summary>Resources/Art/NinjaAdventure/Items 안의 그림 이름.</summary>
        public string icon;
        public int maxStack;

        public bool IsEquipment => kind == "equipment";
        public bool IsConsumable => kind == "consumable";
    }

    [Serializable]
    public class NpcTalk
    {
        public string condition;
        public DialogueLine[] lines;
        /// <summary>이 대사가 끝나면 주는 아이템. 한 번만 주려면 condition 에 "!got:아이템id" 를 넣는다.</summary>
        public string giveItem;
        public int giveCount;
    }

    [Serializable]
    public class NpcDef
    {
        public string id;
        public string displayName;
        /// <summary>위에서부터 조건이 맞는 첫 대사를 사용한다.</summary>
        public NpcTalk[] talks;
    }

    [Serializable]
    public class ClueDef
    {
        public string id;
        public string displayName;
        public string setFlag;
        public DialogueLine[] lines;
        public DialogueLine[] revisitLines;
    }

#pragma warning disable 0649 // JsonUtility 가 채우는 필드
    [Serializable]
    class LetterDatabase
    {
        public LetterDef[] letters;
        public DialogueLine[] endingLines;
    }

    [Serializable]
    class NpcDatabase
    {
        public NpcDef[] npcs;
    }

    [Serializable]
    class ClueDatabase
    {
        public ClueDef[] clues;
    }

    [Serializable]
    class ItemDatabase
    {
        public ItemDef[] items;
    }
#pragma warning restore 0649

    /// <summary>JSON 데이터를 읽어 보관한다. 스토리 문장은 코드가 아니라 JSON 에서 고친다.</summary>
    public static class GameData
    {
        public static LetterDef[] Letters { get; private set; } = new LetterDef[0];
        public static DialogueLine[] EndingLines { get; private set; } = new DialogueLine[0];

        static readonly Dictionary<string, NpcDef> npcs = new Dictionary<string, NpcDef>();
        static readonly Dictionary<string, ClueDef> clues = new Dictionary<string, ClueDef>();
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        public static ItemDef[] Items { get; private set; } = new ItemDef[0];

        public static void Load()
        {
            var letterDb = Read<LetterDatabase>("Data/letters");
            Letters = letterDb?.letters ?? new LetterDef[0];
            EndingLines = letterDb?.endingLines ?? new DialogueLine[0];

            npcs.Clear();
            var npcDb = Read<NpcDatabase>("Data/npcs");
            if (npcDb?.npcs != null)
                foreach (var n in npcDb.npcs) npcs[n.id] = n;

            clues.Clear();
            var clueDb = Read<ClueDatabase>("Data/clues");
            if (clueDb?.clues != null)
                foreach (var c in clueDb.clues) clues[c.id] = c;

            items.Clear();
            var itemDb = Read<ItemDatabase>("Data/items");
            Items = itemDb?.items ?? new ItemDef[0];
            foreach (var it in Items) items[it.id] = it;
        }

        public static LetterDef GetLetter(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var l in Letters)
                if (l.id == id) return l;
            return null;
        }

        public static NpcDef GetNpc(string id) => id != null && npcs.TryGetValue(id, out var n) ? n : null;

        public static ClueDef GetClue(string id) => id != null && clues.TryGetValue(id, out var c) ? c : null;

        public static ItemDef GetItem(string id) => id != null && items.TryGetValue(id, out var it) ? it : null;

        static T Read<T>(string resourcePath) where T : class
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            if (asset == null)
            {
                Debug.LogError("[달빛 우체국] 데이터 파일이 없습니다: Resources/" + resourcePath + ".json");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError("[달빛 우체국] JSON 형식 오류 (" + resourcePath + "): " + e.Message);
                return null;
            }
        }
    }
}
