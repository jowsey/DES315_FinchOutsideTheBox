using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class EmoteWheelItem : MonoBehaviour
    {
        [Serializable]
        public class EmoteInfo
        {
            public string TriggerName;
            public Sprite Icon;
        }

        [SerializeField] private Image _backgroundImage;
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Animator _borderAnimator;

        [SerializeField] private float _hoveredScale = 1.2f;
        [SerializeField] private float _transitionDuration = 0.15f;

        [SerializeField] private Color _borderSelectedColour = Color.white;
        private Color _borderDefaultColour;

        public EmoteInfo Emote { get; private set; }

        private void OnEnable()
        {
            _borderDefaultColour = _borderImage.color;
            _borderAnimator.enabled = false;
        }

        public void Build(EmoteInfo emote)
        {
            Emote = emote;
            _iconImage.sprite = emote.Icon;
        }

        public void SetSelected(bool selected)
        {
            Tween.Scale(transform, selected ? Vector3.one * _hoveredScale : Vector3.one, _transitionDuration, Ease.OutCubic);

            _borderImage.color = selected ? _borderSelectedColour : _borderDefaultColour;
            _borderAnimator.enabled = selected;
        }
    }
}