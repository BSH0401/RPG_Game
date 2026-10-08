using UnityEngine;

namespace MoonlightPost
{
    /// <summary>월드 오브젝트를 만드는 도우미. 그림은 Art 에서, 배치는 GameBootstrap 에서 정한다.</summary>
    public static class Spawner
    {
        public static Transform Root;

        public const int GlowOrder = 1001;

        /// <summary>에셋 그림(낮 색감)에 곱해 밤처럼 보이게 하는 색. 코드 그림을 쓸 때는 흰색.</summary>
        public static Color PropTint = Color.white;
        public static Color CharacterTint = Color.white;
        const int ShadowOrder = -450;

        /// <summary>보이지 않는 충돌 벽.</summary>
        public static GameObject Collider(string name, Vector2 center, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = center;
            go.AddComponent<BoxCollider2D>().size = size;
            return go;
        }

        /// <summary>
        /// 스프라이트 소품. 스프라이트 기준점이 발밑이므로 pos 는 바닥에 닿는 지점이다.
        /// colliderSize 를 주면 발밑 기준으로 colliderOffset 만큼 위에 충돌 상자를 둔다.
        /// </summary>
        public static GameObject Prop(string name, Vector2 pos, Sprite sprite, Vector2? colliderSize = null,
                                      Vector2 colliderOffset = default, int? fixedOrder = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = PropTint;
            if (colliderSize.HasValue)
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.size = colliderSize.Value;
                box.offset = colliderOffset;
            }
            if (fixedOrder.HasValue) sr.sortingOrder = fixedOrder.Value;
            else AddYSort(go, 0, true);
            return go;
        }

        /// <summary>
        /// 건물: footprint(충돌 영역, 월드 좌표 중심·크기)의 아랫변에 그림을 세운다.
        /// 지붕은 그림에서 위쪽으로 튀어나와 보인다.
        /// </summary>
        public static GameObject Building(string name, Vector2 footprintCenter, Vector2 footprintSize, Sprite sprite, bool solid = true)
        {
            var bottom = new Vector2(footprintCenter.x, footprintCenter.y - footprintSize.y * 0.5f);
            return Prop(name, bottom, sprite, solid ? footprintSize : (Vector2?)null, new Vector2(0f, footprintSize.y * 0.5f));
        }

        public static void Tree(Vector2 pos, bool pine, int variant) =>
            Tree(pos, pine ? Art.PineTree(variant % 3) : Art.RoundTree(variant % 4));

        /// <summary>나무·덤불: 밑동에만 작은 충돌체를 둔다.</summary>
        public static GameObject Tree(Vector2 pos, Sprite sprite, float trunkRadius = 0.35f)
        {
            var go = Prop("Tree", pos, sprite);
            var col = go.AddComponent<CircleCollider2D>();
            col.radius = trunkRadius;
            col.offset = new Vector2(0f, 0.3f);
            Shadow(go.transform, sprite.bounds.size.x * 0.8f, 0.05f);
            return go;
        }

        /// <summary>바닥 타일 한 칸. 칸의 왼쪽 아래 모서리가 (x, y).</summary>
        public static void GroundTile(Transform parent, int x, int y, Sprite sprite, int order, Color? tint = null)
        {
            if (sprite == null) return;
            var go = new GameObject("T");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint ?? PropTint;
            sr.sortingOrder = order;
        }

        public static SpriteRenderer Glow(Vector2 pos, float diameter, Color color, Transform parent = null)
        {
            var go = new GameObject("Glow");
            go.transform.SetParent(parent != null ? parent : Root, false);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * diameter;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Glow;
            sr.color = color;
            sr.sortingOrder = GlowOrder;
            return sr;
        }

        public static void Lamp(Vector2 pos, bool lit)
        {
            var go = Prop("LampPost", pos, Art.LampPost(lit), new Vector2(0.3f, 0.25f), new Vector2(0f, 0.12f));
            Shadow(go.transform, 0.8f, 0f);
            if (lit)
            {
                Glow(pos + new Vector2(0f, 1.6f), 1.4f, new Color(1f, 0.92f, 0.65f, 0.9f));
                Glow(pos + new Vector2(0f, 0.8f), 6f, new Color(1f, 0.82f, 0.5f, 0.28f));
            }
        }

        static void Shadow(Transform parent, float width, float y)
        {
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, y, 0f);
            go.transform.localScale = new Vector3(width, width, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Art.Shadow;
            sr.sortingOrder = ShadowOrder;
        }

