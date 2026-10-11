using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 자동 플레이테스트. 실행 인자에 "-playtest 폴더" 가 있을 때만 동작한다(평소 플레이에는 영향 없음).
    ///   1) 편지 12통을 실제 게임 코드(창구 → 단서 → 보스 → 배달)로 끝까지 진행한다. 선택지는
    ///      1회차는 모두 첫 번째, 2회차는 홀수 번째 편지에서 마지막·짝수 번째에서 첫 번째를 골라 두 엔딩을 모두 본다.
    ///   2) 단계마다 플레이어가 걸어서 닿을 수 있는 곳을 바닥 칸 단위로 계산해, 대화·조사 대상 중
    ///      닿을 수 없는 것이 있으면 [문제] 로 기록한다.
    ///   3) 주요 장면을 스크린샷으로 남기고, 모든 대사를 transcript.txt 에 적는다.
    /// 결과: 폴더/report.txt, transcript.txt, *.png
    /// </summary>
    public class Playtest : MonoBehaviour
    {
        static string outDir;
        readonly StringBuilder report = new StringBuilder();
        readonly StringBuilder transcript = new StringBuilder();
        int shotIndex;
        int problems;
        float walked;
        int linesShown;
        bool scenery;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-playtest") outDir = args[i + 1];
            if (outDir == null) return;

            Directory.CreateDirectory(outDir);
            GameState.SaveFileName = "moonlight_post_playtest.json";
            GameMenu.SkipTitle = true;
            GameState.DeleteSave();
            var go = new GameObject("Playtest");
            DontDestroyOnLoad(go);
            go.AddComponent<Playtest>();
        }

        IEnumerator Start()
        {
            DialogueSystem.Shown += OnShown;
            yield return Frames(10);
            for (int route = 0; route < 2; route++)
            {
                scenery = route == 0;
                yield return RunRoute(route);
                GameBootstrap.RestartNewGame();
                yield return Frames(10);
            }
            Log("");
            Log(problems == 0 ? "결과: 문제 없음" : "결과: 문제 " + problems + "건");
            File.WriteAllText(Path.Combine(outDir, "report.txt"), report.ToString());
            File.WriteAllText(Path.Combine(outDir, "transcript.txt"), transcript.ToString());
            GameState.DeleteSave();
            Application.Quit();
        }

        void OnShown(IList<DialogueLine> lines, string[] choices)
        {
            linesShown += lines.Count;
            foreach (var l in lines)
                transcript.AppendLine((string.IsNullOrEmpty(l.speaker) ? "  " : l.speaker + ": ") + l.text);
            if (choices != null)
                for (int i = 0; i < choices.Length; i++) transcript.AppendLine("   [" + (i + 1) + "] " + choices[i]);
            transcript.AppendLine();
        }

        // ------------------------------------------------------------------ 한 회차

        IEnumerator RunRoute(int choice)
        {
            int route = choice;
            Log("=== " + (route + 1) + "회차: 선택지 " + (route == 0 ? "모두 첫 번째" : "홀수 편지는 마지막, 짝수 편지는 첫 번째") + " ===");
            transcript.AppendLine("=================== " + (route + 1) + "회차 ===================");
            var player = FindAnyObjectByType<PlayerController>();
            player.Health.Invulnerable = true;
            // GameState.Load 가 구독을 비우므로 새 게임마다 다시 구독한다.
            GameState.Changed += MarkDirty;
            GameState.NightChanged += MarkDirty;
            openDirty = true;

            if (scenery) yield return Overviews("start");
            if (scenery) yield return MenuShots();
            if (scenery) yield return GrowthTest(player);
            if (scenery) yield return MonsterLineup(player);
            if (scenery) yield return GearTest(player);
            if (scenery) yield return HuntTest(player);
            if (scenery) yield return WeaponLook(player);
            if (scenery) yield return BurrowWallTest(player);
            CheckReach("시작");

            for (int n = 1; n <= 20; n++)
            {
                walked = 0f;
                linesShown = 0;
                choice = route == 0 || n % 2 == 0 ? 0 : 99;
                var counter = FindAnyObjectByType<PostOfficeCounter>();
                yield return Visit(player, counter.transform.position);
                counter.Interact(player);
                var letter = LetterManager.Carrying;
                if (letter == null)
                {
                    yield return Shot("ending_" + (route + 1));
                    var last = Last();
                    yield return Drain(choice);
                    Log("엔딩: " + last);
                    break;
                }
                if (scenery) yield return Shot(n.ToString("00") + "_receive_" + letter.id);
                yield return Drain(choice);
                Log(n + ". 「" + letter.title + "」 → " + letter.recipientName + "  (밤 " + GameState.Night + ", " + NightDirector.MoodName(NightDirector.Mood) + ")");
                CheckReach(letter.id);
                CheckEnemies(letter.id + " 새 밤");
                if (scenery) yield return HuntGolden(player, n);

                // 받는 사람 알아내기 → (막혀 있으면) 받는 사람에게 한 번 가 보기 → 배달 조건 풀기
                yield return Solve(player, letter.id, letter.revealFlag, n);
                if (!LetterManager.IsRecipientRevealed(letter)) Problem(letter.id + ": 받는 사람이 밝혀지지 않음 (" + letter.revealFlag + ")");
                if (!GameState.Check(letter.deliverCondition))
                {
                    var target = NpcInteractable.Find(letter.recipientId);
                    if (target != null)
                    {
                        yield return Visit(player, target.transform.position);
                        target.Interact(player);
                        if (scenery) yield return Shot(n.ToString("00") + "_blocked_" + letter.id);
                        yield return Drain(choice);
                    }
                    yield return Solve(player, letter.id, letter.deliverCondition, n);
                    if (!GameState.Check(letter.deliverCondition)) Problem(letter.id + ": 배달 조건이 풀리지 않음 (" + letter.deliverCondition + ")");
                }
                if (!string.IsNullOrEmpty(letter.deliverCondition)) CheckReach(letter.id + " 배달 전");
                if (letter.objectives != null)
                    foreach (var o in letter.objectives)
                        if (!GameState.Check(o.condition)) Problem(letter.id + ": 배달 직전인데 끝나지 않은 과제: " + o.text);

                var npc = NpcInteractable.Find(letter.recipientId);
                if (npc == null)
                {
                    Problem(letter.id + ": 받는 사람(" + letter.recipientId + ")이 월드에 없다");
                    LetterManager.DevSkip();
                    yield return Drain(choice);
                    continue;
                }
                yield return Visit(player, npc.transform.position);
                npc.Interact(player);
                if (scenery) yield return Shot(n.ToString("00") + "_deliver_" + letter.id);
                yield return Drain(choice);
                if (!LetterManager.IsDelivered(letter)) Problem(letter.id + ": 대화했는데 배달되지 않음");
                Log("   이동 약 " + walked.ToString("0") + "칸(걸어서 약 " + (walked / player.moveSpeed).ToString("0") + "초), 대사 " + linesShown + "줄");

                if (scenery) yield return VillageShot(n.ToString("00") + "_after_" + letter.id);
                if (letter.deliveredFlag == RailStop.UnlockFlag) yield return RideRails(player, n);
                if (scenery && n == 2) yield return UpgradeTest(player);
                if (scenery) yield return DoRequests(player, n);
                if (scenery) yield return TalkToEveryone(player, "「" + letter.title + "」 배달 후");
            }
            if (scenery)
            {
                int trophies = GameState.ItemCount(Loot.Trophy), legends = 0;
                foreach (var id in Loot.Legendaries)
                    if (GameState.ItemCount(id) > 0) legends++;
                Log("황금 그림자 " + trophies + "마리, 전설 장비 " + legends + "개, 먹물 결정 " + GameState.ItemCount(Upgrade.Crystal) + ", 달빛 조각 " + GameState.ItemCount(Upgrade.Shard)
                    + ", 레벨 " + Stats.Level + " (공격력 " + Stats.Attack + ", 최대 체력 " + GameState.MaxHp + ", 방어 " + Stats.Defense + ")"
                    + ", 장비 " + GameState.Gear.Count + "개(낀 것 " + new List<GearItem>(GameState.Equipped()).Count + "), 은화 " + GameState.Silver
                    + ", 사냥 의뢰 " + GameState.Data.bountiesDone + "개");
                if (trophies >= 4 && legends == 0) Problem("황금 그림자를 " + trophies + "마리 잡았는데 전설 장비가 하나도 없음(천장 동작 안 함)");
            }
            if (scenery) yield return Overviews("end");
            CheckReach("엔딩 후");
            player.Health.Invulnerable = false;
        }

        /// <summary>편지를 들고 있지 않을 때 모든 주민에게 말을 걸어 지금 하는 대사를 기록한다.</summary>
        IEnumerator TalkToEveryone(PlayerController player, string when)
        {
            transcript.AppendLine("----- 주민 대사 점검: " + when + " -----");
            foreach (var it in new List<Interactable>(Interactable.All))
            {
                if (!(it is NpcInteractable npc) || it == null || !it.isActiveAndEnabled) continue;
                transcript.Append("[" + npc.DisplayName + "에게 말 걸기] ");
                yield return Visit(player, npc.transform.position);
                npc.Interact(player);
                yield return Drain(0);
            }
            transcript.AppendLine("----- 점검 끝 -----");
            transcript.AppendLine();
        }

        /// <summary>
        /// 조건의 남은 항목을 하나씩 해결한다: 단서는 조사하고, "has:아이템"은 그 아이템을 주는 곳을 찾아 줍고,
        /// 대화로 세우는 플래그는 그 주민에게 말을 걸고, 나머지(보스·사건)는 그 적들을 쓰러뜨린다.
        /// </summary>
        IEnumerator Solve(PlayerController player, string label, string condition, int n)
        {
            if (string.IsNullOrEmpty(condition)) yield break;
            for (int pass = 0; pass < 8 && !GameState.Check(condition); pass++)
            {
                string term = null;
                foreach (var raw in condition.Split('|')[0].Split(','))
                {
                    var t = raw.Trim();
                    if (t.Length > 0 && !t.StartsWith("!") && !GameState.HasFlag(t)) { term = t; break; }
                }
                if (term == null) yield break;

                Interactable target = FindClue(term);
                if (target == null && (term.StartsWith("has:") || term.StartsWith("got:")))
                {
                    var item = term.Substring(4);
                    foreach (var it in Interactable.All)
                        if ((it is ItemPickup p && p.itemId == item) || (it is NpcInteractable giver && FirstTalk(giver)?.giveItem == item)) { target = it; break; }
                }
                if (target == null)
                    foreach (var it in Interactable.All)
                        if (it is NpcInteractable npc && FirstTalk(npc)?.setFlag == term) { target = it; break; }

                if (target != null)
                {
                    yield return Visit(player, target.transform.position);
                    target.Interact(player);
                    if (scenery) yield return Shot(n.ToString("00") + "_task_" + term.Replace(':', '-'));
                    yield return Drain(0);
                    continue;
                }

                var foes = new List<GameObject>(NightDirector.I != null ? NightDirector.I.EventEnemies() : new GameObject[0]);
                if (foes.Count == 0)
                {
                    Problem(label + ": '" + term + "'을(를) 해결할 단서·물건·대화·적을 찾지 못함");
                    yield break;
                }
                // 적 가까이의 걸어서 닿는 칸에 선다(예전에는 적 3칸 아래로 순간이동해 벽 밖에 서기도 했다).
                if (!Flood(player.transform.position)) Flood(GameBootstrapSpawn());
                var near = Stand(foes[0].transform.position, 4f);
                if (near == null) Problem(label + ": 사건의 적(" + (Vector2)foes[0].transform.position + ") 근처에 걸어서 갈 수 없음");
                yield return Visit(player, near ?? (Vector2)foes[0].transform.position, false);
                CheckEnemies(label + " 전투");
                yield return Seconds(1.2f);
                if (scenery) yield return Shot(n.ToString("00") + "_fight_" + term.Replace(':', '-'));
                foreach (var f in foes)
                    if (f != null) f.GetComponent<Health>().TakeDamage(999, player.transform.position);
                yield return Seconds(2f);
                CheckEnemies(label + " 전투 뒤");
            }
        }

        /// <summary>지금 받을 수 있는 의뢰를 모두 받아서 끝낸다(1회차만).</summary>
        IEnumerator DoRequests(PlayerController player, int n)
        {
            foreach (var r in GameData.Requests)
            {
                if (!RequestManager.IsAvailable(r)) continue;
                walked = 0f;
                linesShown = 0;
                var giver = NpcInteractable.Find(r.giver);
                if (giver == null)
                {
                    Problem("의뢰 " + r.id + ": 의뢰하는 주민(" + r.giver + ")이 월드에 없다");
                    continue;
                }
                yield return Visit(player, giver.transform.position);
                giver.Interact(player);
                yield return Drain(0);
                if (!RequestManager.IsAccepted(r))
                {
                    Problem("의뢰 " + r.id + ": 말을 걸어도 의뢰를 받지 못함");
                    continue;
                }
                yield return Solve(player, "의뢰 " + r.id, r.completeCondition, n);
                var turnIn = NpcInteractable.Find(RequestManager.TurnIn(r));
                if (turnIn == null)
                {
                    Problem("의뢰 " + r.id + ": 완료를 알릴 주민이 월드에 없다");
                    continue;
                }
                yield return Visit(player, turnIn.transform.position);
                turnIn.Interact(player);
                if (scenery) yield return Shot(n.ToString("00") + "_request_" + r.id);
                yield return Drain(0);
                if (!RequestManager.IsDone(r)) Problem("의뢰 " + r.id + ": 끝내지 못함 (" + r.completeCondition + ")");
                else Log("   의뢰 「" + r.title + "」 완료 — 이동 약 " + walked.ToString("0") + "칸(걸어서 약 " + (walked / player.moveSpeed).ToString("0") + "초), 대사 " + linesShown + "줄");
            }
        }

        /// <summary>오늘 밤 황금 그림자를 찾아가 쓰러뜨리고 떨어진 전리품을 모두 줍는다.</summary>
        IEnumerator HuntGolden(PlayerController player, int n)
        {
            var golden = NightDirector.Golden;
            if (GameState.Night >= 2 && golden == null && !GameState.HasFlag(NightDirector.GoldenFlag))
            {
                Problem("밤 " + GameState.Night + ": 황금 그림자가 나오지 않음");
                yield break;
            }
            if (golden == null) yield break;
            if (!Flood(player.transform.position)) Flood(GameBootstrapSpawn());
            var near = Stand(golden.transform.position, 4f);
            if (near == null)
            {
                Problem("밤 " + GameState.Night + ": 황금 그림자(" + NightDirector.GoldenRegion + " " + (Vector2)golden.transform.position + ")에 걸어서 갈 수 없음");
                yield break;
            }
            yield return Visit(player, near.Value, false);
            yield return Seconds(0.6f);
            bool firstShot = GameState.ItemCount(Loot.Trophy) == 0;
            if (firstShot) yield return Shot(n.ToString("00") + "_golden");
            int crystals = GameState.ItemCount(Upgrade.Crystal), trophies = GameState.ItemCount(Loot.Trophy);
            if (golden != null) golden.Health.TakeDamage(999, player.transform.position);
            yield return Seconds(1.2f);
            if (firstShot) yield return Shot(n.ToString("00") + "_golden_loot");
            // 전리품 줍기: 떨어진 자리마다 서 본다(빨려 들어오는지 확인).
            for (int pass = 0; pass < 3 && LootDrop.All.Count > 0; pass++)
                foreach (var drop in new List<LootDrop>(LootDrop.All))
                {
                    if (drop == null) continue;
                    Teleport(player, drop.transform.position);
                    yield return Seconds(0.25f);
                }
            yield return Seconds(0.4f);
            if (LootDrop.All.Count > 0) Problem("밤 " + GameState.Night + ": 전리품 " + LootDrop.All.Count + "개를 줍지 못함");
            if (!GameState.HasFlag(NightDirector.GoldenFlag)) Problem("밤 " + GameState.Night + ": 황금 그림자를 쓰러뜨렸는데 기록되지 않음");
            int got = GameState.ItemCount(Upgrade.Crystal) - crystals;
            if (GameState.ItemCount(Loot.Trophy) <= trophies) Problem("밤 " + GameState.Night + ": 황금 그림자 증표를 얻지 못함");
            Log("   황금 그림자(" + NightDirector.GoldenRegion + ") 처치: 먹물 결정 +" + got + ", 증표 " + GameState.ItemCount(Loot.Trophy));
        }

        /// <summary>강화: 재료를 넉넉히 주고 우편가방을 +5까지 올려 본다(최대 체력이 2 늘어야 한다).</summary>
        IEnumerator UpgradeTest(PlayerController player)
        {
            var bag = GameData.GetItem("mail_bag");
            if (bag == null || GameState.ItemCount("mail_bag") == 0)
            {
                Problem("강화 검사: 우편가방이 없음");
                yield break;
            }
            var bench = FindAnyObjectByType<Workbench>();
            if (bench == null) Problem("강화 검사: 강화 작업대가 월드에 없음");
            else
            {
                yield return Visit(player, bench.transform.position);
                bench.Interact(player);
                yield return Frames(3);
                yield return Shot("upgrade_menu");
                UpgradeMenu.Close();
                yield return Frames(2);
            }
            GameState.AddItem(Upgrade.Crystal, 999, 999);
            GameState.AddItem(Upgrade.Shard, 60, 99);
            int hpBefore = GameState.MaxHp, tries = 0, fails = 0;
            while (!Upgrade.IsMax(bag) && tries < 40)
            {
                tries++;
                if (!UpgradeMenu.TryNow(bag, out bool ok))
                {
                    Problem("강화 검사: 재료가 있는데 강화를 시도하지 못함 (단계 " + Upgrade.Level(bag) + ")");
                    break;
                }
                if (!ok) fails++;
            }
            int gained = GameState.MaxHp - hpBefore;
            if (!Upgrade.IsMax(bag)) Problem("강화 검사: 40번 안에 +5에 닿지 못함");
            else if (gained != 20) Problem("강화 검사: 우편가방 +5인데 최대 체력이 " + gained + " 늘어남(20 기대)");
            else Log("   강화 검사: 우편가방 +5 (" + tries + "번 시도, 실패 " + fails + "번), 최대 체력 +" + gained);

            // 주운 장비 강화: 낀 무기를 +10까지. 공격력이 올라야 하고, +6 부터는 실패하면 한 단계 떨어질 수 있다.
            var weapon = GameState.EquippedIn(GearSlot.Weapon);
            if (weapon == null)
            {
                weapon = Gear.Roll(Stats.Level, Gear.Rare, GearSlot.Weapon);
                Gear.Pickup(weapon);
                GameState.Equip(weapon);
            }
            GameState.AddSilver(200000);
            GameState.AddItem(Upgrade.Crystal, 999, 999);
            GameState.AddItem(Upgrade.Shard, 99, 99);
            int atk0 = Stats.Attack, plus0 = weapon.plus, gTries = 0, gFails = 0, drops = 0;
            while (!Upgrade.IsMax(weapon) && gTries < 120)
            {
                gTries++;
                if (GameState.ItemCount(Upgrade.Crystal) < 100) GameState.AddItem(Upgrade.Crystal, 500, 999);
                if (GameState.ItemCount(Upgrade.Shard) < 20) GameState.AddItem(Upgrade.Shard, 60, 99);
                int before = weapon.plus;
                if (!Upgrade.Try(weapon, out bool ok, out bool dropped))
                {
                    Problem("장비 강화: 재료가 있는데 시도하지 못함 (+" + weapon.plus + ", 은화 " + GameState.Silver + ")");
                    break;
                }
                if (ok && weapon.plus != before + 1) Problem("장비 강화: 성공했는데 단계가 오르지 않음");
                if (!ok) gFails++;
                if (dropped) drops++;
                if (dropped && before < 6) Problem("장비 강화: +" + before + " 에서 실패했는데 단계가 떨어짐");
            }
            if (!Upgrade.IsMax(weapon)) Problem("장비 강화: 120번 안에 +10에 닿지 못함 (+" + weapon.plus + ")");
            if (Stats.Attack <= atk0) Problem("장비 강화: 무기를 강화했는데 공격력이 그대로 (" + atk0 + " → " + Stats.Attack + ")");
            if (bench != null)
            {
                bench.Interact(player);
                UpgradeMenu.ShowTab(0);
                yield return Frames(3);
                yield return Shot("upgrade_gear");
                UpgradeMenu.Close();
                yield return Frames(2);
            }
            Log("   장비 강화 검사: " + weapon.Name + " +" + plus0 + " → +" + weapon.plus + " (" + gTries + "번 시도, 실패 " + gFails + "번, 떨어짐 " + drops + "번), 공격력 " + atk0 + " → " + Stats.Attack);
        }

        static void Teleport(PlayerController player, Vector2 p)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            rb.position = p;
            player.transform.position = p;
        }

        /// <summary>
        /// 성장: 쥐를 불러 피해 숫자(치명타 포함)를 찍고, 쓰러뜨려 경험치가 오르는지, 레벨업과 스탯 분배가
        /// 공격력·최대 체력에 그대로 반영되는지 본다. 캐릭터 창 스크린샷도 남긴다.
        /// </summary>
        IEnumerator GrowthTest(PlayerController player)
        {
            Vector2 p = player.transform.position;
            int lv0 = Stats.Level, exp0 = Stats.Exp;
            var rats = new List<EnemyController>();
            for (int i = 0; i < 3; i++)
                rats.Add(Spawner.Enemy(p + new Vector2(1.6f + i * 0.9f, 0.7f * (i - 1)), EnemyKind.Rat).GetComponent<EnemyController>());
            yield return Frames(2);
            foreach (var r in rats) r.TakeHit(Stats.RollDamage(1f, out bool crit), p, 0f, crit);
            rats[1].TakeHit(Mathf.RoundToInt(Stats.Attack * Stats.CritMultiplier), p, 0f, true);
            yield return Seconds(0.15f);
            yield return Shot("growth_damage_numbers");
            foreach (var r in rats)
                if (r != null) r.Health.TakeDamage(999, p);
            yield return Seconds(0.3f);
            if (Stats.Level == lv0 && Stats.Exp <= exp0) Problem("성장: 그림자를 쓰러뜨렸는데 경험치가 오르지 않음");

            int lv1 = Stats.Level, points = Stats.Points;
            Stats.AddExp(Stats.ExpToNext() - Stats.Exp, p);
            yield return Seconds(0.3f);
            yield return Shot("growth_level_up");
            if (Stats.Level != lv1 + 1) Problem("성장: 경험치를 채웠는데 레벨이 " + lv1 + " → " + Stats.Level);
            if (Stats.Points != points + Stats.PointsPerLevel) Problem("성장: 레벨업 스탯 포인트가 " + (Stats.Points - points) + "개");
            if (player.Health.Current != player.Health.Max) Problem("성장: 레벨업했는데 체력이 다 차지 않음");

            int atk = Stats.Attack;
            Stats.Spend(Stat.Str);
            if (Stats.Attack != atk + 2) Problem("성장: 힘 1 → 공격력 " + atk + " → " + Stats.Attack + " (2 기대)");
            int hp = player.Health.Max;
            Stats.Spend(Stat.Vit);
            yield return Frames(1);
            if (player.Health.Max != hp + 8) Problem("성장: 체력 1 → 최대 체력 " + hp + " → " + player.Health.Max + " (8 기대)");

            HUD.SetChar(true);
            yield return Frames(3);
            yield return Shot("growth_character");
            HUD.SetChar(false);
            yield return Frames(2);
            if (Time.timeScale != 1f) Problem("캐릭터 창을 닫았는데 게임이 멈춰 있음");
            Log("   성장 검사: 레벨 " + Stats.Level + ", 공격력 " + Stats.Attack + ", 최대 체력 " + player.Health.Max + ", 남은 포인트 " + Stats.Points);
        }

        /// <summary>
        /// 장비 파밍: 등급마다 장비를 하나씩 주워 빈 부위가 바로 채워지는지, 더 센 무기를 끼면 공격력이 오르는지,
        /// 갑옷이 방어를 주는지 본다. 영웅 장비가 빛기둥과 함께 떨어진 모습, 장비 가방, 상점(사기·팔기)도 찍는다.
        /// </summary>
        IEnumerator GearTest(PlayerController player)
        {
            int atk0 = Stats.Attack;
            for (int r = Gear.Common; r <= Gear.Legendary; r++) Gear.Pickup(Gear.Roll(Stats.Level, r, GearSlot.Ring));
            Gear.Pickup(Gear.Roll(Stats.Level, Gear.Rare, GearSlot.Body));
            if (GameState.EquippedIn(GearSlot.Ring) == null) Problem("장비: 빈 반지 칸에 주운 반지가 끼워지지 않음");
            if (Stats.Defense <= 0) Problem("장비: 옷을 꼈는데 방어가 0");
            var weak = Gear.Roll(1, Gear.Common, GearSlot.Weapon);
            var strong = Gear.Roll(Stats.Level + 10, Gear.Epic, GearSlot.Weapon);
            Gear.Pickup(weak);
            GameState.Equip(weak);
            int atkWeak = Stats.Attack;
            Gear.Pickup(strong);
            GameState.Equip(strong);
            int atkStrong = Stats.Attack;
            if (atkWeak <= atk0) Problem("장비: 무기를 꼈는데 공격력이 그대로 (" + atk0 + " → " + atkWeak + ")");
            if (atkStrong <= atkWeak) Problem("장비: 더 센 무기로 바꿨는데 공격력이 오르지 않음 (" + atkWeak + " → " + atkStrong + ")");

            // 바닥에 떨어진 영웅·전설 장비(빛기둥)
            Vector2 p = player.transform.position;
            LootDrop.SpawnGear(p + new Vector2(4.5f, -1.5f), Gear.Roll(Stats.Level, Gear.Epic));
            LootDrop.SpawnGear(p + new Vector2(-4.5f, -1.5f), Gear.Roll(Stats.Level, Gear.Legendary));
            yield return Seconds(0.35f);
            yield return Shot("gear_drop");
            int before = GameState.Gear.Count;
            for (int pass = 0; pass < 3 && LootDrop.All.Count > 0; pass++)
                foreach (var drop in new List<LootDrop>(LootDrop.All))
                {
                    if (drop == null) continue;
                    Teleport(player, drop.transform.position);
                    yield return Seconds(0.25f);
                }
            if (GameState.Gear.Count < before + 2) Problem("장비: 떨어진 장비 2개를 줍지 못함 (" + before + " → " + GameState.Gear.Count + ")");

            HUD.ShowBag(true, 0);
            yield return Frames(3);
            yield return Shot("gear_bag");
            HUD.ShowBag(false);
            yield return Frames(2);

            // 상점: 은화를 넉넉히 주고 장비 상자 · 물약을 사고, 일반·고급을 판다
            var shop = FindAnyObjectByType<Shop>();
            if (shop == null) Problem("상점: 떠돌이 상인이 월드에 없음");
            else
            {
                yield return Visit(player, shop.transform.position);
                GameState.AddSilver(3000);
                shop.Interact(player);
                yield return Frames(3);
                int silver = GameState.Silver, gear = GameState.Gear.Count;
                if (!ShopMenu.I.TryBuy(3)) Problem("상점: 장비 상자를 사지 못함");
                if (!ShopMenu.I.TryBuy(0)) Problem("상점: 회복 물약을 사지 못함");
                if (GameState.Gear.Count != gear + 1) Problem("상점: 장비 상자를 샀는데 장비가 늘지 않음");
                if (GameState.ItemCount("life_potion") < 1) Problem("상점: 회복 물약이 가방에 없음");
                yield return Frames(2);
                yield return Shot("shop_buy");
                ShopMenu.ShowTab(1);
                yield return Frames(2);
                yield return Shot("shop_sell");
                int sold = ShopMenu.I.SellUpTo(Gear.Uncommon);
                ShopMenu.Close();
                yield return Frames(2);
                if (Time.timeScale != 1f) Problem("상점을 닫았는데 게임이 멈춰 있음");
                Log("   장비 검사: 공격력 " + atk0 + " → " + atkWeak + " → " + atkStrong + ", 방어 " + Stats.Defense + ", 가방 " + GameState.Gear.Count
                    + ", 상점 은화 " + silver + " → " + GameState.Silver + ", 판 장비 " + sold);
            }
        }

        /// <summary>
        /// 사냥 3단계: 깊은 지역의 몬스터가 레벨·체력이 더 높은지, 정예가 더 튼튼한지,
        /// 정예를 쓰러뜨리면 사냥 의뢰가 세어지는지, 게시판에서 보상(은화·경험치·장비)을 받는지 본다.
        /// </summary>
        IEnumerator HuntTest(PlayerController player)
        {
            var nd = NightDirector.I;
            Vector2? lowP = null, highP = null;
            foreach (var r in nd.Regions())
            {
                if (r.spots == null || r.spots.Length == 0) continue;
                if (lowP == null) lowP = r.spots[0];
                highP = r.spots[0];
            }
            var low = Spawner.Enemy(lowP.Value, EnemyKind.Rat).GetComponent<EnemyController>();
            var high = Spawner.Enemy(highP.Value, EnemyKind.Rat).GetComponent<EnemyController>();
            low.enabled = high.enabled = false;
            if (high.level <= low.level) Problem("몬스터 레벨: 깊은 지역(" + high.region + " Lv." + high.level + ")이 안쪽(" + low.region + " Lv." + low.level + ")보다 높지 않음");
            if (high.Health.Max <= low.Health.Max) Problem("몬스터 레벨: 높은 레벨인데 체력이 더 많지 않음");
            string levels = low.region + " Lv." + low.level + "(체력 " + low.Health.Max + ") · " + high.region + " Lv." + high.level + "(체력 " + high.Health.Max + ")";
            Destroy(low.gameObject);
            Destroy(high.gameObject);

            // 정예 넷을 세워 두고 찍는다
            Bounties.Refresh();
            Vector2 p = (Vector2)player.transform.position + new Vector2(0f, -3.6f);
            var elites = new List<EnemyController>();
            var kinds = new[] { EnemyKind.Rat, EnemyKind.Bat, EnemyKind.Rat, EnemyKind.Bat };
            for (int i = 0; i < 4; i++)
            {
                var e = Spawner.Enemy(p + new Vector2(-3.3f + i * 2.2f, 0f), kinds[i]).GetComponent<EnemyController>();
                int normalHp = e.Health.Max;
                e.MakeElite((EliteAffix)(i + 1));
                if (e.Health.Max <= normalHp) Problem("정예: 체력이 그대로 (" + e.FullName + ")");
                // 이름표가 보이도록 켜 둔 채, 플레이어를 알아보지 못하게 한다
                e.detectRange = 0f;
                elites.Add(e);
            }
            yield return MoveCamera(p + new Vector2(0f, 0.4f), 3.6f);
            HUD.ClearToasts();
            yield return Frames(4);
            yield return Shot("elites");
            yield return CameraRestore();

            var eliteBounty = Bounties.All.Find(b => b.type == 2 && !b.claimed && b.night == GameState.Night);
            int before = eliteBounty != null ? eliteBounty.have : 0;
            foreach (var e in elites)
                if (e != null)
                {
                    e.Health.Invulnerable = false;
                    e.Health.TakeDamage(999999, player.transform.position);
                }
            yield return Seconds(0.3f);
            // 갈라진 작은 그림자는 치운다
            foreach (var e in new List<EnemyController>(EnemyController.Active))
                if (e != null && e.noLoot && Vector2.Distance(e.transform.position, p) < 6f) Destroy(e.gameObject);
            if (eliteBounty == null) Problem("사냥 의뢰: 정예 사냥 의뢰가 없음");
            else if (eliteBounty.have <= before) Problem("사냥 의뢰: 정예를 쓰러뜨렸는데 세어지지 않음 (" + before + " → " + eliteBounty.have + ")");

            // 남은 의뢰도 채운 것으로 하고 게시판에서 보상을 받는다
            foreach (var b in Bounties.All) b.have = Mathf.Max(b.have, b.need);
            var board = FindAnyObjectByType<BountyBoard>();
            if (board == null)
            {
                Problem("사냥 게시판이 월드에 없음");
                yield break;
            }
            yield return Visit(player, board.transform.position);
            HUD.ClearToasts();
            yield return Frames(3);
            yield return Shot("bounty_board_world");
            int silver = GameState.Silver, exp = GameState.Data.exp, lv = Stats.Level, gear = GameState.Gear.Count;
            board.Interact(player);
            yield return Frames(3);
            int claimed = 0;
            for (int i = 0; i < Bounties.All.Count; i++)
                if (BountyMenu.I.TryClaim(i)) claimed++;
            yield return Frames(2);
            yield return Shot("bounty_board");
            BountyMenu.Close();
            yield return Frames(2);
            if (claimed == 0) Problem("사냥 의뢰: 보상을 하나도 받지 못함");
            if (GameState.Silver <= silver) Problem("사냥 의뢰: 보상 은화가 들어오지 않음");
            if (Stats.Level == lv && GameState.Data.exp <= exp) Problem("사냥 의뢰: 보상 경험치가 들어오지 않음");
            if (Time.timeScale != 1f) Problem("게시판을 닫았는데 게임이 멈춰 있음");
            Log("   사냥 검사: " + levels + ", 정예 의뢰 " + before + " → " + (eliteBounty != null ? eliteBounty.have : 0)
                + ", 보상 " + claimed + "개 (은화 " + silver + " → " + GameState.Silver + ", 레벨 " + lv + " → " + Stats.Level + ", 장비 " + gear + " → " + GameState.Gear.Count + ")");
        }

        /// <summary>
        /// 등에 멘 무기와 강화 빛: 낀 무기를 +0 · +4 · +7 · +10 으로 바꿔 가며 가까이서 찍는다(+10 은 베기 궤적도).
        /// 끝나면 원래 단계로 돌린다.
        /// </summary>
        IEnumerator WeaponLook(PlayerController player)
        {
            var w = GameState.EquippedIn(GearSlot.Weapon);
            if (w == null)
            {
                Problem("무기 모습: 낀 무기가 없음");
                yield break;
            }
            if (WeaponVisual.I == null) Problem("무기 모습: WeaponVisual 이 없음");
            int original = w.plus;
            Vector2 p = player.transform.position;
            yield return MoveCamera(p + new Vector2(0f, 0.3f), 2.2f);
            foreach (int plus in new[] { 0, 4, 7, 10 })
            {
                w.plus = plus;
                GameState.GearChanged();
                if (WeaponVisual.Tier != WeaponVisual.TierOf(plus)) Problem("무기 모습: +" + plus + " 인데 빛의 단계가 " + WeaponVisual.Tier);
                HUD.ClearToasts();
                yield return Seconds(0.6f);
                if (plus == 10)
                {
                    WeaponVisual.OnSwing(p, Vector2.right, true);
                    WeaponVisual.OnHit(p + Vector2.right * 1.2f, true);
                    yield return Frames(3);
                }
                yield return Shot("weapon_plus" + plus);
            }
            yield return CameraRestore();
            w.plus = original;
            GameState.GearChanged();
        }

        /// <summary>몬스터 그림 확인: 배달부 옆에 종류마다 하나씩 세워 두고 가까이서 찍는다.</summary>
        IEnumerator MonsterLineup(PlayerController player)
        {
            Vector2 p = (Vector2)player.transform.position + new Vector2(0f, -3.5f);
            var made = new List<GameObject>();
            var kinds = new[] { EnemyKind.Rat, EnemyKind.Bat, EnemyKind.Mole, EnemyKind.Boss };
            for (int i = 0; i < kinds.Length; i++)
            {
                var go = Spawner.Enemy(p + new Vector2(-3f + i * 2f, 0f), kinds[i]);
                made.Add(go);
                var e = go.GetComponent<EnemyController>();
                e.enabled = false;
                // 땅속형도 몸이 보이게
                foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.name == "Body") sr.enabled = true;
                    else if (sr.name == "Mound") sr.enabled = false;
            }
            yield return MoveCamera(p + new Vector2(0f, 0.8f), 3.2f);
            yield return Frames(4);
            yield return Shot("monster_lineup");
            yield return CameraRestore();
            foreach (var go in made) Destroy(go);
            yield return Frames(2);
        }

        /// <summary>타이틀·일시정지·설정 화면 스크린샷.</summary>
        IEnumerator MenuShots()
        {
            GameMenu.ShowTitle();
            yield return Frames(3);
            yield return Shot("menu_title");
            GameMenu.ShowPause();
            yield return Frames(3);
            yield return Shot("menu_pause");
            GameMenu.ShowSettings();
            yield return Frames(3);
            yield return Shot("menu_settings");
            GameMenu.Hide();
            yield return Frames(3);
            if (Time.timeScale != 1f) Problem("메뉴를 닫았는데 게임이 멈춰 있음 (timeScale " + Time.timeScale + ")");
        }

        /// <summary>빠른 이동: 모든 정거장에서 타 보고, 내린 자리가 걸을 수 있는 곳인지 확인한다.</summary>
        IEnumerator RideRails(PlayerController player, int n)
        {
            var stops = RailStop.Ordered();
            if (stops.Count < 2) Problem("빠른 이동: 정거장이 " + stops.Count + "개뿐");
            foreach (var from in stops)
            {
                yield return Visit(player, from.transform.position);
                from.Interact(player);
                if (scenery && from == stops[0]) yield return Shot(n.ToString("00") + "_rail_menu");
                yield return Drain(0); // 첫 번째 목적지
                yield return Seconds(1.2f);
                var dest = stops[from == stops[0] ? 1 : 0];
                // 내린 직후 근처 그림자에게 떠밀릴 수 있으므로 2칸 안이면 도착으로 본다.
                if (Vector2.Distance(player.transform.position, dest.arrival) > 2f)
                    Problem("빠른 이동: " + from.stopName + " → " + dest.stopName + " 도착하지 않음 (" + (Vector2)player.transform.position + ")");
                else if (!Free(dest.arrival)) Problem("빠른 이동: " + dest.stopName + "의 내리는 자리가 막혀 있음");
                else Log("   빠른 이동 " + from.stopName + " → " + dest.stopName + " 확인");
                if (scenery && from == stops[0]) yield return Shot(n.ToString("00") + "_rail_arrive");
            }
        }

        static NpcTalk FirstTalk(NpcInteractable npc)
        {
            var def = GameData.GetNpc(npc.npcId);
            if (def?.talks == null) return null;
            foreach (var t in def.talks)
                if (GameState.Check(t.condition)) return t;
            return null;
        }

        static ClueInteractable FindClue(string flag)
        {
            foreach (var it in Interactable.All)
                if (it is ClueInteractable c && GameData.GetClue(c.clueId)?.setFlag == flag) return c;
            return null;
        }

        static string Last()
        {
            var l = GameData.EndingLines;
            foreach (var e in GameData.Endings)
                if (GameState.Check(e.condition)) { l = e.lines; break; }
            return l.Length > 0 ? l[l.Length - 1].text : "(없음)";
        }

        // ------------------------------------------------------------------ 이동 / 도달 검사

        const float Cell = 0.25f;
        const float PlayerRadius = 0.35f;
        static Rect Bounds => GameBootstrap.WorldBounds;
        bool[,] reach;
        int[,] dist;
        bool[,] openCells;
        bool openDirty = true;

        void MarkDirty()
        {
            openDirty = true;
            edgeChecked = false;
        }

        bool edgeChecked;
        readonly HashSet<string> edgeLeaks = new HashSet<string>();

        void EdgeLeak(Vector2 p)
        {
            string key = Mathf.RoundToInt(p.x / 4f) + "," + Mathf.RoundToInt(p.y / 4f);
            if (edgeLeaks.Add(key)) Problem("월드 가장자리에 막히지 않은 칸 @ " + p);
        }

        /// <summary>플레이어 위치에서 바닥 칸을 따라 퍼져 나가며 걸어서 닿는 칸을 구한다.</summary>
        bool Flood(Vector2 from)
        {
            int w = Mathf.CeilToInt(Bounds.width / Cell), h = Mathf.CeilToInt(Bounds.height / Cell);
            reach = new bool[w, h];
            dist = new int[w, h];
            // 걸을 수 있는 칸은 게임 상태(플래그·밤)가 바뀔 때만 다시 계산한다.
            if (openDirty || openCells == null)
            {
                openCells = new bool[w, h];
                for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    openCells[x, y] = Free(CellPos(x, y));
                openDirty = false;
            }
            var open = openCells;
            var q = new Queue<Vector2Int>();
            var s = ToCell(from);
            if (!open[s.x, s.y]) return false;
            // 월드 가장자리 칸이 열려 있으면 바깥 벽에 틈이 있다는 뜻이다(걸어서 월드 밖으로 나갈 수 있음).
            if (!edgeChecked)
            {
                edgeChecked = true;
                for (int x = 0; x < w; x++)
                    foreach (int y in new[] { 0, h - 1 })
                        if (open[x, y]) EdgeLeak(CellPos(x, y));
                for (int y = 0; y < h; y++)
                    foreach (int x in new[] { 0, w - 1 })
                        if (open[x, y]) EdgeLeak(CellPos(x, y));
            }
            reach[s.x, s.y] = true;
            q.Enqueue(s);
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                foreach (var d in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                {
                    var nb = c + d;
                    if (nb.x < 0 || nb.y < 0 || nb.x >= w || nb.y >= h || reach[nb.x, nb.y] || !open[nb.x, nb.y]) continue;
                    reach[nb.x, nb.y] = true;
                    dist[nb.x, nb.y] = dist[c.x, c.y] + 1;
                    q.Enqueue(nb);
                }
            }
            return true;
        }

        static bool Free(Vector2 p)
        {
            foreach (var col in Physics2D.OverlapCircleAll(p, PlayerRadius))
            {
                if (col.isTrigger) continue;
                if (col.GetComponentInParent<PlayerController>() != null) continue;
                if (col.GetComponentInParent<EnemyController>() != null) continue;
                return false;
            }
            return true;
        }

        static Vector2 CellPos(int x, int y) => new Vector2(Bounds.xMin + (x + 0.5f) * Cell, Bounds.yMin + (y + 0.5f) * Cell);

        static Vector2Int ToCell(Vector2 p) => new Vector2Int(
            Mathf.Clamp(Mathf.FloorToInt((p.x - Bounds.xMin) / Cell), 0, Mathf.CeilToInt(Bounds.width / Cell) - 1),
            Mathf.Clamp(Mathf.FloorToInt((p.y - Bounds.yMin) / Cell), 0, Mathf.CeilToInt(Bounds.height / Cell) - 1));

        /// <summary>target 을 조사할 수 있는(사거리 안) 걸어서 닿는 칸 중 가장 가까운 곳. 없으면 null.</summary>
        Vector2? Stand(Vector2 target, float range)
        {
            Vector2? best = null;
            float bestD = float.MaxValue;
            int r = Mathf.CeilToInt(range / Cell);
            var c = ToCell(target);
            for (int x = c.x - r; x <= c.x + r; x++)
            for (int y = c.y - r; y <= c.y + r; y++)
            {
                if (x < 0 || y < 0 || x >= reach.GetLength(0) || y >= reach.GetLength(1) || !reach[x, y]) continue;
                var p = CellPos(x, y);
                float d = Vector2.Distance(p, target);
                // 조금 여유를 두고(0.9배) 대상의 아래쪽(앞쪽)을 선호한다.
                float score = d + (p.y > target.y ? 0.3f : 0f);
                if (d <= range * 0.9f && score < bestD)
                {
                    bestD = score;
                    best = p;
                }
            }
            return best;
        }

        /// <summary>
        /// 땅속 이동이 벽을 뚫지 않는지: 마을과 숲 사이 생울타리(x 17) 건너편에 두더지를 두고, 플레이어는 마을 쪽에 세운다.
        /// 두더지는 플레이어를 향해 땅속으로 파고들지만 울타리를 넘어오면 안 된다.
        /// </summary>
        IEnumerator BurrowWallTest(PlayerController player)
        {
            yield return Visit(player, new Vector2(15.4f, 5f), false);
            var mole = Spawner.Enemy(new Vector2(19.6f, 5f), EnemyKind.Mole);
            float minX = float.MaxValue;
            float end = Time.time + 5f;
            while (Time.time < end && mole != null)
            {
                minX = Mathf.Min(minX, mole.transform.position.x);
                yield return null;
            }
            if (mole != null) Destroy(mole);
            if (minX < 17.3f) Problem("두더지가 땅속으로 생울타리를 뚫고 지나감 (x " + minX.ToString("0.0") + ")");
            else Log("   땅속 이동 벽 검사: 두더지가 생울타리 앞에서 멈춤 (가장 가까이 x " + minX.ToString("0.0") + ")");
        }

        readonly HashSet<string> enemyProblems = new HashSet<string>();

        void CheckEnemies(string label)
        {
            var b = Bounds;
            foreach (var e in new List<EnemyController>(EnemyController.Active))
            {
                if (e == null || e.IsBurrowed) continue;
                Vector2 p = e.transform.position;
                string where = !b.Contains(p) ? "월드 밖" : EnemyController.TerrainBlocked(p, 0.2f) ? "지형 속" : null;
                if (where == null) continue;
                // 같은 자리 문제는 한 번만 적는다.
                string key = e.displayName + Mathf.RoundToInt(p.x) + "," + Mathf.RoundToInt(p.y);
                if (enemyProblems.Add(key)) Problem("[" + label + "] " + e.displayName + "이(가) " + where + "에 있음 @ " + p);
            }
        }

        void CheckReach(string label)
        {
            var player = FindAnyObjectByType<PlayerController>();
            if (!Flood(GameBootstrapSpawn())) Problem("플레이어 시작 위치가 막혀 있음");
            // 지금 편지에 꼭 필요한 대상(창구, 받는 사람, 받는 사람을 밝히는 단서)에 닿지 못하면 문제.
            // 나머지는 아직 열리지 않은 지역일 수 있으므로 기록만 한다.
            var letter = LetterManager.Carrying;
            int count = 0;
            var locked = new List<string>();
            foreach (var it in new List<Interactable>(Interactable.All))
            {
                count++;
                if (Stand(it.transform.position, player.interactRange * it.rangeScale) != null) continue;
                // 받는 사람은 배달 조건(터널 뚫기 등)을 풀어야 닿을 수 있는 경우가 있으므로, 그때는 배달 직전에 다시 검사한다.
                bool needed = it is PostOfficeCounter
                              || (letter != null && it is NpcInteractable npc && npc.npcId == letter.recipientId && GameState.Check(letter.deliverCondition))
                              || (letter != null && it is ClueInteractable clue && GameData.GetClue(clue.clueId)?.setFlag == letter.revealFlag);
                if (needed) Problem("[" + label + "] 걸어서 닿을 수 없음: " + it.DisplayName + " @ " + (Vector2)it.transform.position);
                else locked.Add(it.DisplayName);
            }
            foreach (var c in Collectible.All)
            {
                if (c.Taken) continue;
                count++;
                if (Stand(c.transform.position, 0.8f) == null) locked.Add("우표@" + (Vector2)c.transform.position);
            }
            Log("   도달 검사(" + label + "): 대상 " + count + "개" + (locked.Count > 0 ? ", 아직 못 가는 곳: " + string.Join(", ", locked) : ""));
        }

        static Vector2 GameBootstrapSpawn() => new Vector2(0f, 2.8f);

        IEnumerator Visit(PlayerController player, Vector2 target, bool needRange = true)
        {
            // 지금 위치에서 걸어가는 거리를 잰다(막힌 칸에 서 있으면 시작 위치 기준).
            if (!Flood(player.transform.position)) Flood(GameBootstrapSpawn());
            var stand = needRange ? Stand(target, player.interactRange) : (Vector2?)target;
            var p = stand ?? target;
            var sc = ToCell(p);
            if (reach[sc.x, sc.y]) walked += dist[sc.x, sc.y] * Cell;
            var rb = player.GetComponent<Rigidbody2D>();
            rb.position = p;
            player.transform.position = p;
            yield return null;
            if (CameraFollow.I != null) CameraFollow.I.SnapToTarget();
            yield return Frames(3);
            CheckEnemies("이동 중");
        }

        // ------------------------------------------------------------------ 스크린샷

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            PngWriter.CaptureScreen(Path.Combine(outDir, (shotIndex++).ToString("000") + "_" + name + ".png"));
            yield return Frames(2);
        }

        IEnumerator VillageShot(string name)
        {
            yield return MoveCamera(new Vector2(-1f, 0.5f), 10f);
            yield return Shot(name);
            yield return CameraRestore();
        }

        IEnumerator Overviews(string tag)
        {
            foreach (var (pos, label) in new[] { (new Vector2(-1f, 0.5f), "village"), (new Vector2(34f, 0f), "forest"), (new Vector2(-33f, 1f), "coast"), (new Vector2(0f, 22f), "station"), (new Vector2(34f, 22f), "northforest"), (new Vector2(-33f, 22f), "northshore"), (new Vector2(69f, 22f), "pass"), (new Vector2(69f, 0f), "quarry") })
            {
                yield return MoveCamera(pos, 11.5f);
                yield return Shot("overview_" + tag + "_" + label);
            }
            yield return CameraRestore();
        }

        float savedSize;

        IEnumerator MoveCamera(Vector2 pos, float size)
        {
            var cam = UnityEngine.Camera.main;
            savedSize = cam.orthographicSize;
            if (CameraFollow.I != null) CameraFollow.I.enabled = false;
            cam.orthographicSize = size;
            cam.transform.position = new Vector3(pos.x, pos.y, -10f);
            var overlay = cam.transform.Find("NightOverlay");
            if (overlay != null) overlay.gameObject.SetActive(false);
            yield return Frames(3);
        }

        IEnumerator CameraRestore()
        {
            var cam = UnityEngine.Camera.main;
            cam.orthographicSize = savedSize;
            var overlay = cam.transform.Find("NightOverlay");
            if (overlay != null) overlay.gameObject.SetActive(true);
            if (CameraFollow.I != null)
            {
                CameraFollow.I.enabled = true;
                CameraFollow.I.SnapToTarget();
            }
            yield return Frames(2);
        }

        // ------------------------------------------------------------------ 공용

        IEnumerator Drain(int choice)
        {
            for (int guard = 0; guard < 400 && DialogueSystem.IsOpen; guard++)
            {
                DialogueSystem.I.Step(choice);
                yield return null;
            }
            yield return Frames(2);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Seconds(float s)
        {
            float end = Time.time + s;
            while (Time.time < end) yield return null;
        }

        void Log(string line)
        {
            report.AppendLine(line);
            Debug.Log("[playtest] " + line);
        }

        void Problem(string line)
        {
            problems++;
            Log("   [문제] " + line);
        }
    }
}
