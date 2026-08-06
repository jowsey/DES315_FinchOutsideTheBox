using Game.Items.Equipments;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class YarnDistanceVisual : MonoBehaviour
    {
        private const float LerpSpeed = 6f;
        private const float ApproachingMaxRatio = 0.85f;
        private const float DirectionUpdateLengthDelta = 0.5f;
        private const float TransitionDuration = 0.1f;

        private static readonly Color ValidColour = Color.softGreen;
        private static readonly Color ApproachingMaxColour = Color.softYellow;
        private static readonly Color InvalidColour = Color.softRed;

        [SerializeField] private Image _barBackground;
        [SerializeField] private Image _barFill;
        [SerializeField] private Image _catIcon;

        private YarnEquipment _yarn;
        private float _lastRecordedLength;

        public void Build(YarnEquipment yarn)
        {
            _yarn = yarn;
        }

        private void Update()
        {
            var maxSize = _barBackground.rectTransform.rect.width;
            var ratio = _yarn.TotalLineSize / _yarn.MaxLineSize;

            var newOffset = Mathf.Clamp(-maxSize * (1 - ratio), -maxSize, 0);
            _barFill.rectTransform.offsetMax = new Vector2(
                Mathf.Lerp(_barFill.rectTransform.offsetMax.x, newOffset, 1 - Mathf.Exp(-LerpSpeed * Time.deltaTime)),
                _barFill.rectTransform.offsetMax.y
            );

            _barFill.color = ratio < ApproachingMaxRatio ? ValidColour : ratio < 1f ? ApproachingMaxColour : InvalidColour;

            // moving direction
            var delta = _yarn.TotalLineSize - _lastRecordedLength;
            if (Mathf.Abs(delta) > DirectionUpdateLengthDelta)
            {
                var forwards = delta > 0;
                Tween.ScaleX(_catIcon.transform, forwards ? 1 : -1, TransitionDuration, Ease.InOutCubic);

                _lastRecordedLength = _yarn.TotalLineSize;
            }
        }
    }
}