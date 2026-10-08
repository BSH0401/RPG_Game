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
        Vector3 lastPos;

        public void Init(SpriteRenderer renderer, CharacterSheet characterSheet)
        {
            body = renderer;
            sheet = characterSheet;
            lastPos = transform.position;
            body.sprite = sheet.Get(dir, 0);
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

            if (Time.time < actionUntil)
            {
                body.sprite = sheet.Get(dir, actionRow);
                return;
            }
            if (moving)
            {
                walkTime += Time.deltaTime;
                body.sprite = sheet.Get(dir, (int)(walkTime * fps) % sheet.WalkFrames);
            }
            else
            {
                walkTime = 0f;
                body.sprite = sheet.Get(dir, 0);
            }
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

        public static void PlayOnce(Sprite[] frames, Vector2 pos, float fps, Color color, int order = 600, float scale = 1f)
        {
            if (frames == null || frames.Length == 0) return;
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
