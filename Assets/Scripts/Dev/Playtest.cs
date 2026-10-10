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
                if (scenery) yield return DoRequests(player, n);
                if (scenery) yield return TalkToEveryone(player, "「" + letter.title + "」 배달 후");
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
                yield return Visit(player, foes[0].transform.position + new Vector3(0f, -3f, 0f), false);
                yield return Seconds(1.2f);
                if (scenery) yield return Shot(n.ToString("00") + "_fight_" + term.Replace(':', '-'));
                foreach (var f in foes)
                    if (f != null) f.GetComponent<Health>().TakeDamage(999, player.transform.position);
                yield return Seconds(2f);
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

        void MarkDirty() => openDirty = true;

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
