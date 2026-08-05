using System;
using Game;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class CheckpointBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _mainGroup;

        [SerializeField] private Image _image;
        [SerializeField] private TextMeshProUGUI _titleText;

        [ReadOnly] public Checkpoint Checkpoint;

        public AK.Wwise.Event CheckpointJingle;

        private Tween _textTween;

        private void Start()
        {
            _mainGroup.alpha = 0;
            _image.sprite = Checkpoint.BannerSprite;
            _titleText.text = Checkpoint.AreaName;

            // queue up sfx
            Tween.Delay(1.5f, () =>
            {
                if (this && gameObject) CheckpointJingle.Post(gameObject);
            }, warnIfTargetDestroyed: false);

            // animation sequence
            Sequence.Create()
                .Group(Tween.Alpha(_mainGroup, 1, 2.5f, ease: Ease.InOutCubic))
                .ChainDelay(3f)
                .Chain(Tween.Alpha(_mainGroup, 0, 3f, ease: Ease.InOutCubic))
                .OnComplete(() => Destroy(gameObject), false);

            // animate text to wave
            const float cycleLength = 1.5f;
            const float distance = 5f;

            _textTween = Tween.Custom(-distance, distance, cycleLength, value =>
            {
                _titleText.ForceMeshUpdate();
                var textInfo = _titleText.textInfo;

                for (var i = 0; i < textInfo.characterCount; i++)
                {
                    var charInfo = textInfo.characterInfo[i];
                    if (!charInfo.isVisible) continue;

                    var materialIndex = charInfo.materialReferenceIndex;
                    var vertexIndex = charInfo.vertexIndex;

                    var vertices = textInfo.meshInfo[materialIndex].vertices;
                    var effectiveValue = value * (i % 2 == 0 ? 1 : -1);

                    vertices[vertexIndex + 0].y += effectiveValue;
                    vertices[vertexIndex + 1].y += effectiveValue;
                    vertices[vertexIndex + 2].y += effectiveValue;
                    vertices[vertexIndex + 3].y += effectiveValue;
                }

                for (var i = 0; i < textInfo.meshInfo.Length; i++)
                {
                    textInfo.meshInfo[i].mesh.vertices = textInfo.meshInfo[i].vertices;
                    _titleText.UpdateGeometry(textInfo.meshInfo[i].mesh, i);
                }
            }, Ease.InOutSine, -1, CycleMode.Yoyo);
        }

        private void OnDestroy()
        {
            _textTween.Complete();
        }
    }
}