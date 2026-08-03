using Game;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CheckpointBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _mainGroup;

        [SerializeField] private Image _image;
        [SerializeField] private Animator _animator;

        [ReadOnly] public Checkpoint Checkpoint;

        public AK.Wwise.Event CheckpointJingle;

        private void Start()
        {
            _animator.runtimeAnimatorController = Checkpoint.AnimatorController;
            _mainGroup.alpha = 0;
         
            Tween.Delay(1.5f, () =>
            {
                if (this && gameObject) CheckpointJingle.Post(gameObject);
            }, warnIfTargetDestroyed: false);

            Sequence.Create()
                .Group(Tween.Alpha(_mainGroup, 1, 2.5f, ease: Ease.InOutCubic))
                .ChainDelay(3f)
                .Chain(Tween.Alpha(_mainGroup, 0, 3f, ease: Ease.InOutCubic))
                .OnComplete(() => Destroy(gameObject), false);
        }
    }
}