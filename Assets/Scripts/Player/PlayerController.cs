using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    public enum AttackResult { Hit, Blocked, Parried, Ignored }

    /// <summary>
    /// 배달부: 이동, 3연타 공격, 회피, 우편가방 막기·받아치기, 편지 도구(Q, 부품에 따라 효과가 바뀜), 조사·대화.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I { get; private set; }

        [Header("이동")]
        public float moveSpeed = 5f;
        public float dodgeSpeed = 13f;
        public float dodgeTime = 0.18f;
        public float dodgeCooldown = 0.5f;

        [Header("공격 (3연타: 마지막 타가 강타)")]
        public float attackCooldown = 0.28f;
        public float comboWindow = 0.55f;
        public float attackReach = 0.85f;
        public float attackRadius = 0.75f;
        public int attackDamage = 1;
        public int finisherDamage = 2;

        [Header("우편가방 막기")]
        public float guardMoveFactor = 0.4f;
        /// <summary>막기를 누른 직후 이 시간 안에 맞으면 받아치기.</summary>
        public float parryWindow = 0.2f;
        public float parryStun = 1.6f;

        [Header("편지 도구")]
        public float bindRadius = 3f;
        public float bindStunTime = 2.2f;
        public float dashDistance = 5f;
        public float dashTime = 0.22f;

        [Header("조사")]
        public float interactRange = 1.5f;

        public Health Health { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.down;
        public Interactable CurrentTarget { get; private set; }
        public float ToolCooldownRemaining => Mathf.Max(0f, toolReadyTime - Time.time);
        public bool IsGuarding { get; private set; }
        /// <summary>배달부 주변을 밝히는 손등불(반딧불이 병이 있으면 커진다).</summary>
        public SpriteRenderer lightGlow;
        public Vector3 RespawnPoint { get; set; }

        // 장비 효과 (items.json 의 effect 값, 퍼센트)
        static float DodgeBonus => Inventory.EffectSum("dodge") / 100f;
        // 끼운 편지 도구 부품을 강화했으면 그만큼 더한다.
        static float ToolBonus => (Inventory.EffectSum("tool") + Upgrade.Bonus(Inventory.CurrentToolPart, Upgrade.Level(Inventory.CurrentToolPart))) / 100f;

        readonly List<Collider2D> hits = new List<Collider2D>();
        readonly HashSet<EnemyController> dashHit = new HashSet<EnemyController>();
        Rigidbody2D rb;
        Collider2D ownCollider;
        SpriteRenderer body;
        SpriteRenderer slash;
        SpriteRenderer guardIcon;
        SpriteAnimator animator;
        Sprite[] slashFrames;
        float slashAngleOffset = -90f;
        const float SlashDuration = 0.15f;
        Color bodyColor;
        Vector2 moveInput;
        Vector2 dodgeDir;
        Vector2 dashDir;
        Vector2 knockback;
        float dodgeEnd, nextDodge, nextAttack, toolReadyTime, hurtUntil, safeUntil, knockUntil, slashUntil, dashEnd, guardStart, lastAttackTime;
        int comboStep;
        bool IsDodging => Time.time < dodgeEnd;
        bool IsDashing => Time.time < dashEnd;

        public void Init(SpriteRenderer bodyRenderer, SpriteRenderer slashRenderer)
        {
            body = bodyRenderer;
            bodyColor = body.color;
            slash = slashRenderer;
            slash.enabled = false;
            animator = GetComponent<SpriteAnimator>();
            slashFrames = GameAssets.Available ? GameAssets.SlashFrames : null;
            if (slashFrames != null)
            {
                // 에셋의 베기 그림은 아래쪽으로 휜 반달이라 회전 기준이 반대다.
                slash.sprite = slashFrames[0];
                slashAngleOffset = 90f;
            }

            // 막을 때 앞에 드는 우편가방
            var g = new GameObject("GuardBag");
            g.transform.SetParent(transform, false);
            guardIcon = g.AddComponent<SpriteRenderer>();
            guardIcon.sprite = GameAssets.Available ? GameAssets.Region("Items/Bag", 0, 0, 16, 16, 0.5f, 0.5f) : null;
            if (guardIcon.sprite == null) guardIcon.sprite = Art.Chest(false);
            guardIcon.sortingOrder = 700;
            guardIcon.enabled = false;
        }

        void Awake()
        {
            I = this;
            rb = GetComponent<Rigidbody2D>();
            ownCollider = GetComponent<Collider2D>();
            Health = GetComponent<Health>();
            Health.SetMax(GameState.MaxHp, true);
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void Start() => GameState.Changed += SyncMaxHp;

        void OnDestroy()
        {
            GameState.Changed -= SyncMaxHp;
            if (I == this) I = null;
        }

        void SyncMaxHp()
        {
            if (Health.Max != GameState.MaxHp) Health.SetMax(GameState.MaxHp, true);
        }

        void Update()
        {
            UpdateVisual();
            bool busy = Time.timeScale == 0f || DialogueSystem.IsOpen || Health.IsDead
                        || Time.frameCount == DialogueSystem.LastClosedFrame || Time.frameCount == GameMenu.LastClosedFrame
                        || Time.frameCount == UpgradeMenu.LastClosedFrame;
            if (busy)
            {
                moveInput = Vector2.zero;
                IsGuarding = false;
                return;
            }

            moveInput = GameInput.Move;
            if (moveInput.sqrMagnitude > 0.01f && !IsGuarding) Facing = moveInput.normalized;
            if (animator != null) animator.SetFacing(Facing);

            // 막기: 누르고 있는 동안 앞에서 오는 공격을 막는다. 누른 직후라면 받아치기.
            bool wantGuard = GameInput.GuardHeld && !IsDodging && !IsDashing;
            if (wantGuard && !IsGuarding) guardStart = Time.time;
            IsGuarding = wantGuard;

            if (!IsGuarding)
            {
                if (GameInput.DodgePressed && Time.time >= nextDodge && !IsDashing) StartDodge();
                if (GameInput.AttackPressed && Time.time >= nextAttack && !IsDodging && !IsDashing) Attack();
                if (GameInput.ToolPressed && Time.time >= toolReadyTime && !IsDashing) UseTool();
            }
            if (GameInput.SwitchToolPressed) Inventory.CycleToolPart();
            if (GameInput.UseItemPressed) Inventory.UseConsumable(this);

            CurrentTarget = FindTarget();
            if (GameInput.InteractPressed && CurrentTarget != null) CurrentTarget.Interact(this);

            Health.Invulnerable = IsDodging || IsDashing || Time.time < hurtUntil || Time.time < safeUntil;
        }

        void FixedUpdate()
        {
            Vector2 v;
            if (Time.time < knockUntil) v = knockback;
            else if (IsDashing)
            {
                v = dashDir * (dashDistance / dashTime);
                DashHits();
            }
            else if (IsDodging) v = dodgeDir * dodgeSpeed;
            else v = moveInput * moveSpeed * (IsGuarding ? guardMoveFactor : 1f);
            rb.SetVelocity(v);
        }

        Interactable FindTarget()
        {
            Interactable best = null;
            float bestDist = float.MaxValue;
            Vector2 pos = transform.position;
            foreach (var it in Interactable.All)
            {
                float d = Vector2.Distance(pos, it.transform.position);
                if (d <= interactRange * it.rangeScale && d < bestDist)
                {
                    best = it;
                    bestDist = d;
                }
            }
            return best;
        }

        void StartDodge()
        {
            dodgeDir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : Facing;
            dodgeEnd = Time.time + dodgeTime * (1f + DodgeBonus);
            nextDodge = dodgeEnd + dodgeCooldown;
        }

        // ------------------------------------------------------------------ 공격 (3연타)

        void Attack()
        {
            comboStep = Time.time - lastAttackTime <= comboWindow ? comboStep % 3 + 1 : 1;
            lastAttackTime = Time.time;
            bool finisher = comboStep == 3;
            nextAttack = Time.time + attackCooldown * (finisher ? 1.6f : 1f);

            float reach = finisher ? attackReach * 1.15f : attackReach;
            float radius = finisher ? attackRadius * 1.3f : attackRadius;
            Vector2 center = (Vector2)transform.position + Facing * reach;

            hits.Clear();
            Physics2D.OverlapCircle(center, radius, new ContactFilter2D().NoFilter(), hits);
            var hitSet = new HashSet<EnemyController>();
            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy != null && hitSet.Add(enemy))
                    enemy.TakeHit(finisher ? finisherDamage : attackDamage, transform.position, finisher ? 9f : 6f);
            }
            if (hitSet.Count > 0 && finisher)
            {
                Juice.HitStop(0.06f);
                CameraFollow.Shake(0.12f, 0.12f);
            }

            // 바라보는 방향으로 반달 모양 베기 효과 (강타는 크게)
            slash.transform.position = (Vector2)transform.position + Facing * (slashFrames != null ? 0.6f : 0.15f);
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg + slashAngleOffset);
            slash.transform.localScale = Vector3.one * (finisher ? 1.45f : 1f);
            slash.color = finisher ? new Color(1f, 0.92f, 0.6f) : Color.white;
            slash.enabled = true;
            slashUntil = Time.time + SlashDuration;
            if (animator != null && animator.sheet != null && animator.sheet.HasAttackRow) animator.PlayAction(4, 0.2f);
            Sound.Play("Attack", finisher ? 1f : 0.7f);
        }

        // ------------------------------------------------------------------ 맞을 때: 막기·받아치기

        /// <summary>
        /// 적의 공격이 닿았을 때 부른다. 앞을 보고 막는 중이면 막고,
        /// 막기를 누른 직후(받아치기 시간)라면 적을 비틀거리게 한다. unblockable 공격은 받아치기로만 막힌다.
        /// </summary>
        public AttackResult ReceiveAttack(EnemyController source, int damage, Vector2 from, bool unblockable)
        {
            if (Health.IsDead || Health.Invulnerable) return AttackResult.Ignored;
            Vector2 pos = transform.position;
            Vector2 toAttacker = from - pos;
            bool facingIt = toAttacker.sqrMagnitude < 0.0001f || Vector2.Dot(Facing, toAttacker.normalized) > 0.2f;

            if (IsGuarding && facingIt)
            {
                bool parry = Time.time - guardStart <= parryWindow;
                if (parry)
                {
                    if (source != null)
                    {
                        source.Stun(parryStun);
                        source.TakeHit(1, pos, 7f);
                    }
                    Juice.HitStop(0.12f);
                    CameraFollow.Shake(0.15f, 0.12f);
                    RingFx.Spawn(pos + Facing * 0.5f, 1.2f, new Color(1f, 0.9f, 0.4f, 1f), 0.3f);
                    HUD.Popup(pos + Vector2.up * 1.1f, "받아치기!", new Color(1f, 0.9f, 0.4f));
                    Sound.Play("Tool", 0.9f);
                    safeUntil = Time.time + 0.3f;
                    return AttackResult.Parried;
                }
                if (!unblockable)
                {
                    if (source != null) source.Recoil(pos);
                    knockback = -toAttacker.normalized * 4f;
                    knockUntil = Time.time + 0.08f;
                    Juice.HitStop(0.04f);
                    HUD.Popup(pos + Vector2.up * 1.1f, "막기", new Color(0.75f, 0.85f, 1f));
                    Sound.Play("EnemyHit", 0.5f);
                    safeUntil = Time.time + 0.25f;
                    return AttackResult.Blocked;
                }
            }
            Health.TakeDamage(damage, from);
            return AttackResult.Hit;
        }

        // ------------------------------------------------------------------ 편지 도구 (Q)

        void UseTool()
        {
            var part = Inventory.CurrentToolPart;
            string effect = part != null ? part.effect : "bind";
            float cooldown = part != null && part.value > 0f ? part.value : 6f;
            toolReadyTime = Time.time + cooldown * Mathf.Max(0.4f, 1f - ToolBonus * 0.5f);

            switch (effect)
            {
                case "throw": ThrowLetter(); break;
                case "dash": StartDash(); break;
                default: Bind(); break;
            }
        }

        void Bind()
        {
            Vector2 pos = transform.position;
            int count = 0;
            foreach (var enemy in EnemyController.Active.ToArray())
            {
                if (Vector2.Distance(pos, enemy.transform.position) <= bindRadius)
                {
                    enemy.Stun(bindStunTime * (1f + ToolBonus));
                    count++;
                }
            }
            RingFx.Spawn(pos, bindRadius, new Color(0.55f, 0.9f, 1f, 0.9f), 0.4f);
            Sound.Play("Tool");
            if (count > 0) HUD.Toast("봉인끈이 " + count + "마리를 묶었다!");
        }

        void ThrowLetter()
        {
            LetterProjectile.Spawn((Vector2)transform.position + Facing * 0.5f, Facing, 2 + Mathf.RoundToInt(ToolBonus * 2f));
            Sound.Play("Attack", 0.6f);
        }

        void StartDash()
        {
            dashDir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : Facing;
            Facing = dashDir;
            dashEnd = Time.time + dashTime;
            dashHit.Clear();
            // 내달리는 동안은 적과 부딪히지 않고 통과한다.
            foreach (var e in EnemyController.Active)
            {
                var c = e.GetComponent<Collider2D>();
                if (c != null && ownCollider != null) Physics2D.IgnoreCollision(ownCollider, c, true);
            }
            StartCoroutine(EndDash());
            if (GameAssets.Available) FrameAnimator.PlayOnce(GameAssets.SmokeFrames, transform.position, 18f, Color.white, 600, 1.2f);
            Sound.Play("Tool", 0.7f);
        }

        IEnumerator EndDash()
        {
            yield return new WaitForSeconds(dashTime + 0.05f);
            foreach (var e in EnemyController.Active)
            {
                var c = e.GetComponent<Collider2D>();
                if (c != null && ownCollider != null) Physics2D.IgnoreCollision(ownCollider, c, false);
            }
        }

        void DashHits()
        {
            Vector2 pos = transform.position;
            foreach (var e in EnemyController.Active.ToArray())
            {
                if (dashHit.Contains(e) || Vector2.Distance(pos, e.transform.position) > 0.9f) continue;
                dashHit.Add(e);
                e.TakeHit(1 + Mathf.RoundToInt(ToolBonus), pos, 4f);
                e.Stun(0.7f);
                Juice.HitStop(0.04f);
            }
        }

        // ------------------------------------------------------------------ 피격·사망

        void OnDamaged(Vector2 from)
        {
            hurtUntil = Time.time + 0.9f;
            knockback = ((Vector2)transform.position - from).normalized * 8f;
            knockUntil = Time.time + 0.12f;
            CameraFollow.Shake(0.15f);
            Juice.HitStop(0.05f);
            Sound.Play("PlayerHurt");
        }

        void OnDied()
        {
            HUD.Toast("쓰러졌다... 우체국에서 눈을 떴다.");
            StartCoroutine(Respawn());
        }

        IEnumerator Respawn()
        {
            yield return new WaitForSeconds(1.2f);
            transform.position = RespawnPoint;
            rb.SetVelocity(Vector2.zero);
            Health.SetMax(GameState.MaxHp, true);
            hurtUntil = Time.time + 1.5f;
            if (CameraFollow.I != null) CameraFollow.I.SnapToTarget();
            if (NightDirector.I != null) NightDirector.I.ResetEnemies();
        }

        // ------------------------------------------------------------------ 표시

        void UpdateVisual()
        {
            if (lightGlow != null)
            {
                float light = Inventory.EffectSum("light");
                lightGlow.transform.localScale = Vector3.one * (4.5f + 3.5f * light);
                lightGlow.color = new Color(1f, 0.92f, 0.7f, 0.12f + 0.08f * light);
            }
            if (guardIcon != null)
            {
                guardIcon.enabled = IsGuarding;
                if (IsGuarding)
                {
                    guardIcon.transform.localPosition = (Vector3)(Facing * 0.5f) + new Vector3(0f, -0.1f, 0f);
                    // 받아치기 시간에는 반짝인다
                    bool parryTime = Time.time - guardStart <= parryWindow;
                    guardIcon.color = parryTime ? new Color(1f, 0.95f, 0.6f) : Color.white;
                }
            }
            if (slash != null && slash.enabled)
            {
                float remaining = slashUntil - Time.time;
                if (remaining <= 0f) slash.enabled = false;
                else if (slashFrames != null)
                {
                    int i = (int)((1f - remaining / SlashDuration) * slashFrames.Length);
                    slash.sprite = slashFrames[Mathf.Clamp(i, 0, slashFrames.Length - 1)];
                }
                else slash.color = new Color(slash.color.r, slash.color.g, slash.color.b, Mathf.Clamp01(remaining / SlashDuration));
            }
            if (body == null) return;
            if (Health.IsDead) body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.3f);
            else if (IsDodging || IsDashing) body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.5f);
            else if (Time.time < hurtUntil && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f) body.color = new Color(1f, 0.5f, 0.5f);
            else body.color = bodyColor;
        }
    }
}
