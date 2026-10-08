using System.Collections.Generic;
using UnityEngine;

namespace MoonlightPost
{
    /// <summary>E 키로 조사·대화할 수 있는 모든 것의 기반.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public float rangeScale = 1f;

        public abstract string DisplayName { get; }
        public abstract string Prompt { get; }
        public abstract void Interact(PlayerController player);

        protected virtual void OnEnable() => All.Add(this);
        protected virtual void OnDisable() => All.Remove(this);
    }
}
