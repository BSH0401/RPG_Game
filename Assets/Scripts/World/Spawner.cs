using UnityEngine;

namespace MoonlightPost
{
    /// <summary>임시 도형으로 월드 오브젝트를 만드는 도우미. 아트 교체 시 이곳의 스프라이트만 바꾸면 된다.</summary>
    public static class Spawner
    {
        public static Transform Root;

        /// <summary>사각형 블록. solid 이면 충돌한다. 바닥처럼 정렬이 고정되면 sortingOrder 를 직접 준다.</summary>
        public static GameObject Block(string name, Vector2 center, Vector2 size, Color color, bool solid, int? fixedOrder = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = center;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Square;
            sr.color = color;
            if (solid) go.AddComponent<BoxCollider2D>();
            if (fixedOrder.HasValue) sr.sortingOrder = fixedOrder.Value;
            else AddYSort(go, Mathf.RoundToInt(size.y * 5f), true);
            return go;
        }

        public static GameObject Circle(string name, Vector2 center, float diameter, Color color, bool solid, int? fixedOrder = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(Root, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * diameter;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = color;
            if (solid) go.AddComponent<CircleCollider2D>().radius = 0.5f;
            if (fixedOrder.HasValue) sr.sortingOrder = fixedOrder.Value;
            else AddYSort(go, 0, true);
            return go;
        }

        /// <summary>나무: 줄기(충돌) + 잎(장식).</summary>
        public static void Tree(Vector2 pos)
        {
            var trunk = Circle("Tree", pos, 0.7f, new Color(0.22f, 0.16f, 0.12f), true);
            var leaves = new GameObject("Leaves");
            leaves.transform.SetParent(trunk.transform, false);
            leaves.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            leaves.transform.localScale = Vector3.one * 2.6f;
            var sr = leaves.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle;
            sr.color = new Color(0.1f, 0.24f, 0.22f);
            sr.sortingOrder = 2;
        }

        /// <summary>캐릭터 모양: 몸(원) + 모자(사각형). 몸 렌더러를 돌려준다.</summary>
        public static SpriteRenderer Character(GameObject go, Color bodyColor, Color hatColor, float size = 0.9f)
        {
            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            bodyGo.transform.localScale = Vector3.one * size;
            var body = bodyGo.AddComponent<SpriteRenderer>();
            body.sprite = SpriteFactory.Circle;
            body.color = bodyColor;

            var hatGo = new GameObject("Hat");
            hatGo.transform.SetParent(go.transform, false);
            hatGo.transform.localPosition = new Vector3(0f, size * 0.45f, 0f);
            hatGo.transform.localScale = new Vector3(size * 0.75f, size * 0.25f, 1f);
            var hat = hatGo.AddComponent<SpriteRenderer>();
            hat.sprite = SpriteFactory.Square;
            hat.color = hatColor;
            hat.sortingOrder = 1;

            AddYSort(go, 0, false);
            return body;
        }

        public static NpcInteractable Npc(string id, Vector2 pos, Color bodyColor, Color hatColor)
        {
            var go = new GameObject("NPC_" + id);
            go.transform.SetParent(Root, false);
            go.transform.position = pos;
            Character(go, bodyColor, hatColor);
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
                body = Character(go, new Color(0.18f, 0.12f, 0.3f), new Color(0.6f, 0.2f, 0.4f), 1.9f);
            }
            else
            {
                health.SetMax(3, true);
                body = Character(go, new Color(0.38f, 0.28f, 0.55f), new Color(0.2f, 0.15f, 0.3f), 0.8f);
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
