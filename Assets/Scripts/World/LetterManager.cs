using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 핵심 루프: 창구에서 편지 받기 → (밤이 바뀜) → 탐험 → 받는 사람에게 배달 → 플래그로 마을 변화.
    /// 한 번에 편지 한 통만 들고 다닌다.
    /// </summary>
    public static class LetterManager
    {
        const string Counter = "우체국 창구";

        public static LetterDef Carrying => GameData.GetLetter(GameState.CarryingLetterId);

        public static bool IsDelivered(LetterDef letter) => GameState.HasFlag("delivered:" + letter.id);

        public static bool IsRecipientRevealed(LetterDef letter) => GameState.Check(letter.revealFlag);

        public static string RecipientLabel(LetterDef letter) =>
            IsRecipientRevealed(letter) ? letter.recipientName : letter.recipientHint;

        public static LetterDef NextAvailable()
        {
            foreach (var letter in GameData.Letters)
                if (!IsDelivered(letter) && GameState.Check(letter.condition))
                    return letter;
            return null;
        }

        public static void HandleCounter(PlayerController player)
        {
            player.Health.HealFull();

            var carrying = Carrying;
            if (carrying != null)
            {
                DialogueSystem.I.Show(new[]
                {
                    new DialogueLine(Counter, "아직 배달하지 않은 편지가 있다: 「" + carrying.title + "」"),
                    new DialogueLine(Counter, "받는 사람: " + RecipientLabel(carrying)),
                    new DialogueLine("", "(잠시 쉬었다. 체력이 모두 회복되었다.)")
                });
                return;
            }

            var next = NextAvailable();
            if (next == null)
            {
                var ending = GameData.EndingLines;
                foreach (var e in GameData.Endings)
                    if (GameState.Check(e.condition))
                    {
                        ending = e.lines;
                        break;
                    }
                DialogueSystem.I.Show(ending);
                return;
            }

            GameState.AdvanceNight();
            GameState.SetCarrying(next.id);
            if (!string.IsNullOrEmpty(next.receiveItem) && GameState.ItemCount(next.receiveItem) == 0)
            {
                GameState.AddItem(next.receiveItem);
                var got = GameData.GetItem(next.receiveItem);
                if (got != null) HUD.Toast("획득: " + got.name + "   [I] 가방");
            }
            GameState.Save();

            var lines = new List<DialogueLine>();
            if (next.receiveLines != null) lines.AddRange(next.receiveLines);
            lines.Add(new DialogueLine("", Josa.Eul("「" + next.title + "」") + " 받았다. (체력 회복)"));
            DialogueSystem.I.Show(lines);
            Sound.Play("LetterGet");
        }

        /// <summary>recipientId 가 들고 있는 편지의 받는 사람이면 배달 흐름을 시작하고 true.</summary>
        public static bool TryDeliver(string recipientId)
        {
            var letter = Carrying;
            if (letter == null || letter.recipientId != recipientId) return false;

            if (!GameState.Check(letter.deliverCondition))
            {
                // 받는 사람을 만나긴 했다는 표시(과제 "~에게 찾아가기"에 쓴다).
                GameState.SetFlag("met:" + letter.id);
                DialogueSystem.I.Show(letter.blockedLines);
                return true;
            }

            var prompt = new List<DialogueLine>();
            if (letter.deliverPrompt != null) prompt.AddRange(letter.deliverPrompt);

            if (letter.choices != null && letter.choices.Length > 0)
            {
                prompt.Add(new DialogueLine("", "편지를 어떻게 전할까?"));
                var texts = new string[letter.choices.Length];
                for (int i = 0; i < texts.Length; i++) texts[i] = letter.choices[i].text;

                DialogueSystem.I.Show(prompt, texts, index =>
                {
                    var choice = letter.choices[index];
                    GameState.SetFlag(choice.setFlag);
                    DialogueSystem.I.Show(choice.lines, () => Complete(letter));
                });
            }
            else
            {
                DialogueSystem.I.Show(prompt, () => Complete(letter));
            }
            return true;
        }

        /// <summary>
        /// 개발용(F11): 편지가 없으면 창구에서 다음 편지를 받고, 들고 있으면 단서·보스를 건너뛰고
        /// 첫 번째 선택지로 바로 배달한다. 이야기 뒷부분을 빨리 확인할 때 쓴다.
        /// </summary>
        public static void DevSkip()
        {
            var letter = Carrying;
            if (letter == null)
            {
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (player != null) HandleCounter(player);
                return;
            }
            Satisfy(letter.revealFlag);
            Satisfy(letter.deliverCondition);
            if (letter.objectives != null)
                foreach (var o in letter.objectives) Satisfy(o.condition);
            if (letter.choices != null && letter.choices.Length > 0) GameState.SetFlag(letter.choices[0].setFlag);
            Complete(letter);
            HUD.Toast("[개발용] 「" + letter.title + "」 배달을 건너뛰었다.");
        }

        /// <summary>개발용: 조건의 긍정 항목을 모두 참으로 만든다(아이템은 얻고, 플래그는 세운다).</summary>
        static void Satisfy(string condition)
        {
            if (string.IsNullOrEmpty(condition)) return;
            foreach (var raw in condition.Split('|')[0].Split(','))
            {
                var term = raw.Trim();
                if (term.Length == 0 || term.StartsWith("!") || term.StartsWith("carrying:")) continue;
                if (term.StartsWith("has:") || term.StartsWith("got:")) GameState.AddItem(term.Substring(4));
                else GameState.SetFlag(term);
            }
        }

        static void Complete(LetterDef letter)
        {
            if (letter.consumeItems != null)
                foreach (var id in letter.consumeItems) GameState.RemoveItem(id);
            GameState.SetFlag("delivered:" + letter.id);
            GameState.SetFlag(letter.deliveredFlag);
            if (letter.rewardMaxHp > 0) GameState.AddMaxHp(letter.rewardMaxHp);
            if (!string.IsNullOrEmpty(letter.rewardItem))
            {
                var item = GameData.GetItem(letter.rewardItem);
                GameState.AddItem(letter.rewardItem, 1, item != null && item.maxStack > 0 ? item.maxStack : 99);
            }
            GameState.SetCarrying("");
            GameState.Save();

            var lines = new List<DialogueLine> { new DialogueLine("", "「" + letter.title + "」 배달 완료!") };
            if (!string.IsNullOrEmpty(letter.rewardText)) lines.Add(new DialogueLine("", letter.rewardText));
            DialogueSystem.I.Show(lines);
            HUD.Toast("배달 완료 — 마을로 돌아가 달라진 점을 확인해보자.");
            Sound.Play("Delivered");
        }
    }
}
