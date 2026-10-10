using System.Collections.Generic;

namespace MoonlightPost
{
    /// <summary>
    /// 주민의 의뢰(곁가지 일거리). 편지와 달리 여러 개를 동시에 맡을 수 있고, 안 해도 이야기는 끝까지 간다.
    ///   받을 수 있음: condition 이 참이고 아직 받지 않음 → giver 에게 말을 걸면 받는다("req:id").
    ///   진행 중: objectives 가 오른쪽 위 의뢰 칸에 보인다.
    ///   완료: completeCondition 이 참일 때 turnIn(없으면 giver)에게 말을 걸면 끝난다("reqdone:id").
    /// 정의는 Resources/Data/requests.json, 필요한 물건·사건의 배치는 GameBootstrap.Tasks.cs.
    /// </summary>
    public static class RequestManager
    {
        public static bool IsAccepted(RequestDef r) => GameState.HasFlag("req:" + r.id);
        public static bool IsDone(RequestDef r) => GameState.HasFlag("reqdone:" + r.id);
        public static bool IsActive(RequestDef r) => IsAccepted(r) && !IsDone(r);
        public static bool IsAvailable(RequestDef r) => !IsAccepted(r) && GameState.Check(r.condition);
        public static bool IsReady(RequestDef r) => IsActive(r) && GameState.Check(r.completeCondition);
        public static string TurnIn(RequestDef r) => string.IsNullOrEmpty(r.turnIn) ? r.giver : r.turnIn;

        public static IEnumerable<RequestDef> Active()
        {
            foreach (var r in GameData.Requests)
                if (IsActive(r)) yield return r;
        }

        /// <summary>이름표에 붙일 표시: 의뢰를 줄 수 있으면 "의뢰", 끝낸 의뢰를 받을 사람이면 "의뢰 완료".</summary>
        public static string BadgeFor(string npcId)
        {
            foreach (var r in GameData.Requests)
                if (IsReady(r) && TurnIn(r) == npcId) return "의뢰 완료";
            foreach (var r in GameData.Requests)
                if (IsAvailable(r) && r.giver == npcId) return "의뢰";
            return null;
        }

        /// <summary>npcId 와 대화할 때 끝낼 의뢰나 줄 의뢰가 있으면 처리하고 true.</summary>
        public static bool TryHandle(string npcId)
        {
            foreach (var r in GameData.Requests)
                if (IsReady(r) && TurnIn(r) == npcId)
                {
                    Complete(r);
                    return true;
                }
            foreach (var r in GameData.Requests)
                if (IsAvailable(r) && r.giver == npcId)
                {
                    Accept(r);
                    return true;
                }
            return false;
        }

        static void Accept(RequestDef r)
        {
            DialogueSystem.I.Show(r.acceptLines, () =>
            {
                GameState.SetFlag("req:" + r.id);
                if (!string.IsNullOrEmpty(r.giveItem)) GameState.AddItem(r.giveItem);
                GameState.Save();
                HUD.Toast("새 의뢰: " + r.title);
                Sound.Play("LetterGet", 0.7f);
            });
        }

        static void Complete(RequestDef r)
        {
            DialogueSystem.I.Show(r.completeLines, () =>
            {
                if (r.consumeItems != null)
                    foreach (var id in r.consumeItems) GameState.RemoveItem(id);
                GameState.SetFlag("reqdone:" + r.id);
                GameState.SetFlag(r.setFlag);
                GameState.Save();
                HUD.Toast("의뢰 완료: " + r.title);
                Sound.Play("Delivered", 0.8f);
                if (!string.IsNullOrEmpty(r.rewardItem)) Inventory.Give(r.rewardItem, r.rewardCount > 0 ? r.rewardCount : 1);
                if (!string.IsNullOrEmpty(r.rewardText)) DialogueSystem.I.Show(new[] { new DialogueLine("", r.rewardText) });
            });
        }
    }
}
