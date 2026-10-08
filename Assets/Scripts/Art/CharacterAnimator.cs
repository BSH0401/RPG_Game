using UnityEngine;

namespace MoonlightPost
{
    /// <summary>
    /// 프레임 애니메이션이 없는 동안 쓰는 간단한 움직임: 걸을 때 통통 튀기, 서 있을 때 숨쉬기, 좌우 뒤집기.
    /// </summary>
    public class CharacterAnimator : MonoBehaviour
    {
        public SpriteRenderer body;
        public float bobHeight = 0.07f;
        public float bobSpeed = 14f;

        Vector3 lastPos;
        Vector3 baseLocal;
        float phase;

        void Start()
        {
            lastPos = transform.position;
            if (body != null) baseLocal = body.transform.localPosition;
            phase = Random.value * 10f;
        }

        void LateUpdate()
        {
            if (body == null || Time.deltaTime <= 0f) return;
            Vector3 delta = transform.position - lastPos;
            lastPos = transform.position;
            float speed = delta.magnitude / Time.deltaTime;
            bool moving = speed > 0.3f;

            if (Mathf.Abs(delta.x) > 0.0005f) body.flipX = delta.x < 0f;

            phase += Time.deltaTime * (moving ? bobSpeed : 2.2f);
            var t = body.transform;
            if (moving)
            {
                t.localPosition = baseLocal + new Vector3(0f, Mathf.Abs(Mathf.Sin(phase)) * bobHeight, 0f);
                t.localScale = Vector3.one;
            }
            else
            {
                t.localPosition = baseLocal;
                float breathe = Mathf.Sin(phase) * 0.025f;
                t.localScale = new Vector3(1f - breathe * 0.5f, 1f + breathe, 1f);
            }
        }
    }
}
