using System;
using PrimeTween;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class InteractPrompt : MonoBehaviour
    {
        [Serializable]
        public class InteractPromptConfiguration
        {
            public string Label;
            public InputActionReference ActionReference;
        }

        [field: SerializeField] public WorldFollowUI WorldFollowUI { get; private set; }

        [SerializeField] private TextMeshProUGUI _promptLabel;
        [SerializeField] private InputIconManager _inputIconManager;

        [SerializeField] private float _transitionDuration = 0.25f;

        private void OnValidate()
        {
            if (!_promptLabel) _promptLabel = GetComponentInChildren<TextMeshProUGUI>();
            if (!_inputIconManager) _inputIconManager = GetComponentInChildren<InputIconManager>();
            if (!WorldFollowUI) WorldFollowUI = GetComponent<WorldFollowUI>();
        }

        private void OnEnable()
        {
            var initialScale = transform.localScale;
            transform.localScale = Vector3.zero;
            Tween.Scale(transform, initialScale, _transitionDuration, Ease.OutCubic);
        }

        public void Build(InteractPromptConfiguration config)
        {
            _promptLabel.text = config.Label;
            _inputIconManager.SetAction(config.ActionReference);

            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)transform);
        }

        public void Destroy()
        {
            Tween.Scale(transform, transform.localScale, Vector3.zero, _transitionDuration, Ease.InBack)
                .OnComplete(() => Destroy(gameObject));
        }
    }
}