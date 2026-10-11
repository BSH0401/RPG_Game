using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    public enum AttackResult { Hit, Blocked, Parried, Ignored }

    /// <summary>
    /// 배달부: 이동, 마우스 커서 쪽으로 3연타 공격(누르고 있으면 계속), 회피, 스킬(우클릭·Q), 조사·대화.
    /// 공격력·속도·치명타는 레벨과 스탯(Stats)에서 나온다. 스킬은 끼운 편지 도구 부품에 따라 바뀐다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I { get; private set; }

        /// <summary>적의 공격 수치(1, 2 …)에 곱해 실제 피해로 만든다. 체력이 10배 단위가 되면서 생겼다.</summary>
        public const int EnemyDamageScale = 10;

        [Header("이동")]
        public float moveSpeed = 5f;
        public float dodgeSpeed = 13f;
        public float dodgeTime = 0.18f;
        public float dodgeCooldown = 0.5f;

        [Header("공격 (3연타: 마지막 타가 강타)")]
        public float attackCooldown = 0.3f;
        public float comboWindow = 0.6f;
        public float attackReach = 0.85f;
        public float attackRadius = 0.8f;
        public float finisherMul = 1.8f;

        [Header("스킬 (편지 도구 부품에 따라)")]
        public float bindRadius = 3f;
        public float bindStunTime = 2.2f;
        public float dashDistance = 5f;
        public float dashTime = 0.22f;

        [Header("조사")]
        public float interactRange = 1.5f;

        public Health Health { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.down;
        public Interactable CurrentTarget { get; private set; }
        public float SkillCooldownRemaining => Mathf.Max(0f, skillReadyTime - Time.time);
        public float SkillCooldownTotal { get; private set; } = 1f;
        /// <summary>배달부 주변을 밝히는 손등불(반딧불이 병이 있으면 커진다).</summary>
        public SpriteRenderer lightGlow;
        public Vector3 RespawnPoint { get; set; }

        // 장비 효과 (items.json 의 effect 값, 퍼센트)
        static float DodgeBonus => Inventory.EffectSum("dodge") / 100f;
        // 끼운 편지 도구 부품을 강화했으면 그만큼 더한다.
        static float SkillBonus => (Inventory.EffectSum("tool") + Upgrade.Bonus(Inventory.CurrentToolPart, Upgrade.Level(Inventory.CurrentToolPart))) / 100f + Stats.SkillPowerBonus;

        readonly List<Collider2D> hits = new List<Collider2D>();
        readonly HashSet<EnemyController> dashHit = new HashSet<EnemyController>();
        Rigidbody2D rb;
        Collider2D ownCollider;
        SpriteRenderer body;
        SpriteRenderer slash;
        SpriteAnimator animator;
        Sprite[] slashFrames;
        float slashAngleOffset = -90f;
        /// <summary>캐릭터 그림에 칼 공격 동작이 있으면(Mana Seed) 따로 그리던 베기 효과 대신 그것을 쓴다.</summary>
        bool swordSheet;
        const float SlashDuration = 0.15f;
        Color bodyColor;
        Vector2 moveInput;
        Vector2 dodgeDir;
        Vector2 dashDir;
        Vector2 knockback;
        float dodgeEnd, nextDodge, nextAttack, skillReadyTime, hurtUntil, safeUntil, knockUntil, slashUntil, dashEnd, lastAttackTime, faceLockUntil;
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
            swordSheet = animator != null && animator.sheet != null && animator.sheet.HasAction("slash1");
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

        /// <summary>최대 체력이 늘면 늘어난 만큼 채우고, 줄면 넘치는 만큼만 깎는다.</summary>
        void SyncMaxHp()
        {
            int max = GameState.MaxHp;
            if (Health.Max == max) return;
            int gained = max - Health.Max;
            Health.SetMax(max, false);
            if (gained > 0) Health.Heal(gained);
        }

        void Update()
        {
            UpdateVisual();
            bool busy = Time.timeScale == 0f || DialogueSystem.IsOpen || Health.IsDead
                        || Time.frameCount == DialogueSystem.LastClosedFrame || Time.frameCount == GameMenu.LastClosedFrame
                        || Time.frameCount == UpgradeMenu.LastClosedFrame || Time.frameCount == ShopMenu.LastClosedFrame;
            if (busy)
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = GameInput.Move;
            if (moveInput.sqrMagnitude > 0.01f && Time.time >= faceLockUntil) Facing = moveInput.normalized;
            if (animator != null) animator.SetFacing(Facing);

            if (GameInput.DodgePressed && Time.time >= nextDodge && !IsDashing) StartDodge();
            if ((GameInput.AttackPressed || GameInput.AttackHeld) && Time.time >= nextAttack && !IsDodging && !IsDashing) Attack();
            if (GameInput.SkillPressed && !IsDashing)
            {
                if (Time.time >= skillReadyTime) UseSkill();
                else HUD.Popup((Vector2)transform.position + Vector2.up * 1.2f, SkillCooldownRemaining.ToString("0.0") + "초", new Color(0.7f, 0.75f, 0.9f));
            }
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
            else v = moveInput * moveSpeed * Stats.MoveSpeedMul * (Time.time < nextAttack ? 0.75f : 1f);
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

        /// <summary>노리는 방향: 마우스로 공격하면 커서 쪽, 키보드(J·Q)면 바라보는 방향.</summary>
        Vector2 Aim()
        {
            var cam = Camera.main;
            if (cam != null && GameInput.AimWithMouse)
            {
                Vector2 mouse = cam.ScreenToWorldPoint(GameInput.MouseScreen);
                Vector2 to = mouse - (Vector2)transform.position;
                if (to.sqrMagnitude > 0.01f) return to.normalized;
            }
            return Facing;
        }

        /// <summary>공격하는 동안은 노린 쪽을 바라본다.</summary>
        void FaceToward(Vector2 dir, float lockTime)
        {
            Facing = dir;
            faceLockUntil = Time.time + lockTime;
            if (animator != null) animator.SetFacing(Facing);
        }

        void StartDodge()
        {
            dodgeDir = moveInput.sqrMagnitude > 0.01f ? moveInput.normalized : Facing;
            dodgeEnd = Time.time + dodgeTime * (1f + DodgeBonus);
            nextDodge = dodgeEnd + dodgeCooldown;
            if (animator != null) animator.PlaySequence("run", dodgeEnd - Time.time);
        }

        // ------------------------------------------------------------------ 공격 (3연타)

        void Attack()
        {
            comboStep = Time.time - lastAttackTime <= comboWindow ? comboStep % 3 + 1 : 1;
            lastAttackTime = Time.time;
            bool finisher = comboStep == 3;
            float cooldown = attackCooldown * (finisher ? 1.6f : 1f) / Stats.AttackSpeedMul;
            nextAttack = Time.time + cooldown;

            Vector2 aim = Aim();
            FaceToward(aim, cooldown + 0.1f);

            float reach = finisher ? attackReach * 1.15f : attackReach;
            float radius = finisher ? attackRadius * 1.35f : attackRadius;
            Vector2 center = (Vector2)transform.position + aim * reach;

            hits.Clear();
            Physics2D.OverlapCircle(center, radius, new ContactFilter2D().NoFilter(), hits);
            var hitSet = new HashSet<EnemyController>();
            bool anyCrit = false;
            foreach (var col in hits)
            {
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy == null || !hitSet.Add(enemy)) continue;
                int dmg = Stats.RollDamage(finisher ? finisherMul : 1f, out bool crit);
                anyCrit |= crit;
                enemy.TakeHit(dmg, transform.position, finisher ? 9f : 6f, crit);
            }
            if (hitSet.Count > 0)
            {
                Juice.HitStop(finisher || anyCrit ? 0.07f : 0.03f);
                if (finisher || anyCrit) CameraFollow.Shake(0.12f, 0.12f);
            }

            if (swordSheet)
            {
                // 칼 동작에 베기 궤적이 그려져 있다: 1·2타는 베기, 3타는 찌르기
                animator.PlaySequence(comboStep == 1 ? "slash1" : comboStep == 2 ? "slash2" : "thrust", Mathf.Max(0.22f, cooldown * 0.95f));
                Sound.Play("Attack", finisher ? 1f : 0.7f);
                return;
            }

            // 노린 방향으로 반달 모양 베기 효과 (강타는 크게)
            slash.transform.position = (Vector2)transform.position + aim * (slashFrames != null ? 0.6f : 0.15f);
            slash.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg + slashAngleOffset);
            slash.transform.localScale = Vector3.one * (finisher ? 1.5f : 1.05f);
            slash.color = finisher ? new Color(1f, 0.92f, 0.6f) : Color.white;
            slash.enabled = true;
            slashUntil = Time.time + SlashDuration;
            if (animator != null && animator.sheet != null && animator.sheet.HasAttackRow) animator.PlayAction(4, 0.2f);
            Sound.Play("Attack", finisher ? 1f : 0.7f);
        }

        // ------------------------------------------------------------------ 맞을 때

        /// <summary>
        /// 적의 공격이 닿았을 때 부른다. damage 는 적 쪽 수치(1, 2 …)이고 EnemyDamageScale 을 곱해 깎는다.
        /// 회피·무적 중이면 무시한다.
        /// </summary>
        public AttackResult ReceiveAttack(EnemyController source, int damage, Vector2 from, bool unblockable)
        {
            if (Health.IsDead || Health.Invulnerable) return AttackResult.Ignored;
            // 방어가 피해를 줄인다(최소 1).
            int dmg = Mathf.Max(1, Mathf.RoundToInt(damage * EnemyDamageScale * (1f - Stats.DamageReduction)));
            if (Health.TakeDamage(dmg, from))
                HUD.Popup((Vector2)transform.position + Vector2.up * 1.1f, "-" + dmg, new Color(1f, 0.4f, 0.4f));
            return AttackResult.Hit;
        }

        // ------------------------------------------------------------------ 스킬 (우클릭 · Q)

        /// <summary>지금 끼운 부품으로 정해지는 스킬의 이름·설명(HUD·캐릭터 창).</summary>
        public static string SkillName(ItemDef part)
        {
            switch (part != null ? part.effect : "bind")
            {
                case "throw": return "날아가는 편지";
                case "dash": return "속달 돌진";
                default: return "봉인끈";
            }
        }

        public static string SkillDescription(ItemDef part)
        {
            switch (part != null ? part.effect : "bind")
            {
                case "throw": return "커서 쪽으로 편지를 날려 꿰뚫는다. 공격력 x2.6";
                case "dash": return "커서 쪽으로 내달리며 지나간 적을 친다(무적). 공격력 x2.2";
                default: return "주변 적을 묶고 피해를 준다. 공격력 x1.4 + 기절";
            }
        }

        void UseSkill()
        {
            var part = Inventory.CurrentToolPart;
            string effect = part != null ? part.effect : "bind";
            float cooldown = part != null && part.value > 0f ? part.value : 6f;
            SkillCooldownTotal = cooldown * Mathf.Max(0.4f, 1f - SkillBonus * 0.5f) * Stats.CooldownMul;
            skillReadyTime = Time.time + SkillCooldownTotal;

            Vector2 aim = Aim();
            FaceToward(aim, 0.3f);
            if (swordSheet) animator.PlaySequence(effect == "bind" ? "bash" : effect == "throw" ? "thrust" : "slash2", 0.3f);
            switch (effect)
            {
                case "throw": ThrowLetter(aim); break;
                case "dash": StartDash(aim); break;
                default: Bind(); break;
            }
        }

        float SkillMul(float baseMul) => baseMul * (1f + SkillBonus);

        void Bind()
        {
            Vector2 pos = transform.position;
            foreach (var enemy in EnemyController.Active.ToArray())
            {
                if (Vector2.Distance(pos, enemy.transform.position) > bindRadius) continue;
                int dmg = Stats.RollDamage(SkillMul(1.4f), out bool crit);
                enemy.TakeHit(dmg, pos, 3f, crit);
                enemy.Stun(bindStunTime * (1f + SkillBonus));
            }
            RingFx.Spawn(pos, bindRadius, new Color(0.55f, 0.9f, 1f, 0.9f), 0.4f);
            RingFx.Spawn(pos, bindRadius * 0.6f, new Color(0.8f, 0.95f, 1f, 0.9f), 0.3f);
            CameraFollow.Shake(0.1f, 0.1f);
            Sound.Play("Tool");
        }

        void ThrowLetter(Vector2 aim)
        {
            LetterProjectile.Spawn((Vector2)transform.position + aim * 0.5f, aim, SkillMul(2.6f));
            Sound.Play("Attack", 0.8f);
        }

        void StartDash(Vector2 aim)
        {
            dashDir = aim;
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
                if (dashHit.Contains(e) || Vector2.Distance(pos, e.transform.position) > 1f) continue;
                dashHit.Add(e);
                int dmg = Stats.RollDamage(SkillMul(2.2f), out bool crit);
                e.TakeHit(dmg, pos, 4f, crit);
                e.Stun(0.7f);
                Juice.HitStop(0.04f);
            }
        }

        // ------------------------------------------------------------------ 피격·사망

        void OnDamaged(Vector2 from)
        {
            hurtUntil = Time.time + 0.9f;
            if (animator != null) animator.PlaySequence("hurt", 0.25f);
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
