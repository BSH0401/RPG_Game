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

    /// <summary>편지 하나의 탐험 과제. condition 이 참이면 완료로 표시된다.</summary>
    [Serializable]
    public class LetterObjective
    {
        public string text;
        public string condition;
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
        /// <summary>이 조건이 참이 되면 받는 사람과 지도 위치가 공개된다(예: "clue:a,clue:b"). 비어 있으면 처음부터 공개.</summary>
        public string revealFlag;
        /// <summary>우체국 창구에서 이 편지를 받을 수 있는 조건.</summary>
        public string condition;
        /// <summary>배달할 수 있는 조건(예: 보스 처치). 만족하지 않으면 blockedLines 를 보여준다.</summary>
        public string deliverCondition;
        public DialogueLine[] blockedLines;
        public DialogueLine[] receiveLines;
        /// <summary>창구에서 이 편지를 받을 때 함께 얻는 아이템 id.</summary>
        public string receiveItem;
        public DialogueLine[] deliverPrompt;
        public DeliveryChoice[] choices;
        public string deliveredFlag;
        /// <summary>오른쪽 위 편지 칸에 보이는 탐험 과제 목록.</summary>
        public LetterObjective[] objectives;
        /// <summary>배달할 때 건네고 없어지는 아이템(찾아 온 밀가루 자루, 램프 기름 등).</summary>
        public string[] consumeItems;
        public int rewardMaxHp;
        /// <summary>배달 보상 아이템 id (items.json).</summary>
        public string rewardItem;
        public string rewardText;
    }

    /// <summary>주민의 의뢰(곁가지 일거리). RequestManager 참고.</summary>
    [Serializable]
    public class RequestDef
    {
        public string id;
        public string title;
        /// <summary>의뢰를 주는 주민 id.</summary>
        public string giver;
        /// <summary>끝낸 의뢰를 받는 주민 id(비어 있으면 giver).</summary>
        public string turnIn;
        /// <summary>이 의뢰를 받을 수 있는 조건.</summary>
        public string condition;
        public LetterObjective[] objectives;
        /// <summary>이 조건이 참이면 turnIn 에게 말을 걸어 끝낼 수 있다.</summary>
        public string completeCondition;
        public DialogueLine[] acceptLines;
        public DialogueLine[] completeLines;
        /// <summary>의뢰를 받을 때 건네받는 물건(전해 줄 병 편지 등).</summary>
        public string giveItem;
        public string[] consumeItems;
        public string rewardItem;
        public int rewardCount;
        public string rewardText;
        /// <summary>끝냈을 때 세우는 플래그(대사·풍경 변화용).</summary>
        public string setFlag;
    }

    [Serializable]
    public class ItemDef
    {
        public string id;
        public string name;
        public string description;
        /// <summary>equipment(가지고 있으면 항상 효과), consumable(R 키로 사용), key(열쇠·이야기 물건),
        /// part(편지 도구 부품: Q 의 효과를 바꾼다. C 키로 교체. value = 재사용 대기 초)</summary>
        public string kind;
        /// <summary>maxHp, dodge(회피 %), tool(봉인끈 %), light(시야), heal(회복)</summary>
        public string effect;
        public float value;
        /// <summary>Resources/Art/NinjaAdventure/Items 안의 그림 이름.</summary>
        public string icon;
        /// <summary>"legendary" 면 전설 장비(금색 이름, 황금 그림자가 떨어뜨림).</summary>
        public string rarity;
        public int maxStack;
        /// <summary>이 아이템을 rewardAt[i] 개 모으면 rewardItems[i] 를 준다(잃어버린 우표 등).</summary>
        public int[] rewardAt;
        public string[] rewardItems;

        public bool IsEquipment => kind == "equipment";
        public bool IsConsumable => kind == "consumable";
        public bool IsPart => kind == "part";
        public bool IsMaterial => kind == "material";
    }

    [Serializable]
    public class NpcTalk
    {
        public string condition;
        public DialogueLine[] lines;
        /// <summary>이 대사가 끝나면 주는 아이템. 한 번만 주려면 condition 에 "!got:아이템id" 를 넣는다.</summary>
        public string giveItem;
        public int giveCount;
        /// <summary>이 대사가 끝나면 세우는 플래그(예: 서명 받기). 한 번만 하려면 condition 에 "!플래그" 를 넣는다.</summary>
        public string setFlag;
        /// <summary>setFlag 를 세울 때 띄우는 알림.</summary>
        public string toast;
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
    /// <summary>모든 편지를 배달한 뒤의 엔딩. 위에서부터 조건이 맞는 첫 엔딩을 보여준다.</summary>
    [Serializable]
    public class EndingDef
    {
        public string condition;
        public DialogueLine[] lines;
    }

    [Serializable]
    class LetterDatabase
    {
        public LetterDef[] letters;
        public DialogueLine[] endingLines;
        public EndingDef[] endings;
    }

    [Serializable]
    class RequestDatabase
    {
        public RequestDef[] requests;
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
        public static EndingDef[] Endings { get; private set; } = new EndingDef[0];

        static readonly Dictionary<string, NpcDef> npcs = new Dictionary<string, NpcDef>();
        static readonly Dictionary<string, ClueDef> clues = new Dictionary<string, ClueDef>();
        static readonly Dictionary<string, ItemDef> items = new Dictionary<string, ItemDef>();
        public static ItemDef[] Items { get; private set; } = new ItemDef[0];
        public static RequestDef[] Requests { get; private set; } = new RequestDef[0];

        public static void Load()
        {
            var letterDb = Read<LetterDatabase>("Data/letters");
            Letters = letterDb?.letters ?? new LetterDef[0];
            EndingLines = letterDb?.endingLines ?? new DialogueLine[0];
            Endings = letterDb?.endings ?? new EndingDef[0];

            npcs.Clear();
            var npcDb = Read<NpcDatabase>("Data/npcs");
            if (npcDb?.npcs != null)
                foreach (var n in npcDb.npcs) npcs[n.id] = n;

            clues.Clear();
            var clueDb = Read<ClueDatabase>("Data/clues");
            if (clueDb?.clues != null)
                foreach (var c in clueDb.clues) clues[c.id] = c;

            Requests = Read<RequestDatabase>("Data/requests")?.requests ?? new RequestDef[0];

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
