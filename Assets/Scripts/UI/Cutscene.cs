using System;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    [Serializable]
    public class CutsceneFrame
    {
        [PreviewField] public Sprite Sprite;
        [TextArea] public string Text;
    }

    public class Cutscene : MonoBehaviour
    {
        [SerializeField] [Required] private InputActionReference _nextFrameAction;
        [SerializeField] [Required] private InputActionReference _skipAction;

        [SerializeField] [Required] private CanvasGroup _canvasGroup;
        [SerializeField] [Required] private Image _frameImage;
        [SerializeField] [Required] private TextMeshProUGUI _frameText;
        [SerializeField] [Required] private Image _advanceFramePrompt;

        [SerializeField] [Required] private CutsceneFrame[] _frames;

        private int _currentFrame;

        private void DrawFrame(int index)
        {
            _frameImage.sprite = _frames[index].Sprite;
            _frameText.text = _frames[index].Text;
        }

        private void Start()
        {
            _canvasGroup.alpha = 1;
            transform.SetAsLastSibling();

            DrawFrame(0);
        }

        private void Update()
        {
            transform.SetAsLastSibling(); // force ensure we're absolutely on top always

            if (_skipAction.action.WasPressedThisFrame())
            {
                Destroy(gameObject);
                return;
            }
            
            if (Tween.GetTweensCount(_frameImage.transform) > 0 || Tween.GetTweensCount(_canvasGroup) > 0) return;

            if (_nextFrameAction.action.WasPressedThisFrame())
            {
                _currentFrame++;

                if (_currentFrame >= _frames.Length)
                {
                    Tween.Alpha(_canvasGroup, 0f, 2f, Ease.InCubic)
                        .OnComplete(() => Destroy(gameObject), false);
                    return;
                }

                // animate advance prompt
                Tween.Scale(_advanceFramePrompt.transform, Vector3.one * 0.9f, 0.1f, Ease.InCubic, 2, CycleMode.Yoyo);

                // animate next frame transition
                Sequence.Create()
                    .Group(Tween.Scale(_frameImage.transform, Vector3.zero, 0.75f, Ease.InBack))
                    .Group(Tween.Scale(_frameText.transform, Vector3.zero, 0.75f, Ease.InBack))
                    .ChainCallback(() => DrawFrame(_currentFrame), false)
                    .ChainDelay(0.25f)
                    .Chain(Tween.Scale(_frameImage.transform, Vector3.one, 1.0f, Ease.OutCubic))
                    .Group(Tween.Scale(_frameText.transform, Vector3.one, 1.0f, Ease.OutCubic))
                    .Chain(Tween.Scale(_advanceFramePrompt.transform, Vector3.one * 1.1f, 0.1f, Ease.InCubic, 2, CycleMode.Yoyo));
            }
        }
    }
}