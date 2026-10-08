using System.Collections.Generic;

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

        public static bool IsRecipientRevealed(LetterDef letter) =>
            string.IsNullOrEmpty(letter.revealFlag) || GameState.HasFlag(letter.revealFlag);

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
                DialogueSystem.I.Show(GameData.EndingLines);
                return;
            }

            GameState.AdvanceNight();
            GameState.SetCarrying(next.id);
            GameState.Save();

            var lines = new List<DialogueLine>();
            if (next.receiveLines != null) lines.AddRange(next.receiveLines);
            lines.Add(new DialogueLine("", "「" + next.title + "」을(를) 받았다. (체력 회복)"));
            DialogueSystem.I.Show(lines);
            HUD.Toast(GameState.Night + "번째 밤 — 동쪽 숲의 길이 바뀌었다.");
        }

        /// <summary>recipientId 가 들고 있는 편지의 받는 사람이면 배달 흐름을 시작하고 true.</summary>
        public static bool TryDeliver(string recipientId)
        {
            var letter = Carrying;
            if (letter == null || letter.recipientId != recipientId) return false;

            if (!GameState.Check(letter.deliverCondition))
            {
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

        static void Complete(LetterDef letter)
        {
            GameState.SetFlag("delivered:" + letter.id);
            GameState.SetFlag(letter.deliveredFlag);
            if (letter.rewardMaxHp > 0) GameState.AddMaxHp(letter.rewardMaxHp);
            GameState.SetCarrying("");
            GameState.Save();

            var lines = new List<DialogueLine> { new DialogueLine("", "「" + letter.title + "」 배달 완료!") };
            if (!string.IsNullOrEmpty(letter.rewardText)) lines.Add(new DialogueLine("", letter.rewardText));
            DialogueSystem.I.Show(lines);
            HUD.Toast("배달 완료 — 마을로 돌아가 달라진 점을 확인해보자.");
        }
    }
}
