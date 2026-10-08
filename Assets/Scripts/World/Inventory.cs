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
            GameState.AddItem(id, count, def.maxStack > 0 ? def.maxStack : 99);
            GameState.Save();
            HUD.Toast("획득: " + def.name + (count > 1 ? " x" + count : "") + "   [I] 가방");
            Sound.Play("LetterGet", 0.8f);
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

        public static int ConsumableCount()
        {
            int n = 0;
            foreach (var def in GameData.Items)
                if (def.IsConsumable) n += GameState.ItemCount(def.id);
            return n;
        }
    }
}
