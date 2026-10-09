namespace MoonlightPost
{
    /// <summary>
    /// 아이템 얻기·쓰기와 장비 효과 계산. 아이템 정의는 Resources/Data/items.json.
    /// 장비는 따로 끼우지 않고, 가지고 있으면 효과가 항상 적용된다.
    /// </summary>
    public static class Inventory
    {
        /// <summary>가진 장비들의 effect 값 합계(예: "dodge" → 40).</summary>
        public static float EffectSum(string effect)
        {
            float sum = 0f;
            foreach (var stack in GameState.Items)
            {
                var def = GameData.GetItem(stack.id);
                if (def != null && def.IsEquipment && def.effect == effect) sum += def.value;
            }
            return sum;
        }

        /// <summary>아이템을 주고 알림을 띄운 뒤 저장한다.</summary>
        public static void Give(string id, int count = 1)
        {
            var def = GameData.GetItem(id);
            if (def == null || count <= 0) return;
            int before = GameState.ItemCount(id);
            GameState.AddItem(id, count, def.maxStack > 0 ? def.maxStack : 99);
            int after = GameState.ItemCount(id);
            HUD.Toast("획득: " + def.name + (def.maxStack > 1 && def.rewardAt != null ? " (" + after + "/" + def.maxStack + ")" : count > 1 ? " x" + count : "") + "   [I] 가방");
            Sound.Play("LetterGet", 0.8f);

            // 모으면 주는 보상
            if (def.rewardAt != null && def.rewardItems != null)
                for (int i = 0; i < def.rewardAt.Length && i < def.rewardItems.Length; i++)
                    if (before < def.rewardAt[i] && after >= def.rewardAt[i] && GameState.ItemCount(def.rewardItems[i]) == 0)
                    {
                        var reward = GameData.GetItem(def.rewardItems[i]);
                        GameState.AddItem(def.rewardItems[i]);
                        if (reward != null)
                            DialogueSystem.I.Show(new[]
                            {
                                new DialogueLine("", def.name + "을(를) " + def.rewardAt[i] + "개 모았다!"),
                                new DialogueLine("", "보상: " + reward.name + "\n" + reward.description)
                            });
                        Sound.Play("Delivered", 0.8f);
                    }
            GameState.Save();
        }

        /// <summary>회복 아이템을 하나 쓴다(R 키). 쓸 수 있는 게 없으면 false.</summary>
        public static bool UseConsumable(PlayerController player)
        {
            foreach (var def in GameData.Items)
            {
                if (!def.IsConsumable || GameState.ItemCount(def.id) <= 0) continue;
                if (def.effect == "heal" && player.Health.Current >= player.Health.Max)
                {
                    HUD.Toast("체력이 가득 차 있다.");
                    return false;
                }
                GameState.RemoveItem(def.id);
                if (def.effect == "heal") player.Health.Heal(UnityEngine.Mathf.RoundToInt(def.value));
                HUD.Toast(def.name + "을(를) 먹었다. 체력 회복!");
                Sound.Play("Clue", 0.6f);
                GameState.Save();
                return true;
            }
            HUD.Toast("먹을 것이 없다.");
            return false;
        }

        /// <summary>지금 끼운 편지 도구 부품. 없으면 null(기본 봉인끈으로 동작).</summary>
        public static ItemDef CurrentToolPart
        {
            get
            {
                var def = GameData.GetItem(GameState.ToolPart);
                return def != null && def.IsPart && GameState.ItemCount(def.id) > 0 ? def : GameData.GetItem("seal_string");
            }
        }

        /// <summary>가진 부품 중 다음 것으로 바꾼다(C 키).</summary>
        public static void CycleToolPart()
        {
            var owned = new System.Collections.Generic.List<ItemDef>();
            foreach (var def in GameData.Items)
                if (def.IsPart && GameState.ItemCount(def.id) > 0) owned.Add(def);
            if (owned.Count <= 1)
            {
                HUD.Toast("바꿔 끼울 편지 도구가 아직 없다.");
                return;
            }
            var current = CurrentToolPart;
            int i = current != null ? owned.FindIndex(d => d.id == current.id) : -1;
            var next = owned[(i + 1) % owned.Count];
            GameState.SetToolPart(next.id);
            GameState.Save();
            HUD.Toast("편지 도구: " + next.name);
            Sound.Play("Talk", 0.6f);
        }

        public static int OwnedPartCount()
        {
            int n = 0;
            foreach (var def in GameData.Items)
                if (def.IsPart && GameState.ItemCount(def.id) > 0) n++;
            return n;
        }

        public static int ConsumableCount()
        {
            int n = 0;
            foreach (var def in GameData.Items)
                if (def.IsConsumable) n += GameState.ItemCount(def.id);
            return n;
        }
    }
}
