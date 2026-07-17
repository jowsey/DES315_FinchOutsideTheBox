using PrimeTween;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace UI
{
    public class LoadingScreen : MonoBehaviour
    {
        public enum AnimateDirection
        {
            In,
            Out
        }

        [SerializeField] private CanvasGroup _artworkGroup;
        [SerializeField] private RectTransform _bottomBar;
        [SerializeField] private Image _spinner;
        [SerializeField] private Image _progressBar;

        [SerializeField] private float _transitionDuration = 0.5f;

        public UnityEvent OnFinishAnimateIn = new();

        public void Animate(AnimateDirection direction)
        {
            var dirIn = direction == AnimateDirection.In;

            Sequence.Create()
                .Group(Tween.Alpha(_artworkGroup, dirIn ? 0f : 1f, dirIn ? 1f : 0f, _transitionDuration, Ease.InOutCubic))
                .Group(Tween.UIAnchoredPositionY(_bottomBar, dirIn ? -_bottomBar.sizeDelta.y : 0f, dirIn ? 0f : -_bottomBar.sizeDelta.y, _transitionDuration, Ease.InOutCubic))
                .OnComplete(() =>
                {
                    if (dirIn)
                        OnFinishAnimateIn.Invoke();
                    else
                        Destroy(gameObject);
                });
        }

        public void OnEnable()
        {
            DontDestroyOnLoad(gameObject);

            // prioritise smooth frames over i/o speed
            Application.backgroundLoadingPriority = ThreadPriority.Low;

            Tween.LocalRotationAtSpeed(_spinner.rectTransform, new Vector3(0f, 0f, -180f), 180f, cycles: -1, cycleMode: CycleMode.Incremental);
            // unity LoadSceneAsync is lowkey not even async so
            Tween.UIFillAmount(_progressBar, 0f, 1f, 5f, Ease.OutExpo);
            Tween.Scale(_artworkGroup.transform, Vector3.one, Vector3.one * 1.05f, 5f);
            
            Animate(AnimateDirection.In);
        }
    }
}