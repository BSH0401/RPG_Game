using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    public enum EnemyAttack
    {
        /// <summary>예고선 방향으로 돌진.</summary>
        Lunge,
        /// <summary>예고 원 안을 내려찍기.</summary>
        Slam
    }

    /// <summary>
    /// 밤의 숲에 나오는 적. 모든 공격은 예고(빨간 표시) 후에만 피해를 주므로
    /// 플레이어는 예고를 보고 회피(Space)하거나 봉인끈(Q)으로 끊을 수 있다.
    /// 적의 차이는 무기 수가 아니라 이 컴포넌트의 수치와 공격 패턴으로 만든다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class EnemyController : MonoBehaviour
    {
        public static readonly List<EnemyController> Active = new List<EnemyController>();

        public string displayName = "그림자 들쥐";
        public bool isBoss;
        public string defeatFlag;
        public float moveSpeed = 2.2f;
        public float detectRange = 6f;
        public float attackRange = 2.4f;
        public float windupTime = 0.6f;
        public float lungeSpeed = 9f;
        public float lungeTime = 0.25f;
        public float recoverTime = 0.7f;
        public float slamRadius = 2.2f;
        public float hitRadius = 0.8f;
        public int lungeDamage = 1;
        public int slamDamage = 2;
        public EnemyAttack[] pattern = { EnemyAttack.Lunge };

        enum State { Idle, Chase, Windup, Lunge, Recover, Stunned, Hurt }

        public Health Health { get; private set; }
        public bool IsStunned => state == State.Stunned;

        Rigidbody2D rb;
        SpriteRenderer body;
        Color baseColor;
        SpriteRenderer telegraph;
        State state = State.Idle;
        float stateEnd;
        Vector2 attackDir;
        EnemyAttack currentAttack;
        int patternIndex;
        bool lungeHit;
        Vector2 velocity;
        float flashUntil;

        public void Init(SpriteRenderer bodyRenderer)
        {
            body = bodyRenderer;
            baseColor = body.color;

            var tg = new GameObject(name + "_Telegraph");
            telegraph = tg.AddComponent<SpriteRenderer>();
            telegraph.color = new Color(1f, 0.2f, 0.25f, 0.35f);
            telegraph.sortingOrder = -500; // 바닥 위, 캐릭터 아래
            tg.SetActive(false);
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void OnDestroy()
        {
            if (telegraph != null) Destroy(telegraph.gameObject);
        }

        void Update()
        {
            if (Time.timeScale == 0f) return;
            var player = PlayerController.I;
            if (player == null || DialogueSystem.IsOpen)
            {
                // 대화 중에는 공격하지 않는다.
                velocity = Vector2.zero;
                if (state == State.Windup || state == State.Lunge) EnterState(State.Recover, recoverTime);
                UpdateVisual();
                return;
            }

            Vector2 pos = transform.position;
            Vector2 toPlayer = (Vector2)player.transform.position - pos;
            float dist = toPlayer.magnitude;
            bool playerAlive = !player.Health.IsDead;

            switch (state)
            {
                case State.Idle:
                    velocity = Vector2.zero;
                    if (playerAlive && dist < detectRange) state = State.Chase;
                    break;

                case State.Chase:
                    if (!playerAlive || dist > detectRange * 1.6f)
                    {
                        state = State.Idle;
                        break;
                    }
                    if (dist <= attackRange) BeginWindup(toPlayer);
                    else velocity = toPlayer.normalized * moveSpeed;
                    break;

                case State.Windup:
                    velocity = Vector2.zero;
                    if (Time.time >= stateEnd) ExecuteAttack(dist, player);
                    break;

                case State.Lunge:
                    velocity = attackDir * lungeSpeed;
                    if (!lungeHit && dist < hitRadius)
                    {
                        lungeHit = true;
                        player.Health.TakeDamage(lungeDamage, pos);
                    }
                    if (Time.time >= stateEnd) EnterState(State.Recover, recoverTime);
                    break;

                case State.Recover:
                case State.Stunned:
                    velocity = Vector2.zero;
                    if (Time.time >= stateEnd) state = State.Chase;
                    break;

                case State.Hurt:
                    velocity = Vector2.Lerp(velocity, Vector2.zero, 10f * Time.deltaTime);
                    if (Time.time >= stateEnd) state = State.Chase;
                    break;
            }

            UpdateVisual();
        }

        void FixedUpdate() => rb.SetVelocity(velocity);

        void EnterState(State next, float duration)
        {
            state = next;
            stateEnd = Time.time + duration;
            if (telegraph != null) telegraph.gameObject.SetActive(false);
        }

        void BeginWindup(Vector2 toPlayer)
        {
            currentAttack = pattern.Length > 0 ? pattern[patternIndex++ % pattern.Length] : EnemyAttack.Lunge;
            attackDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;
            EnterState(State.Windup, windupTime);
            velocity = Vector2.zero;

            // 공격 예고: 돌진은 진행 방향의 띠, 내려찍기는 피해 범위 원.
            var t = telegraph.transform;
            Vector2 pos = transform.position;
            if (currentAttack == EnemyAttack.Lunge)
            {
                float length = lungeSpeed * lungeTime + hitRadius;
                telegraph.sprite = SpriteFactory.Square;
                t.position = pos + attackDir * (length * 0.5f);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(length, hitRadius * 1.2f, 1f);
            }
            else
            {
                telegraph.sprite = SpriteFactory.Circle;
                t.position = pos;
                t.rotation = Quaternion.identity;
                t.localScale = Vector3.one * slamRadius * 2f;
            }
            telegraph.gameObject.SetActive(true);
        }

        void ExecuteAttack(float dist, PlayerController player)
        {
            if (currentAttack == EnemyAttack.Lunge)
            {
                EnterState(State.Lunge, lungeTime);
                lungeHit = false;
            }
            else
            {
                if (dist <= slamRadius) player.Health.TakeDamage(slamDamage, transform.position);
                RingFx.Spawn(transform.position, slamRadius, new Color(1f, 0.4f, 0.4f, 0.9f));
                CameraFollow.Shake(0.2f);
                EnterState(State.Recover, recoverTime);
            }
        }

        public void TakeHit(int damage, Vector2 from) => Health.TakeDamage(damage, from);

        public void Stun(float duration)
        {
            if (Health.IsDead) return;
            // 보스는 봉인끈이 절반만 통한다.
            EnterState(State.Stunned, isBoss ? duration * 0.5f : duration);
            velocity = Vector2.zero;
        }

        void OnDamaged(Vector2 from)
        {
            flashUntil = Time.time + 0.1f;
            // 보스는 공격 중에 맞아도 멈추지 않는다(슈퍼아머).
            if (isBoss && (state == State.Windup || state == State.Lunge)) return;
            if (state == State.Stunned) return;
            EnterState(State.Hurt, 0.2f);
            velocity = ((Vector2)transform.position - from).normalized * (isBoss ? 2f : 6f);
        }

        void OnDied()
        {
            if (!string.IsNullOrEmpty(defeatFlag))
            {
                GameState.SetFlag(defeatFlag);
                GameState.Save();
            }
            if (isBoss) HUD.Toast(displayName + "을(를) 물리쳤다!");
            RingFx.Spawn(transform.position, isBoss ? 2.5f : 1f, new Color(0.8f, 0.7f, 1f, 0.9f), 0.5f);
            Destroy(gameObject);
        }

        void UpdateVisual()
        {
            if (body == null) return;
            Color c = baseColor;
            if (Time.time < flashUntil) c = Color.white;
            else if (state == State.Stunned) c = Color.Lerp(baseColor, new Color(0.5f, 0.9f, 1f), 0.6f);
            else if (state == State.Windup && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f) c = new Color(1f, 0.35f, 0.35f);
            body.color = c;
        }
    }
}
