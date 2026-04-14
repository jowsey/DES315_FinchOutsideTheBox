using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class TextChatFeed : NetworkBehaviour
    {
        [SerializeField] [Required] private TextChatItem _textChatItemPrefab;

        [SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] private TMP_InputField _inputField;
        [SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] private CanvasGroup _inputFieldCanvasGroup;

        [SerializeField] [Required] private InputActionReference _activateInputAction;
        [SerializeField] [Required] private InputActionReference _closeInputAction;

        private bool _inputFieldActive;
        private bool _closedThisFrame; // workaround to the fact that our close and open buttons are the same

        [Command(requiresAuthority = false)]
        private void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > _inputField.characterLimit) return;

            RpcReceiveMessage(sender!.identity, message);
        }

        [ClientRpc]
        private void RpcReceiveMessage(NetworkIdentity sender, string message)
        {
            var player = sender.GetComponent<PlayerController>();
            var item = Instantiate(_textChatItemPrefab, transform);
            item.Build(player, message);

            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private void OnEnable()
        {
            _inputField.onSubmit.AddListener(OnSubmit);
            _inputFieldCanvasGroup.alpha = 0;

            Toggle(false);
        }

        private void OnDisable()
        {
            _inputField.onSubmit.RemoveListener(OnSubmit);
        }

        private void OnSubmit(string message)
        {
            if (!_inputFieldActive) return;
            Toggle(false);

            if (string.IsNullOrWhiteSpace(message)) return;
            CmdSendMessage(message);

            _inputField.text = "";
        }

        private void Update()
        {
            if (PlayerController.ControlsEnabled && !_inputFieldActive && !_closedThisFrame && _activateInputAction.action.WasPressedThisFrame())
            {
                Toggle(true);
            }
            else if (_inputFieldActive && _closeInputAction.action.WasPressedThisFrame())
            {
                Toggle(false);
            }

            _closedThisFrame = false;
        }

        private void Toggle(bool active)
        {
            if (active)
            {
                Tween.Alpha(_inputFieldCanvasGroup, 1f, 0.1f, Ease.OutCubic);
                _inputField.ActivateInputField();

                PlayerController.AddControlBlocker(this);
            }
            else
            {
                Tween.Alpha(_inputFieldCanvasGroup, 0f, 0.1f, Ease.InCubic);
                _inputField.DeactivateInputField();

                PlayerController.RemoveControlBlocker(this);

                _closedThisFrame = true;
            }

            _inputFieldActive = active;
        }
    }
}