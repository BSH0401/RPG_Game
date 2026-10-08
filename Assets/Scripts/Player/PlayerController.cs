using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 배달부: 이동, 기본 공격, 회피, 편지 도구(봉인끈), 조사·대화.
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

        [Header("공격")]
        public float attackCooldown = 0.3f;
        public float attackReach = 0.85f;
        public float attackRadius = 0.75f;
        public int attackDamage = 1;

        [Header("편지 도구: 봉인끈")]
        public float toolCooldown = 6f;
        public float toolRadius = 3f;
        public float toolStunTime = 2.2f;

        [Header("조사")]
        public float interactRange = 1.5f;

        public Health Health { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.down;
        public Interactable CurrentTarget { get; private set; }
        public float ToolCooldownRemaining => Mathf.Max(0f, toolReadyTime - Time.time);
        public Vector3 RespawnPoint { get; set; }

        readonly List<Collider2D> hits = new List<Collider2D>();
        Rigidbody2D rb;
        SpriteRenderer body;
        SpriteRenderer slash;
        SpriteAnimator animator;
        Sprite[] slashFrames;
        float slashAngleOffset = -90f;
        const float SlashDuration = 0.15f;
        Color bodyColor;
        Vector2 moveInput;
        Vector2 dodgeDir;
        Vector2 knockback;
        float dodgeEnd, nextDodge, nextAttack, toolReadyTime, hurtUntil, knockUntil, slashUntil;
        bool IsDodging => Time.time < dodgeEnd;

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
        }

        void Awake()
        {
            I = this;
            rb = GetComponent<Rigidbody2D>();
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
                        || Time.frameCount == DialogueSystem.LastClosedFrame;
            if (busy)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = GameInput.Move;
            if (moveInput.sqrMagnitude > 0.01f) Facing = moveInput.normalized;
            if (animator != null) animator.SetFacing(Facing);

            if (GameInput.DodgePressed && Time.time >= nextDodge) StartDodge();
            if (GameInput.AttackPressed && Time.time >= nextAttack && !IsDodging) Attack();
            if (GameInput.ToolPressed && Time.time >= toolReadyTime) UseTool();

            CurrentTarget = FindTarget();
            if (GameInput.InteractPressed && CurrentTarget != null) CurrentTarget.Interact(this);

            Health.Invulnerable = IsDodging || Time.time < hurtUntil;
        }

        void FixedUpdate()
        {
            Vector2 v;
            if (Time.time < knockUntil) v = knockback;
            else if (IsDodging) v = dodgeDir * dodgeSpeed;
            else v = moveInput * moveSpeed;
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
            dodgeEnd = Time.time + dodgeTime;
            nextDodge = dodgeEnd + dodgeCooldown;
        }

        void Attack()
        {
            nextAttack = Time.time + attackCooldown;
            Vector2 center = (Vector2)transform.position + Facing * attackReach;

            hits.Clear();
            Physics2D.OverlapCircle(center, attackRadius, new ContactFilter2D().NoFilter(), hits);
            var hitSet = new HashSet<EnemyController>();
            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy != null && hitSet.Add(enemy)) enemy.TakeHit(attackDamage, transform.position);
            }

            // 바라보는 방향으로 반달 모양 베기 효과
            slash.transform.position = (Vector2)transform.position + Facing * (slashFrames != null ? 0.6f : 0.15f);
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(Facing.y, Facing.x) * Mathf.Rad2Deg + slashAngleOffset);
            slash.enabled = true;
            slashUntil = Time.time + SlashDuration;
            if (animator != null && animator.sheet != null && animator.sheet.HasAttackRow) animator.PlayAction(4, 0.2f);
            Sound.Play("Attack", 0.7f);
        }

        void UseTool()
        {
            toolReadyTime = Time.time + toolCooldown;
            Vector2 pos = transform.position;
            int count = 0;
            foreach (var enemy in EnemyController.Active.ToArray())
            {
                if (Vector2.Distance(pos, enemy.transform.position) <= toolRadius)
                {
                    enemy.Stun(toolStunTime);
                    count++;
                }
            }
            RingFx.Spawn(pos, toolRadius, new Color(0.55f, 0.9f, 1f, 0.9f), 0.4f);
            Sound.Play("Tool");
            if (count > 0) HUD.Toast("봉인끈이 " + count + "마리를 묶었다!");
        }

        void OnDamaged(Vector2 from)
        {
            hurtUntil = Time.time + 0.9f;
            knockback = ((Vector2)transform.position - from).normalized * 8f;
            knockUntil = Time.time + 0.12f;
            CameraFollow.Shake(0.15f);
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
            CameraFollow.I?.SnapToTarget();
            if (NightDirector.I != null) NightDirector.I.ResetEnemies();
        }

        void UpdateVisual()
        {
            if (slash != null && slash.enabled)
            {
                float remaining = slashUntil - Time.time;
                if (remaining <= 0f) slash.enabled = false;
                else if (slashFrames != null)
                {
                    int i = (int)((1f - remaining / SlashDuration) * slashFrames.Length);
                    slash.sprite = slashFrames[Mathf.Clamp(i, 0, slashFrames.Length - 1)];
                }
                else slash.color = new Color(1f, 1f, 1f, Mathf.Clamp01(remaining / SlashDuration));
            }
            if (body == null) return;
            if (Health.IsDead) body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.3f);
            else if (IsDodging) body.color = new Color(bodyColor.r, bodyColor.g, bodyColor.b, 0.5f);
            else if (Time.time < hurtUntil && Mathf.Repeat(Time.time * 12f, 1f) < 0.5f) body.color = new Color(1f, 0.5f, 0.5f);
            else body.color = bodyColor;
        }
    }
}
