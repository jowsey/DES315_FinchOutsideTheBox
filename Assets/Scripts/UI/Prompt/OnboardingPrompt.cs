using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;
using Event = AK.Wwise.Event;

namespace UI
{
    public class OnboardingPrompt : MonoBehaviour
    {
        [SerializeField] private InputActionReference _satisfyAction;
        [SerializeField] private OnboardingPrompt _nextStep;

        [SerializeField] private CanvasGroup _canvasGroup;

        [SerializeField] private float _transitionDuration = 0.75f;

        [SerializeField] private Event _satisfySfx;
        [SerializeField] private Event _appearSfx;

        [SerializeField] private InputIconManager _inputIconManager;
        
        public static bool EnableDetection = true;
        
        private RectTransform _rt;

        private bool _satisfied;

        private void OnValidate()
        {
            if (!_canvasGroup) _canvasGroup = GetComponent<CanvasGroup>();
            if (!_inputIconManager) _inputIconManager = GetComponentInChildren<InputIconManager>();
        }

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _inputIconManager.SetAction(_satisfyAction);
        }

        public void Start()
        {
            _appearSfx?.Post(gameObject);

            Tween.Alpha(_canvasGroup, 0f, 1f, _transitionDuration, Ease.OutBack);
            Tween.UIAnchoredPosition(_rt, _rt.anchoredPosition - _rt.sizeDelta * Vector2.up, _rt.anchoredPosition, _transitionDuration, Ease.OutBack);
        }

        private void Update()
        {
            if (_satisfied || !EnableDetection || !_satisfyAction.action.WasPerformedThisFrame()) return;
            _satisfied = true;

            Tween.Delay(1.5f).OnComplete(() =>
            {
                Complete();

                Tween.Delay(_transitionDuration * 2f).OnComplete(() =>
                {
                    if (_nextStep) _nextStep.gameObject.SetActive(true);
                }, warnIfTargetDestroyed: false);
            }, warnIfTargetDestroyed: false);
        }

        private void Complete()
        {
            _satisfySfx?.Post(gameObject);

            Tween.Alpha(_canvasGroup, 1f, 0f, _transitionDuration, Ease.InBack);
            Tween.UIAnchoredPosition(_rt, _rt.anchoredPosition - _rt.sizeDelta * Vector2.up, _transitionDuration, Ease.InBack)
                .OnComplete(() => gameObject.SetActive(false), warnIfTargetDestroyed: false);
        }
    }
}