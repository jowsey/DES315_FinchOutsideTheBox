using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class StatusEffectBox : MonoBehaviour
    {
        private PlayerController.PlayerStatusEffect _effect;

        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _durationText;
        
        [SerializeField] private CanvasGroup _canvasGroup;

        private const float TransitionDuration = 0.75f;

        private string RenderDurationTimer()
        {
            var secondsRemaining = (_effect.StartTime + _effect.Duration) - Time.time;
            return secondsRemaining > 0 ? TimeSpan.FromSeconds(secondsRemaining).ToString("mm\\:ss") : "00:00";
        }

        public void Build(PlayerController.PlayerStatusEffect effect)
        {
            _effect = effect;
            _nameText.text = _effect.DisplayName;
            _durationText.text = RenderDurationTimer();
            // _iconImage.sprite =  // todo
        }

        private void Update()
        {
            if (_effect != null)
            {
                _durationText.text = RenderDurationTimer();
            }
        }

        private void OnEnable()
        {
            _canvasGroup.alpha = 0f;
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform.parent);
            
            var rt = (RectTransform)transform;
            Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x - rt.sizeDelta.x, rt.anchoredPosition.x, TransitionDuration, Ease.OutBack);
            Tween.Alpha(_canvasGroup, 0f, 1f, TransitionDuration, Ease.OutBack);
        }

        public void Destroy()
        {
            var rt = (RectTransform)transform;
            Sequence.Create()
                .Group(Tween.UIAnchoredPositionX(rt, rt.anchoredPosition.x, rt.anchoredPosition.x - rt.sizeDelta.x, TransitionDuration, Ease.InBack))
                .Group(Tween.Alpha(_canvasGroup, 1f, 0f, TransitionDuration, Ease.InBack))
                .OnComplete(() => Destroy(gameObject));
        }
    }
}