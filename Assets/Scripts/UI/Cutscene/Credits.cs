using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class Credits : MonoBehaviour
    {
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; }
        
        [SerializeField] private float _pixelsPerSecond = 100;
        [SerializeField] private InputActionReference _speedUpAction;

        private Tween _scrollTween;

        [SerializeField] private float _speedUpMultiplier = 3.5f;

        public readonly UnityEvent OnCreditsFinished = new();

        private void Start()
        {
            var canvasScaler = GetComponentInParent<CanvasScaler>();
            var rt = (RectTransform)transform;

            FullRebuildBottomUp(rt);

            var contentHeight = rt.rect.height;
            var canvasHeight = canvasScaler.referenceResolution.y;
            var totalHeight = contentHeight + canvasHeight;

            _scrollTween = Tween.UIAnchoredPositionY(rt, 0, totalHeight, totalHeight / _pixelsPerSecond, Ease.Linear)
                .OnComplete(() => Destroy(gameObject));
        }

        private void OnDestroy()
        {
            if (_scrollTween.isAlive) _scrollTween.Stop();
            OnCreditsFinished.Invoke();
        }

        private void Update()
        {
            if (_speedUpAction.action.WasPressedThisFrame())
            {
                _scrollTween.timeScale = _speedUpMultiplier;
            }
            else if (_speedUpAction.action.WasReleasedThisFrame())
            {
                _scrollTween.timeScale = 1f;
            }
        }

        private static void FullRebuildBottomUp(RectTransform rt)
        {
            foreach (RectTransform child in rt)
            {
                FullRebuildBottomUp(child);
            }

            if (rt.GetComponent<ILayoutElement>() != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
        }
    }
}