using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(LineRenderer))]
    [ExecuteAlways]
    public class ActionCurveLine : MonoBehaviour
    {
        private LineRenderer _lineRenderer;

        public float HeightCurveMultiplier = 1f;
        public Vector3 EndPoint;

        [Min(0)] public int Midpoints = 5;

        private void OnValidate()
        {
            Rerender();
        }

        private void Rerender()
        {
            if (!_lineRenderer) _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.positionCount = Midpoints + 2;

            var startPoint = transform.position;

            _lineRenderer.SetPosition(0, startPoint);
            _lineRenderer.SetPosition(Midpoints + 1, EndPoint);

            for (var i = 1; i <= Midpoints; i++)
            {
                var t = (float)i / (Midpoints + 1);
                var midPoint = Vector3.Lerp(startPoint, EndPoint, t);
                midPoint.y += Mathf.Sin(t * Mathf.PI) * HeightCurveMultiplier;
                _lineRenderer.SetPosition(i, midPoint);
            }
        }

        private void LateUpdate()
        {
            if (transform.hasChanged)
            {
                Rerender();
                transform.hasChanged = false;
            }
        }
    }
}