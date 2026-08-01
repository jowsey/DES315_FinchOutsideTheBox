using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [ExecuteAlways]
    public class OutlineTarget : MonoBehaviour
    {
        public static readonly List<OutlineTarget> Active = new();

        [Required] public Renderer Renderer;
        public Color Colour = Color.white;
        [Min(0f)] public float MaxDrawDistance;
        [SerializeField, Range(0f, 1f)] public float OutlineWidthFactor = 1f;

        private void OnValidate()
        {
            if (!Renderer) Renderer = GetComponentInChildren<Renderer>();
        }

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }
    }
}