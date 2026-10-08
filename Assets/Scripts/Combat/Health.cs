using System;
using UnityEngine;

namespace MoonlightPost
{
    public class Health : MonoBehaviour
    {
        public int Max { get; private set; } = 3;
        public int Current { get; private set; } = 3;
        public bool Invulnerable { get; set; }
        public bool IsDead => Current <= 0;

        /// <summary>피해를 입었을 때. 인자는 공격이 온 위치(넉백 방향 계산용).</summary>
        public event Action<Vector2> Damaged;
        public event Action Died;

        public void SetMax(int max, bool refill)
        {
            Max = Mathf.Max(1, max);
            Current = refill ? Max : Mathf.Min(Current, Max);
        }

        public bool TakeDamage(int amount, Vector2 from)
        {
            if (IsDead || Invulnerable || amount <= 0) return false;
            Current = Mathf.Max(0, Current - amount);
            Damaged?.Invoke(from);
            if (Current == 0) Died?.Invoke();
            return true;
        }

        public void HealFull() => Current = Max;

        public void Heal(int amount)
        {
            if (!IsDead && amount > 0) Current = Mathf.Min(Max, Current + amount);
        }
    }
}
