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
        public string rewardText;
    }

    [Serializable]
    public class NpcTalk
    {
        public string condition;
        public DialogueLine[] lines;
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
#pragma warning restore 0649

    /// <summary>JSON 데이터를 읽어 보관한다. 스토리 문장은 코드가 아니라 JSON 에서 고친다.</summary>
    public static class GameData
    {
        public static LetterDef[] Letters { get; private set; } = new LetterDef[0];
        public static DialogueLine[] EndingLines { get; private set; } = new DialogueLine[0];

        static readonly Dictionary<string, NpcDef> npcs = new Dictionary<string, NpcDef>();
        static readonly Dictionary<string, ClueDef> clues = new Dictionary<string, ClueDef>();

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
