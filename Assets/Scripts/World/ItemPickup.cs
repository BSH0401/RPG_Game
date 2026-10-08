using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 상자·진열대처럼 조사하면 아이템을 주는 곳.
    /// 한 번만 열리거나(perNight=false), 밤마다 한 번씩 다시 채워진다(perNight=true).
    /// requireItem 이 있으면 그 아이템(열쇠)이 있어야 열린다.
    /// </summary>
    public class ItemPickup : Interactable
    {
        public string pickupId;
        public string displayName = "상자";
        public string itemId;
        public int count = 1;
        public string requireItem;
        public bool perNight;
        public string verb = "열기";
        public string openLine = "상자를 열었다.";
        public string lockedLine = "단단히 잠겨 있다. 열쇠가 필요하다.";
        public string emptyLine = "비어 있다.";

        public SpriteRenderer visual;
        public Sprite closedSprite;
        public Sprite openSprite;

        string Flag => "pickup:" + pickupId + (perNight ? ":" + GameState.Night : "");
        bool Taken => GameState.HasFlag(Flag);

        public override string DisplayName => displayName;
        public override string Prompt => displayName + (Taken ? " 살펴보기" : " " + verb);

        void Start()
        {
            GameState.Changed += RefreshVisual;
            RefreshVisual();
        }

        void OnDestroy() => GameState.Changed -= RefreshVisual;

        void RefreshVisual()
        {
            if (visual != null && openSprite != null && closedSprite != null)
                visual.sprite = Taken ? openSprite : closedSprite;
        }

        public override void Interact(PlayerController player)
        {
            if (Taken)
            {
                DialogueSystem.I.Show(new[] { new DialogueLine("", emptyLine) });
                return;
            }
            if (!string.IsNullOrEmpty(requireItem) && !GameState.HasFlag("has:" + requireItem))
            {
                DialogueSystem.I.Show(new[] { new DialogueLine("", lockedLine) });
                return;
            }

            var def = GameData.GetItem(itemId);
            string itemName = def != null ? def.name : itemId;
            DialogueSystem.I.Show(new[]
            {
                new DialogueLine("", openLine),
                new DialogueLine("", itemName + (count > 1 ? " " + count + "개" : "") + "을(를) 얻었다." + (def != null ? "\n" + def.description : ""))
            }, () =>
            {
                GameState.SetFlag(Flag);
                Inventory.Give(itemId, count);
            });
        }
    }
}