        /// <summary>
        /// 캐릭터 그림을 붙인다. 오브젝트 위치(=충돌체 중심)보다 feetOffset 만큼 아래가 발밑.
        /// 몸 렌더러를 돌려준다(색으로 피격·기절 표현).
        /// </summary>
        public static SpriteRenderer Character(GameObject go, Sprite sprite, float scale = 1f, float feetOffset = 0.35f)
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, -feetOffset, 0f);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = sprite;

            if (!Mathf.Approximately(scale, 1f))
            {
                // 크기는 부모에 적용해 몸 애니메이션의 스케일과 겹치지 않게 한다.
                var holder = new GameObject("Scale");
                holder.transform.SetParent(go.transform, false);
                holder.transform.localScale = Vector3.one * scale;
                bodyGo.transform.SetParent(holder.transform, false);
                bodyGo.transform.localPosition = new Vector3(0f, -feetOffset / scale, 0f);
            }

            Shadow(go.transform, sprite.bounds.size.x * scale * 0.9f, -feetOffset + 0.02f);
            go.AddComponent<CharacterAnimator>().body = body;
            AddYSort(go, 0, false);
            return body;
        }

        /// <summary>에셋 캐릭터 시트로 몸을 만든다(4방향 걷기 애니메이션).</summary>
        public static SpriteRenderer Character(GameObject go, CharacterSheet sheet, float feetOffset = 0.4f)
        {
            const float Px = 1f / 16f;
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            // 1픽셀 띄우고 바로 아래에 그림자를 깔아 발이 바닥에 묻혀 보이지 않게 한다.
            bodyGo.transform.localPosition = new Vector3(0f, -feetOffset + Px, 0f);
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.color = CharacterTint;
            var shadow = new GameObject("Shadow");
            shadow.transform.SetParent(go.transform, false);
            shadow.transform.localPosition = new Vector3(0f, -feetOffset - Px, 0f);
            var shadowRenderer = shadow.AddComponent<SpriteRenderer>();
            shadowRenderer.sprite = Art.FootShadow;
            shadowRenderer.sortingOrder = ShadowOrder;
            go.AddComponent<SpriteAnimator>().Init(body, sheet);
            AddYSort(go, 0, false);
            return body;
        }

        /// <summary>actor 시트가 있으면 그것을, 없으면 코드로 그린 sprite 를 쓴다.</summary>
        public static NpcInteractable Npc(string id, Vector2 pos, Sprite sprite, float scale = 1f, string actor = null)
        {
            var go = new GameObject("NPC_" + id);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            var sheet = actor != null && GameAssets.Available ? GameAssets.Sheet(actor) : null;
            if (sheet != null) Character(go, sheet);
            else Character(go, sprite, scale);
            go.AddComponent<CircleCollider2D>().radius = 0.35f;
            var npc = go.AddComponent<NpcInteractable>();
            npc.npcId = id;
            return npc;
        }

        public static GameObject Enemy(Vector2 pos, bool boss)
        {
            var go = new GameObject(boss ? "Boss_InkShade" : "Enemy_ShadowRat");
            go.transform.SetParent(Root, false);
            go.transform.position = pos;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.mass = boss ? 20f : 2f;
            go.AddComponent<CircleCollider2D>().radius = boss ? 0.7f : 0.35f;

            var health = go.AddComponent<Health>();
            var enemy = go.AddComponent<EnemyController>();
            SpriteRenderer body;
            if (boss)
            {
                health.SetMax(14, true);
                enemy.displayName = "먹물 그림자";
                enemy.isBoss = true;
                enemy.defeatFlag = "boss_forest_defeated";
                enemy.moveSpeed = 1.8f;
                enemy.detectRange = 7f;
                enemy.attackRange = 3f;
                enemy.windupTime = 0.8f;
                enemy.lungeSpeed = 11f;
                enemy.lungeTime = 0.3f;
                enemy.recoverTime = 0.9f;
                enemy.slamRadius = 2.8f;
                enemy.hitRadius = 1.2f;
                enemy.pattern = new[] { EnemyAttack.Slam, EnemyAttack.Lunge, EnemyAttack.Lunge };
                var bossFrames = GameAssets.Available ? GameAssets.BossFrames : null;
                if (bossFrames != null)
                {
                    body = Character(go, bossFrames[0], 1f, 0.7f);
                    // 하얀 영혼 그림을 먹물색으로 물들인다.
                    body.color = new Color(0.55f, 0.38f, 0.9f);
                    Object.Destroy(go.GetComponent<CharacterAnimator>());
                    var anim = body.gameObject.AddComponent<FrameAnimator>();
                    anim.frames = bossFrames;
                    anim.fps = 7f;
                }
                else body = Character(go, Art.InkBoss, 1f, 0.7f);
                Glow(pos, 5f, new Color(0.6f, 0.4f, 1f, 0.25f), go.transform);
            }
            else
            {
                health.SetMax(3, true);
                var ratSheet = GameAssets.Available ? GameAssets.Sheet("MouseBlack") : null;
                body = ratSheet != null ? Character(go, ratSheet, 0.4f) : Character(go, Art.ShadowRat, 1f, 0.35f);
                if (ratSheet != null) body.color = new Color(0.75f, 0.7f, 0.95f);
                Glow(pos, 1.6f, new Color(1f, 0.4f, 0.6f, 0.18f), go.transform);
            }
            enemy.Init(body);
            return go;
        }

        static void AddYSort(GameObject go, int offset, bool isStatic)
        {
            var ys = go.AddComponent<YSort>();
            ys.offset = offset;
            ys.isStatic = isStatic;
        }
    }
}
