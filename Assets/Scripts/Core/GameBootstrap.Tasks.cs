using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 편지마다의 탐험 과제(편지 정의의 objectives 와 짝을 이룬다).
    ///   찾아 올 물건: 그 편지를 들고 있을 때만 나타나고, 배달할 때 건넨다(consumeItems).
    ///   작은 사건: 그 편지를 들고 있는 동안 그림자 떼가 모이고, 모두 물리치면 플래그가 선다.
    ///   단서: 받는 사람(누구)과 있는 곳(어디)을 따로 찾아야 하는 편지도 있다.
    /// </summary>
    public partial class GameBootstrap
    {
        void BuildLetterTasks()
        {
            // 편지 2: 오웬이 숲 입구에 두고 간 밀가루 자루
            QuestItem("owen_reply", "flour_bag", new Vector2(18.8f, 2.6f), "Bag", Art.FlourSack, "밀가루 자루",
                "숲 입구에 오웬이 두고 간 밀가루 자루가 있다. 꽤 무겁다.");
            // 편지 7: 북쪽 갯바위 선착장 창고의 등대 램프 기름
            QuestItem("storm_warning", "lamp_oil", new Vector2(-41.6f, 29f), "FireflyJar", Art.Chest(false), "램프 기름통",
                "옛 우편 선착장 창고 구석에 램프 기름통이 남아 있다.");
            // 편지 8: 바람에 날려 북쪽 숲 철길 끝까지 간 편지 뒷장
            QuestItem("owen_old_letter", "letter_half", new Vector2(44f, 23.4f), "PaperLetter", Art.Signpost, "찢어진 편지 뒷장",
                "무너진 터널 앞 철길 위에, 바닷물에 젖었다 마른 편지 뒷장이 걸려 있다.");
            // 편지 10: 우편차 자물쇠 열쇠(옛 신호소)
            QuestItem("teo_letter", "signal_key", new Vector2(26.2f, 27.6f), "Key", Art.Chest(false), "우편차 열쇠꾸러미",
                "신호소 문고리에 녹슨 열쇠꾸러미가 걸려 있다. 꼬리표: '우편차'.");

            // 편지 3: 옛 우체국장의 집 문에 붙은 쪽지(어디에 있는지)
            WorldVisuals.I.Register(Clue("door_note", new Vector2(-10.2f, -6.1f), ItemOr("PaperLetter", Art.Signpost)).gameObject,
                "delivered:owen_reply,!dora_home");
            // 편지 11: 테오가 모닥불을 떠나 선착장으로 간 발자국
            WorldVisuals.I.Register(Clue("teo_footprints", new Vector2(-20.4f, 6.4f), Art.Mound).gameObject, "carrying:hana_reply");

            director.encounters = new[]
            {
                // 편지 6: 테오의 모닥불을 둘러싼 먹물 박쥐
                new EncounterSpec
                {
                    letterId = "serin_request", flag = "cleared:teo_camp",
                    spawns = new[] { new Vector2(-22.6f, 6.2f), new Vector2(-18.2f, 5f), new Vector2(-20.4f, 8.4f) },
                    kinds = new[] { EnemyKind.Bat, EnemyKind.Bat, EnemyKind.Rat },
                    clearToast = "테오의 모닥불 주변이 조용해졌다."
                },
                // 편지 9: 빵 냄새를 맡고 오두막을 둘러싼 그림자 떼
                new EncounterSpec
                {
                    letterId = "mira_bread_letter", flag = "cleared:hut_siege",
                    spawns = new[] { new Vector2(45f, -8.4f), new Vector2(50f, -7.6f), new Vector2(44.6f, -5.2f), new Vector2(50.6f, -5.6f) },
                    kinds = new[] { EnemyKind.Rat, EnemyKind.Mole, EnemyKind.Bat, EnemyKind.Rat },
                    startToast = "빵 냄새를 맡은 그림자 떼가 숲 끝 오두막으로 몰려갔다는 소문이다.",
                    clearToast = "오두막 앞의 그림자 떼를 모두 쫓아냈다."
                },
            };
        }

        /// <summary>그 편지를 들고 있고 아직 줍지 않았을 때만 보이는 과제용 물건.</summary>
        void QuestItem(string letterId, string itemId, Vector2 pos, string icon, Sprite fallback, string name, string line)
        {
            WorldVisuals.I.Register(Group("Quest_" + itemId, () =>
            {
                var go = Spawner.Prop("QuestItem_" + itemId, pos, ItemOr(icon, fallback));
                Spawner.Glow(pos + new Vector2(0f, 0.4f), 2.2f, new Color(1f, 0.9f, 0.5f, 0.45f), go.transform);
                var pickup = go.AddComponent<ItemPickup>();
                pickup.pickupId = "quest_" + itemId;
                pickup.displayName = name;
                pickup.itemId = itemId;
                pickup.verb = "줍기";
                pickup.openLine = line;
                pickup.emptyLine = "이미 챙겼다.";
            }), "carrying:" + letterId + ",!got:" + itemId);
        }
    }
}
