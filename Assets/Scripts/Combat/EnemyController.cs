using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    public enum EnemyAttack
    {
        /// <summary>예고선 방향으로 돌진. 막을 수 있음.</summary>
        Lunge,
        /// <summary>예고 원 안을 내려찍기. 막을 수 없음(회피·받아치기).</summary>
        Slam,
        /// <summary>먹물 구슬을 쏜다. 막을 수 있고, 받아치면 되돌아간다.</summary>
        Shoot,
        /// <summary>부채꼴로 여러 발을 쏜다(보스).</summary>
        Spread,
        /// <summary>플레이어 발밑과 주변에 촉수가 차례로 내리꽂힌다. 주황 원, 막을 수 없음(보스).</summary>
        Rain,
        /// <summary>물속으로 잠겨 다가온 뒤 튀어나오며 내려찍는다(보스).</summary>
        Dive
    }

    /// <summary>
    /// 밤의 숲에 나오는 적. 모든 공격은 예고 표시 뒤에만 피해를 준다.
    ///  - 빨간 예고: 막을 수 있음 / 주황 예고: 막을 수 없음
    /// 적의 차이는 수치와 행동 옵션(거리 유지, 땅속 숨기)과 공격 패턴으로 만든다. 종류별 수치는 Spawner.Enemy.
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
        public int shootDamage = 1;
        public EnemyAttack[] pattern = { EnemyAttack.Lunge };

        [Header("원거리형: 플레이어와 이 거리를 유지하며 쏜다 (0 이면 사용 안 함)")]
        public float keepDistance;

        [Header("땅속형: 땅속으로 숨어 다가온 뒤 튀어나와 내려찍는다")]
        public bool burrows;
        public float burrowSpeed = 3.2f;

        [Header("보스 전용")]
        public int spreadCount = 3;
        public int rainCount = 3;
        public float diveTime = 1.4f;
        /// <summary>체력이 절반 아래로 떨어지면 빨라지고 공격이 늘어난다.</summary>
        public bool enrages;
        public Sprite[] idleFrames;
        public Sprite[] shootFrames;
        public FrameAnimator bodyAnim;
        bool enraged;

        enum State { Idle, Chase, Windup, Lunge, Recover, Stunned, Hurt, Burrowed }

        public Health Health { get; private set; }
        public bool IsStunned => state == State.Stunned;
        public bool IsBurrowed => state == State.Burrowed;

        Rigidbody2D rb;
        Collider2D col;
        SpriteRenderer body;
        SpriteRenderer mound;
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
        float strafeSign = 1f;
        float pendingKnock = 6f;

        public void Init(SpriteRenderer bodyRenderer)
        {
            body = bodyRenderer;
            baseColor = body.color;

            var tg = new GameObject(name + "_Telegraph");
            telegraph = tg.AddComponent<SpriteRenderer>();
            telegraph.color = new Color(1f, 0.2f, 0.25f, 0.35f);
            telegraph.sortingOrder = -500; // 바닥 위, 캐릭터 아래
            tg.SetActive(false);

            bool dives = System.Array.IndexOf(pattern, EnemyAttack.Dive) >= 0;
            if (burrows || dives)
            {
                // 땅속(물속)에 있을 때 보이는 흙더미 / 먹물 웅덩이
                var m = new GameObject("Mound");
                m.transform.SetParent(transform, false);
                m.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                if (dives) m.transform.localScale = Vector3.one * 2.5f;
                mound = m.AddComponent<SpriteRenderer>();
                mound.sprite = dives ? Art.InkPuddle : Art.Mound;
                mound.sortingOrder = -440;
                mound.enabled = false;
                if (burrows) EnterBurrow();
            }
            strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
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
                    if (keepDistance > 0f) ChaseAtRange(toPlayer, dist);
                    else if (dist <= attackRange) BeginWindup(toPlayer);
                    else velocity = toPlayer.normalized * moveSpeed;
                    break;

                case State.Burrowed:
                    // 땅속: 맞지 않고, 흙더미만 보이며 플레이어 발밑으로 파고든다.
                    if (!playerAlive || dist > detectRange * 1.6f)
                    {
                        velocity = Vector2.zero;
                        break;
                    }
                    velocity = toPlayer.normalized * burrowSpeed;
                    if (dist < 0.9f || Time.time >= stateEnd) Emerge(toPlayer);
                    break;

                case State.Windup:
                    velocity = Vector2.zero;
                    if (Time.time >= stateEnd) ExecuteAttack(dist, player, toPlayer);
                    break;

                case State.Lunge:
                    velocity = attackDir * lungeSpeed;
                    if (!lungeHit && dist < hitRadius)
                    {
                        lungeHit = true;
                        player.ReceiveAttack(this, lungeDamage, pos, false);
                    }
                    if (Time.time >= stateEnd) EnterState(State.Recover, recoverTime);
                    break;

                case State.Recover:
                case State.Stunned:
                    velocity = Vector2.zero;
                    if (Time.time >= stateEnd)
                    {
                        if (burrows) EnterBurrow();
                        else state = State.Chase;
                    }
                    break;

                case State.Hurt:
                    velocity = Vector2.Lerp(velocity, Vector2.zero, 10f * Time.deltaTime);
                    if (Time.time >= stateEnd) state = State.Chase;
                    break;
            }

            UpdateVisual();
        }

        /// <summary>원거리형: 적당한 거리를 두고 옆으로 돌며 쏜다.</summary>
        void ChaseAtRange(Vector2 toPlayer, float dist)
        {
            Vector2 dir = toPlayer.normalized;
            Vector2 side = new Vector2(-dir.y, dir.x) * strafeSign;
            if (dist > keepDistance + 1.2f) velocity = dir * moveSpeed;
            else if (dist < keepDistance - 1.2f) velocity = -dir * moveSpeed;
            else velocity = side * moveSpeed * 0.6f;

            if (dist <= attackRange && Random.value < Time.deltaTime * 1.2f)
            {
                strafeSign = -strafeSign;
                BeginWindup(toPlayer);
            }
        }

        void FixedUpdate() => rb.SetVelocity(velocity);

        void EnterState(State next, float duration)
        {
            state = next;
            stateEnd = Time.time + duration;
            if (telegraph != null) telegraph.gameObject.SetActive(false);
        }

        void EnterBurrow(float duration = 3.5f)
        {
            EnterState(State.Burrowed, duration);
            Health.Invulnerable = true;
            if (col != null) col.enabled = false;
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, transform.position, 16f, new Color(0.7f, 0.6f, 0.5f), 600, 1f);
        }

        void Emerge(Vector2 toPlayer)
        {
            Health.Invulnerable = false;
            if (col != null) col.enabled = true;
            BeginWindup(toPlayer, EnemyAttack.Slam);
        }

        void BeginWindup(Vector2 toPlayer, EnemyAttack? forced = null)
        {
            currentAttack = forced ?? (pattern.Length > 0 ? pattern[patternIndex++ % pattern.Length] : EnemyAttack.Lunge);
            attackDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.down;
            EnterState(State.Windup, windupTime);
            velocity = Vector2.zero;

            // 촉수·잠수는 예고를 따로 그린다(촉수는 떨어질 자리마다 원, 잠수는 웅덩이).
            if (currentAttack == EnemyAttack.Rain || currentAttack == EnemyAttack.Dive)
            {
                stateEnd = Time.time + windupTime * 0.6f;
                return;
            }

            // 공격 예고: 돌진·사격은 진행 방향의 띠, 내려찍기는 피해 범위 원.
            // 빨간색 = 막을 수 있음, 주황색 = 막을 수 없음(피하거나 받아치기)
            var t = telegraph.transform;
            Vector2 pos = transform.position;
            telegraph.color = currentAttack == EnemyAttack.Slam ? new Color(1f, 0.55f, 0.05f, 0.42f) : new Color(1f, 0.2f, 0.25f, 0.35f);
            if (currentAttack == EnemyAttack.Slam)
            {
                telegraph.sprite = SpriteFactory.Circle;
                t.position = pos;
                t.rotation = Quaternion.identity;
                t.localScale = Vector3.one * slamRadius * 2f;
            }
            else
            {
                bool shot = currentAttack == EnemyAttack.Shoot || currentAttack == EnemyAttack.Spread;
                float length = shot ? 6f : lungeSpeed * lungeTime + hitRadius;
                float width = currentAttack == EnemyAttack.Spread ? 1.4f : shot ? 0.3f : hitRadius * 1.2f;
                telegraph.sprite = SpriteFactory.Square;
                t.position = pos + attackDir * (length * 0.5f);
                t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg);
                t.localScale = new Vector3(length, width, 1f);
            }
            telegraph.gameObject.SetActive(true);
        }

        void ExecuteAttack(float dist, PlayerController player, Vector2 toPlayer)
        {
            switch (currentAttack)
            {
                case EnemyAttack.Lunge:
                    EnterState(State.Lunge, lungeTime);
                    lungeHit = false;
                    break;

                case EnemyAttack.Shoot:
                    // 예고한 방향 그대로 쏜다(예고를 보고 피할 수 있게).
                    EnemyProjectile.Spawn(this, (Vector2)transform.position + attackDir * 0.5f, attackDir, shootDamage);
                    Sound.Play("Attack", 0.4f);
                    EnterState(State.Recover, recoverTime);
                    break;

                case EnemyAttack.Spread:
                {
                    float baseAngle = Mathf.Atan2(attackDir.y, attackDir.x) * Mathf.Rad2Deg;
                    for (int i = 0; i < spreadCount; i++)
                    {
                        float a = (baseAngle + (i - (spreadCount - 1) * 0.5f) * 16f) * Mathf.Deg2Rad;
                        var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        EnemyProjectile.Spawn(this, (Vector2)transform.position + d * 1f, d, shootDamage);
                    }
                    Sound.Play("Attack", 0.6f);
                    EnterState(State.Recover, recoverTime);
                    break;
                }

                case EnemyAttack.Rain:
                {
                    // 첫 촉수는 지금 플레이어가 있는 자리, 나머지는 주변. 차례로 떨어진다.
                    Vector2 target = player.transform.position;
                    for (int i = 0; i < rainCount; i++)
                    {
                        Vector2 p = i == 0 ? target : target + Random.insideUnitCircle.normalized * Random.Range(1.4f, 2.8f);
                        GroundHazard.Spawn(p, 1.1f, 0.9f + i * 0.15f, 1);
                    }
                    EnterState(State.Recover, recoverTime * 0.6f);
                    break;
                }

                case EnemyAttack.Dive:
                    EnterBurrow(diveTime);
                    Sound.Play("BossSlam", 0.5f);
                    break;

                default:
                    // 내려찍기는 막을 수 없다(받아치기나 회피만 통한다).
                    if (dist <= slamRadius) player.ReceiveAttack(this, slamDamage, transform.position, true);
                    RingFx.Spawn(transform.position, slamRadius, new Color(1f, 0.4f, 0.4f, 0.9f));
                    CameraFollow.Shake(isBoss ? 0.2f : 0.1f);
                    Sound.Play("BossSlam", isBoss ? 1f : 0.6f);
                    // 땅속형은 튀어나온 뒤 한동안 무방비 상태가 된다(때릴 기회).
                    EnterState(State.Recover, burrows ? 1.5f : recoverTime);
                    break;
            }
        }

        /// <summary>플레이어의 공격에 맞음. knock = 밀려나는 세기.</summary>
        public void TakeHit(int damage, Vector2 from, float knock = 6f)
        {
            if (IsBurrowed) return;
            pendingKnock = knock;
            if (Health.TakeDamage(damage, from))
                HUD.Popup((Vector2)transform.position + Vector2.up * (isBoss ? 2.2f : 1f), "-" + damage, damage >= 2 ? new Color(1f, 0.85f, 0.3f) : Color.white);
        }

        /// <summary>공격이 막혔을 때 튕겨 나간다.</summary>
        public void Recoil(Vector2 awayFrom)
        {
            if (Health.IsDead || state == State.Stunned || IsBurrowed) return;
            EnterState(State.Hurt, isBoss ? 0.3f : 0.5f); // Hurt 상태는 밀려난 속도가 서서히 줄어든다
            velocity = ((Vector2)transform.position - awayFrom).normalized * (isBoss ? 2f : 5f);
        }

        public void Stun(float duration)
        {
            if (Health.IsDead || IsBurrowed) return;
            // 보스는 봉인끈이 절반만 통한다.
            EnterState(State.Stunned, isBoss ? duration * 0.5f : duration);
            velocity = Vector2.zero;
        }

        void OnDamaged(Vector2 from)
        {
            flashUntil = Time.time + 0.1f;
            Sound.Play("EnemyHit", 0.8f);
            if (enrages && !enraged && Health.Current * 2 <= Health.Max) Enrage();
            // 보스는 공격 중에 맞아도 멈추지 않는다(슈퍼아머).
            if (isBoss && (state == State.Windup || state == State.Lunge)) return;
            if (state == State.Stunned || (burrows && state == State.Recover)) return;
            EnterState(State.Hurt, 0.2f);
            velocity = ((Vector2)transform.position - from).normalized * (isBoss ? pendingKnock * 0.33f : pendingKnock);
        }

        void Enrage()
        {
            enraged = true;
            windupTime *= 0.7f;
            recoverTime *= 0.7f;
            moveSpeed *= 1.25f;
            spreadCount += 2;
            rainCount += 2;
            baseColor = Color.Lerp(baseColor, new Color(1f, 0.3f, 0.4f), 0.4f);
            HUD.Toast(Josa.Ga(displayName) + " 분노했다! 공격이 빨라진다.");
            RingFx.Spawn(transform.position, 3f, new Color(1f, 0.3f, 0.4f, 0.9f), 0.5f);
            CameraFollow.Shake(0.3f, 0.2f);
            Juice.HitStop(0.15f);
        }

        void OnDied()
        {
            if (!string.IsNullOrEmpty(defeatFlag))
            {
                GameState.SetFlag(defeatFlag);
                GameState.Save();
            }
            if (isBoss) HUD.Toast(Josa.Eul(displayName) + " 물리쳤다!");
            RingFx.Spawn(transform.position, isBoss ? 2.5f : 1f, new Color(0.8f, 0.7f, 1f, 0.9f), 0.5f);
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, transform.position, 16f, Color.white, 600, isBoss ? 3f : 1.5f);
            Sound.Play("EnemyDie");
            Juice.HitStop(isBoss ? 0.25f : 0.05f);
            Destroy(gameObject);
        }

        void UpdateVisual()
        {
            if (body == null) return;
            if (bodyAnim != null && idleFrames != null)
            {
                var wanted = state == State.Windup && currentAttack == EnemyAttack.Spread && shootFrames != null ? shootFrames : idleFrames;
                if (bodyAnim.frames != wanted) bodyAnim.frames = wanted;
            }
            bool hidden = IsBurrowed;
            body.enabled = !hidden;
            if (mound != null)
            {
                mound.enabled = hidden;
                if (hidden) mound.transform.localScale = new Vector3(1f + 0.1f * Mathf.Sin(Time.time * 18f), 1f, 1f);
            }

            Color c = baseColor;
            if (Time.time < flashUntil) c = new Color(1f, 1f, 1f, 0.35f);
            else if (state == State.Stunned) c = Color.Lerp(baseColor, new Color(0.5f, 0.9f, 1f), 0.6f);
            else if (state == State.Windup && Mathf.Repeat(Time.time * 10f, 1f) < 0.5f) c = new Color(1f, 0.35f, 0.35f);
            body.color = c;
        }
    }
}
