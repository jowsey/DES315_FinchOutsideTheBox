using System;
using PrimeTween;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Util
{
    [Serializable]
    public class TweenInfo
    {
        public float Duration = 1f;
        public Ease Ease = Ease.Linear;
        public int Spins = 1;
    }

    [InfoBox("Makes an object spin when you click it. Built for the main menu easter egg.")]
    public class ClickToSpin : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private TweenInfo[] _tweens =
        {
            new() { Duration = 1f, Ease = Ease.InOutCubic, Spins = 1 },
        };

        private int _clicks;
        private Tween _activeTween;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_activeTween.isAlive) return;

            var info = _tweens[Mathf.Min(_clicks++, _tweens.Length - 1)];

            _activeTween = Tween.LocalEulerAngles(transform,
                transform.localEulerAngles,
                transform.localEulerAngles + Vector3.up * (info.Spins * 360),
                info.Duration,
                info.Ease
            );
        }
    }
}