using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 4방향 캐릭터 시트 애니메이션. 움직이는 방향을 보고 걷기 프레임을 돌린다.
    /// 플레이어처럼 바라보는 방향을 따로 정하는 경우 SetFacing 을 매 프레임 호출한다.
    /// </summary>
    public class SpriteAnimator : MonoBehaviour
    {
        public SpriteRenderer body;
        public CharacterSheet sheet;
        public float fps = 8f;

        int dir = CharacterSheet.Down;
        bool facingLocked;
        float walkTime;
        float actionUntil;
        int actionRow;
        /// <summary>이름 붙은 동작(칼 공격 등)을 재생 중이면 그 이름과 시작 시각·길이.</summary>
        string sequence;
        float sequenceStart, sequenceLength;
        Vector3 lastPos;
        /// <summary>거리로 맞추는 걸음의 진행(0~1, 한 바퀴 = 두 걸음).</summary>
        float stridePhase;
        /// <summary>발이 땅에 닿을 때 흙먼지(플레이어만).</summary>
        public bool dust;

        public void Init(SpriteRenderer renderer, CharacterSheet characterSheet)
        {
            body = renderer;
            sheet = characterSheet;
            lastPos = transform.position;
            body.sprite = sheet.Stand(dir);
            fps = sheet.WalkFps;
        }

        /// <summary>바라보는 방향을 직접 정한다(플레이어).</summary>
        public void SetFacing(Vector2 facing)
        {
            facingLocked = true;
            dir = DirOf(facing, dir);
        }

        /// <summary>대화할 때 상대를 바라보게 한다.</summary>
        public void FaceToward(Vector3 target) => dir = DirOf(target - transform.position, dir);

        /// <summary>공격 같은 한 동작 프레임을 잠깐 보여준다.</summary>
        public void PlayAction(int row, float duration)
        {
            if (sheet == null || row >= sheet.Rows) return;
            actionRow = row;
            actionUntil = Time.time + duration;
        }

        /// <summary>이름 붙은 동작을 duration 동안 한 번 재생한다. 시트에 없으면 false.</summary>
        public bool PlaySequence(string name, float duration)
        {
            if (sheet == null || !sheet.HasAction(name)) return false;
            sequence = name;
            sequenceStart = Time.time;
            sequenceLength = Mathf.Max(0.05f, duration);
            return true;
        }

        static int DirOf(Vector2 v, int fallback)
        {
            if (v.sqrMagnitude < 0.0001f) return fallback;
            if (Mathf.Abs(v.x) > Mathf.Abs(v.y)) return v.x < 0f ? CharacterSheet.Left : CharacterSheet.Right;
            return v.y < 0f ? CharacterSheet.Down : CharacterSheet.Up;
        }

        void LateUpdate()
        {
            if (body == null || sheet == null || Time.deltaTime <= 0f) return;
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            bool moving = delta.magnitude / Time.deltaTime > 0.3f;
            if (moving && !facingLocked) dir = DirOf(delta, dir);

            if (sequence != null)
            {
                float t = (Time.time - sequenceStart) / sequenceLength;
                if (t < 1f)
                {
                    body.sprite = sheet.Action(sequence, dir, t);
                    return;
                }
                sequence = null;
            }
            if (Time.time < actionUntil)
            {
                body.sprite = sheet.Get(dir, actionRow);
                return;
            }
            if (moving && sheet.WalkStride > 0f)
            {
                // 움직인 거리만큼 발을 내딛는다. 빠르면 달리기 동작.
                float speed = delta.magnitude / Time.deltaTime;
                bool run = speed > sheet.RunAbove && sheet.HasAction("run");
                float before = stridePhase;
                stridePhase = Mathf.Repeat(stridePhase + delta.magnitude / (run ? sheet.RunStride : sheet.WalkStride), 1f);
                body.sprite = run ? sheet.Action("run", dir, stridePhase) : sheet.Get(dir, Mathf.Min((int)(stridePhase * sheet.WalkFrames), sheet.WalkFrames - 1));
                // 두 걸음마다(0 과 0.5 를 지날 때) 발밑에 작은 흙먼지
                if (dust && run && (stridePhase < before || (before < 0.5f && stridePhase >= 0.5f))) Dust();
            }
            else if (moving)
            {
                walkTime += Time.deltaTime;
                body.sprite = sheet.Get(dir, (int)(walkTime * fps) % sheet.WalkFrames);
            }
            else
            {
                walkTime = 0f;
                stridePhase = 0f;
                body.sprite = sheet.Stand(dir);
            }
        }

        void Dust()
        {
            if (!GameAssets.Available) return;
            Vector2 feet = body.transform.position;
            FrameAnimator.PlayOnce(GameAssets.SmokeFrames, feet + new Vector2(Random.Range(-0.1f, 0.1f), 0.05f), 20f, new Color(0.75f, 0.72f, 0.8f, 0.45f), -430, 0.45f);
        }
    }

    /// <summary>프레임 묶음을 차례로 보여준다(보스 대기 모션, 연기 효과 등).</summary>
    public class FrameAnimator : MonoBehaviour
    {
        public Sprite[] frames;
        public float fps = 8f;
        public bool loop = true;
        public bool destroyAtEnd;

        SpriteRenderer sr;
        float time;

        public static GameObject PlayOnce(Sprite[] frames, Vector2 pos, float fps, Color color, int order = 600, float scale = 1f)
        {
            if (frames == null || frames.Length == 0) return null;
            var go = new GameObject("Fx");
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = frames[0];
            renderer.color = color;
            renderer.sortingOrder = order;
            var anim = go.AddComponent<FrameAnimator>();
            anim.frames = frames;
            anim.fps = fps;
            anim.loop = false;
            anim.destroyAtEnd = true;
            return go;
        }

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void Update()
        {
            if (frames == null || frames.Length == 0 || sr == null) return;
            time += Time.deltaTime;
            int i = (int)(time * fps);
            if (i >= frames.Length)
            {
                if (destroyAtEnd)
                {
                    Destroy(gameObject);
                    return;
                }
                i = loop ? i % frames.Length : frames.Length - 1;
            }
            sr.sprite = frames[i];
        }
    }
}
