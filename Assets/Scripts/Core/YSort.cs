using UnityEngine;

namespace MoonlightPost
{
    /// <summary>탑다운 화면에서 아래쪽(앞쪽)에 있는 오브젝트가 위에 그려지게 한다.</summary>
    public class YSort : MonoBehaviour
    {
        public int offset;
        public bool isStatic;
        SpriteRenderer[] renderers;
        int[] baseOrders;

        void Start()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            baseOrders = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseOrders[i] = renderers[i].sortingOrder;
            Apply();
            if (isStatic) enabled = false;
        }

        void LateUpdate() => Apply();

        void Apply()
        {
            int order = offset - Mathf.RoundToInt(transform.position.y * 10f);
            for (int i = 0; i < renderers.Length; i++)
            {
                // 그림자(-450)나 빛(1000 이상)처럼 고정 순서를 쓰는 자식은 건드리지 않는다.
                if (renderers[i] == null || baseOrders[i] <= -400 || baseOrders[i] >= 400) continue;
                renderers[i].sortingOrder = order + baseOrders[i];
            }
        }
    }
}
