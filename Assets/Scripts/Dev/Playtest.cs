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

            if (scenery) yield return Overviews("start");
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

                if (!LetterManager.IsRecipientRevealed(letter))
                {
                    var clue = FindClue(letter.revealFlag);
                    if (clue == null) Problem(letter.id + ": 받는 사람을 밝힐 단서(" + letter.revealFlag + ")가 월드에 없다");
                    else
                    {
                        yield return Visit(player, clue.transform.position);
                        clue.Interact(player);
                        if (scenery) yield return Shot(n.ToString("00") + "_clue_" + clue.clueId);
                        yield return Drain(choice);
                        if (!LetterManager.IsRecipientRevealed(letter)) Problem(letter.id + ": 단서를 조사해도 받는 사람이 밝혀지지 않음");
                    }
                }

                if (!GameState.Check(letter.deliverCondition))
                {
                    EnemyController boss = null;
                    foreach (var e in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                        if (e.isBoss) boss = e;
                    if (boss == null) Problem(letter.id + ": 배달 조건(" + letter.deliverCondition + ")을 풀 보스가 없음");
                    else
                    {
                        yield return Visit(player, boss.transform.position + new Vector3(0f, -3f, 0f), false);
                        yield return Seconds(1.2f);
                        if (scenery) yield return Shot(n.ToString("00") + "_boss_" + boss.displayName);
                        boss.GetComponent<Health>().TakeDamage(999, player.transform.position);
                        yield return Seconds(2f);
                    }
                    if (!GameState.Check(letter.deliverCondition)) Problem(letter.id + ": 보스를 쓰러뜨려도 배달 조건이 풀리지 않음");
                }

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
        static readonly Rect Bounds = new Rect(-50f, -11f, 102f, 22f);
        bool[,] reach;
        int[,] dist;

        /// <summary>플레이어 위치에서 바닥 칸을 따라 퍼져 나가며 걸어서 닿는 칸을 구한다.</summary>
        bool Flood(Vector2 from)
        {
            int w = Mathf.CeilToInt(Bounds.width / Cell), h = Mathf.CeilToInt(Bounds.height / Cell);
            reach = new bool[w, h];
            dist = new int[w, h];
            var open = new bool[w, h];
            for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                open[x, y] = Free(CellPos(x, y));
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
                bool needed = it is PostOfficeCounter
                              || (letter != null && it is NpcInteractable npc && npc.npcId == letter.recipientId)
                              || (letter != null && it is ClueInteractable clue && GameData.GetClue(clue.clueId)?.setFlag == letter.revealFlag);
                if (needed) Problem("[" + label + "] 걸어서 닿을 수 없음: " + it.DisplayName + " @ " + (Vector2)it.transform.position);
                else locked.Add(it.DisplayName);
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
            foreach (var (pos, label) in new[] { (new Vector2(-1f, 0.5f), "village"), (new Vector2(34f, 0f), "forest"), (new Vector2(-33f, 1f), "coast") })
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
