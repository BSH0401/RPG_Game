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

            // ---- 2부 ----
            // 편지 15: 채석장에 떨어진, 먹물이 스민 폭풍 밤의 우편 자루
            QuestItem("yuna_report", "ink_mailbag", new Vector2(74.2f, -7.2f), "Bag", Art.FlourSack, "먹물이 스민 우편 자루",
                "바위 틈에 우편 자루가 끼어 있다. 자루에서 먹물이 배어 나와 바닥이 검게 젖어 있다. 소인 날짜는 삼 년 전 폭풍 밤.");
            // 편지 18: 첫 열차를 움직일 석탄(채석장 오두막 옆)
            QuestItem("dora_last_letter", "coal_sack", new Vector2(60.6f, -7.4f), "Bag", Art.FlourSack, "석탄 자루",
                "오두막 옆에 석탄 자루가 쌓여 있다. 꼬리표: '고개 역 증기 기관차용'.");
            // 편지 18: 북쪽 숲 신호소의 선로 전환기
            var lever = Spawner.Prop("NPC_signal_lever", new Vector2(25.2f, 27.2f), Art.Signpost, new Vector2(0.4f, 0.3f), new Vector2(0f, 0.15f));
            lever.AddComponent<NpcInteractable>().npcId = "signal_lever";

            BuildRequestTasks();

            director.encounters = new[]
            {
                // 편지 14: 천문대를 둘러싼 그림자 떼
                new EncounterSpec
                {
                    letterId = "pass_parcel", flag = "cleared:observatory",
                    spawns = new[] { new Vector2(77.6f, 24.2f), new Vector2(83f, 24.8f), new Vector2(80.6f, 22.4f), new Vector2(76.8f, 27.8f) },
                    kinds = new[] { EnemyKind.Bat, EnemyKind.Mole, EnemyKind.Rat, EnemyKind.Bat },
                    startToast = "천문대 쪽 하늘이 먹물처럼 어둡다. 그림자들이 모여든 모양이다.",
                    clearToast = "천문대 앞의 그림자들을 쫓아냈다. 유나에게 소포를 전하자."
                },
                // 편지 15: 채석장의 먹물 자루를 지키는 그림자 떼
                new EncounterSpec
                {
                    letterId = "yuna_report", flag = "cleared:quarry_shadows",
                    spawns = new[] { new Vector2(71.6f, -5.6f), new Vector2(76.6f, -5.2f), new Vector2(75f, -9.4f), new Vector2(70.6f, -8.6f), new Vector2(73.4f, -3.2f) },
                    kinds = new[] { EnemyKind.Mole, EnemyKind.Bat, EnemyKind.Rat, EnemyKind.Mole, EnemyKind.Bat },
                    clearToast = "우편 자루 주변의 그림자들이 흩어졌다."
                },
                // 의뢰(오웬): 북쪽 숲 신호소 공터에 생긴 그림자 둥지
                new EncounterSpec
                {
                    condition = "req:owen_nest", flag = "cleared:owen_nest",
                    spawns = new[] { new Vector2(26f, 25.4f), new Vector2(30.6f, 25.8f), new Vector2(28.4f, 24.2f), new Vector2(25f, 28.6f) },
                    kinds = new[] { EnemyKind.Mole, EnemyKind.Bat, EnemyKind.Rat, EnemyKind.Bat },
                    clearToast = "신호소 공터의 그림자 둥지를 걷어 냈다. 오웬에게 알리자."
                },
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

        /// <summary>의뢰에 필요한 물건과 조사할 곳(Resources/Data/requests.json 과 짝).</summary>
        void BuildRequestTasks()
        {
            // 미라: 그림자 들쥐가 끌고 간 반죽 그릇 (동쪽 숲 남쪽 길 끝)
            QuestItemWhen("req:mira_bowl,!got:dough_bowl", "dough_bowl", new Vector2(26f, -9.5f), "Croissant", Art.FlourSack, "반죽 그릇",
                "들쥐 발자국 사이에 하얀 반죽 그릇이 엎어져 있다. 이가 하나 빠졌지만 멀쩡하다.");
            // 도라: 폐역 역사 뒤에 떨어뜨린 옛 날짜 도장
            QuestItemWhen("req:dora_stamp,!got:date_stamp", "date_stamp", new Vector2(-13.4f, 30.6f), "SilverSeal", Art.Chest(false), "옛 날짜 도장",
                "마른 나무 뿌리 사이에 녹슨 날짜 도장이 박혀 있다. 손잡이에 '달빛 우체국'.");
            // 세린: 북쪽 갯바위 물웅덩이 옆의 등대 렌즈 조각
            QuestItemWhen("req:serin_lens,!got:lens_shard", "lens_shard", new Vector2(-28.5f, 24.6f), "FireflyJar", Art.Chest(false), "등대 렌즈 조각",
                "물웅덩이 가장자리에서 무언가 달빛을 되비춘다. 두꺼운 유리 조각이다.");
            // 노아: 북쪽 숲 무너진 터널에서 난다는 기적 소리
            WorldVisuals.I.Register(Clue("tunnel_whistle", new Vector2(46.2f, 19.4f), RockSprite(false)).gameObject, "req:noah_whistle,!reqdone:noah_whistle");
            // 바우: 고개 역 시계의 태엽 열쇠(채석장 남동쪽 구석)
            QuestItemWhen("req:bau_clock,!got:clock_key", "clock_key", new Vector2(84.2f, -9.6f), "Key", Art.Chest(false), "태엽 열쇠",
                "바위 아래 녹슨 태엽 열쇠가 반쯤 묻혀 있다. 손잡이에 '고개 역'.");
            // 유나: 바람에 흩어진 별지도 세 장(북쪽 갯바위, 폐역, 북쪽 숲)
            QuestItemWhen("req:yuna_pages,!got:star_page_1", "star_page_1", new Vector2(-45.2f, 18.2f), "PaperLetter", Art.Signpost, "별지도 첫째 장",
                "바닷바람에 날려 온 종이가 바위에 붙어 있다. 별자리 사이에 '1'이라고 적혀 있다.");
            QuestItemWhen("req:yuna_pages,!got:star_page_2", "star_page_2", new Vector2(-6f, 30.6f), "PaperLetter", Art.Signpost, "별지도 둘째 장",
                "승강장 뒤 마른 풀에 종이가 걸려 있다. 북쪽 하늘의 별이 빼곡하다. '2'.");
            QuestItemWhen("req:yuna_pages,!got:star_page_3", "star_page_3", new Vector2(49.6f, 30.4f), "PaperLetter", Art.Signpost, "별지도 셋째 장",
                "나뭇가지 끝에 종이가 걸려 있다. 동쪽 하늘, 달이 지나는 길이 그려져 있다. '3'.");
        }

        /// <summary>그 편지를 들고 있고 아직 줍지 않았을 때만 보이는 과제용 물건.</summary>
        void QuestItem(string letterId, string itemId, Vector2 pos, string icon, Sprite fallback, string name, string line) =>
            QuestItemWhen("carrying:" + letterId + ",!got:" + itemId, itemId, pos, icon, fallback, name, line);

        /// <summary>condition 이 참일 때만 보이는 과제용 물건.</summary>
        void QuestItemWhen(string condition, string itemId, Vector2 pos, string icon, Sprite fallback, string name, string line)
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
            }), condition);
        }
    }
}
