using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace UI
{
    public class CheckpointBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _mainGroup;

        [SerializeField] private TextMeshProUGUI _taglineText;
        [SerializeField] private TextMeshProUGUI _areaNameText;

        [ReadOnly] public Checkpoint Checkpoint;

        public AK.Wwise.Event CheckpointJingle;

        private void Start()
        {
            _areaNameText.text = Checkpoint.AreaName;

            _mainGroup.alpha = 0;
            _taglineText.alpha = 0f;
            _areaNameText.alpha = 0f;

            Tween.Delay(1.5f, () =>
            {
                if (this && gameObject) CheckpointJingle.Post(gameObject);
            }, warnIfTargetDestroyed: false);

            Sequence.Create()
                .Group(Tween.Alpha(_mainGroup, 1, 2.5f, ease: Ease.InOutCubic))
                .Group(Tween.Alpha(_taglineText, 1, 3f, ease: Ease.InOutCubic, startDelay: 0.5f))
                .Group(Tween.Alpha(_areaNameText, 1, 3f, ease: Ease.InOutCubic, startDelay: 1f))
                .ChainDelay(3f)
                .Chain(Tween.Alpha(_mainGroup, 0, 3f, ease: Ease.InOutCubic))
                .OnComplete(() => Destroy(gameObject), false);
        }
    }
}