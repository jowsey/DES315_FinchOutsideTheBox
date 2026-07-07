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
        [SerializeField] [Required] private InputActionReference _submitInputAction;

        private bool _inputFieldActive;
        private bool _closedThisFrame; // workaround to the fact that our close and open buttons are the same

        [Command(requiresAuthority = false)]
        private void CmdSendMessage(string message, NetworkConnectionToClient sender = null)
        {
            if (string.IsNullOrWhiteSpace(message) || message.Length > _inputField.characterLimit) return;

            RpcReceiveMessage(sender!.identity, message);
        }

        public void DisplayLocalMessage(PlayerController player, string message)
        {
            if (!player) return;

            var item = Instantiate(_textChatItemPrefab, transform);
            item.Build(player, message);

            var rt = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        [ClientRpc]
        private void RpcReceiveMessage(NetworkIdentity sender, string message)
        {
            var player = sender.GetComponent<PlayerController>();
            DisplayLocalMessage(player, message);
        }

        private void OnEnable()
        {
            _inputField.onDeselect.AddListener(OnDeselect);
            _inputFieldCanvasGroup.alpha = 0;

            Toggle(false);
        }

        private void OnDisable()
        {
            _inputField.onDeselect.RemoveListener(OnDeselect);
        }

        private void OnDeselect(string _)
        {
            if (!_inputFieldActive) return;
            Toggle(false);
        }

        private void Update()
        {
            // Open
            if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.ToggleTextChat) &&
                !_inputFieldActive &&
                !_closedThisFrame &&
                _activateInputAction.action.WasPressedThisFrame())
            {
                Toggle(true);
                return;
            }

            _closedThisFrame = false;

            // Close on exit, or when submitting with empty message
            if (_inputFieldActive &&
                (_closeInputAction.action.WasPressedThisFrame() ||
                 (_submitInputAction.action.WasPressedThisFrame() && string.IsNullOrWhiteSpace(_inputField.text))))
            {
                Toggle(false);
                return;
            }

            // Submit
            // Message is guarded as non-empty by this point
            if (_inputFieldActive && _submitInputAction.action.WasPressedThisFrame())
            {
                CmdSendMessage(_inputField.text);

                _inputField.text = "";
                Toggle(false);
                return;
            }

            // Patch default Unity behaviour
            if (_inputFieldActive && !_inputField.isFocused && _submitInputAction.action.IsPressed())
            {
                _inputField.ActivateInputField();
                return;
            }
        }

        private void Toggle(bool toggle)
        {
            Tween.CompleteAll(_inputFieldCanvasGroup);

            _inputFieldCanvasGroup.interactable = toggle;
            _inputFieldCanvasGroup.blocksRaycasts = toggle;

            _inputFieldActive = toggle;

            if (toggle)
            {
                if (_inputFieldCanvasGroup.alpha < 1)
                    Tween.Alpha(_inputFieldCanvasGroup, 1f, 0.1f, Ease.OutCubic);

                const PlayerController.ControlBlockerFlags controllerBlockerFlags = PlayerController.ControlBlockerFlags.All;
                PlayerController.AddControlBlockerFlags(this, controllerBlockerFlags);

                _inputField.ActivateInputField();
                Cursor.lockState = CursorLockMode.None;
            }
            else
            {
                if (_inputFieldCanvasGroup.alpha > 0)
                    Tween.Alpha(_inputFieldCanvasGroup, 0f, 0.1f, Ease.InCubic);

                PlayerController.RemoveAllControlBlockerFlags(this);

                _inputField.DeactivateInputField();
                Cursor.lockState = CursorLockMode.Locked;

                _closedThisFrame = true;
            }
        }
    }
}