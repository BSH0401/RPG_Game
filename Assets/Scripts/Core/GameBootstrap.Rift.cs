using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 그림자 균열: 마을의 들어가는 균열과, 지도 밖(섬 남쪽 아래) 어둠 속에 뜬 둥근 돌 마당.
    /// 마당은 보이지 않는 벽으로 둘러싸이고, 가장자리에 보랏빛 돌기둥이 선다. 파도와 보상은 RiftManager.
    /// </summary>
    public partial class GameBootstrap
    {
        /// <summary>마을의 그림자 균열 위치: 우체국 오른쪽, 강화 작업대 너머.</summary>
        static readonly Vector2 RiftPortalPos = new Vector2(7.8f, 5.2f);

        void BuildRift()
        {
            RiftManager.MakePortal(RiftPortalPos, false);
            BuildRiftArena(RiftManager.Center, RiftManager.Radius);
        }

        void BuildRiftArena(Vector2 c, float radius)
        {
            var root = new GameObject("RiftArena").transform;
            root.SetParent(worldRoot.transform, false);
            int cx = Mathf.RoundToInt(c.x), cy = Mathf.RoundToInt(c.y);
            int r = Mathf.CeilToInt(radius) + 1;
            System.Func<int, int, bool> inside = (x, y) => new Vector2(x + 0.5f - c.x, y + 0.5f - c.y).magnitude < radius + 0.4f;
            bool cainos = GameAssets.CainosAvailable;
            var tint = new Color(0.52f, 0.44f, 0.72f);
            for (int y = cy - r; y <= cy + r; y++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!inside(x, y)) continue;
                    var sprite = cainos ? CainosBlob("StoneGround", inside, x, y) : useAssets ? BlobTile(Floor, 11, 14, inside, x, y, true) : null;
                    if (sprite != null) Spawner.GroundTile(root, x, y, sprite, -1000, tint);
                }
            if (!cainos && !useAssets)
            {
                // 코드 그림: 둥근 바닥 하나
                var floor = new GameObject("RiftFloor");
                floor.transform.SetParent(root, false);
                floor.transform.position = c;
                floor.transform.localScale = Vector3.one * radius * 2f;
                var sr = floor.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle;
                sr.color = new Color(0.22f, 0.18f, 0.3f);
                sr.sortingOrder = -1000;
            }

            // 보이지 않는 둥근 벽(32조각)
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 0.7f);
                var wall = Spawner.Collider("RiftWall", p, new Vector2(2.2f, 1f));
                wall.transform.SetParent(root, true);
                wall.transform.rotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
            }

            // 가장자리의 돌기둥과 보랏빛
            var pillar = cainos ? GameAssets.Cainos("Props", 352, 174, 32, 77) : null;
            for (int i = 0; i < 8; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2f / 8f;
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + 0.1f);
                if (pillar != null)
                {
                    var go = Spawner.Prop("RiftPillar", p, pillar, new Vector2(0.8f, 0.4f), new Vector2(0f, 0.2f));
                    go.transform.SetParent(root, true);
                    go.GetComponent<SpriteRenderer>().color = new Color(0.7f, 0.6f, 0.95f);
                }
                Spawner.Glow(p + new Vector2(0f, 1.6f), 3.2f, new Color(0.65f, 0.35f, 1f, 0.3f), root);
            }
            Spawner.Glow(c, radius * 2.6f, new Color(0.5f, 0.3f, 0.9f, 0.12f), root);
            for (int i = 0; i < 14; i++)
            {
                var p = c + Random.insideUnitCircle * (radius - 1f);
                Firefly.Spawn(root, p, new Color(0.8f, 0.55f, 1f), 1.2f, 0.9f);
            }
        }
    }
}
