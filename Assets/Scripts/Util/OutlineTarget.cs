using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Util
{
    [ExecuteAlways]
    public class OutlineTarget : MonoBehaviour
    {
        public static readonly List<OutlineTarget> Active = new();

        [Required] public Renderer[] Renderers;
        public Color Colour = Color.white;
        [Min(0f)] public float MaxDrawDistance;
        [Range(0f, 1f)] public float WidthFactor = 1f;

        private void OnValidate()
        {
            Renderers ??= GetComponentsInChildren<Renderer>();
        }

        private void Awake()
        {
            Renderers ??= GetComponentsInChildren<Renderer>();
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