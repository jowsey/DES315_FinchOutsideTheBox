using PrimeTween;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace UI
{
    public class Credits : MonoBehaviour
    {
        [SerializeField] private float _pixelsPerSecond = 100;

        private void Start()
        {
            var director = FindAnyObjectByType<PlayableDirector>();
            director.Pause();

            var canvasScaler = GetComponentInParent<CanvasScaler>();
            var rt = (RectTransform)transform;

            FullRebuildBottomUp(rt);

            var contentHeight = rt.rect.height;
            var canvasHeight = canvasScaler.referenceResolution.y;
            var totalHeight = contentHeight + canvasHeight;

            Tween.UIAnchoredPositionY(rt, 0, totalHeight, totalHeight / _pixelsPerSecond, Ease.Linear)
                .OnComplete(() =>
                {
                    director.Resume();
                    Destroy(gameObject);
                });
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