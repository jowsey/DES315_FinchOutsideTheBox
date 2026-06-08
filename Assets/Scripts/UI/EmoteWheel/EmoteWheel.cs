using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class EmoteWheel : MonoBehaviour
    {
        [SerializeField] [Min(0)] private float _wheelRadius = 288f;
        [SerializeField] [Min(0)] private float _deadzoneRadius = 120f;
        [SerializeField] private float _transitionDuration = 0.15f;

        [SerializeField] private InputActionReference _openEmoteWheelAction;
        [SerializeField] private InputActionReference _explicitSelectAction;
        [SerializeField] private EmoteWheelItem.EmoteInfo[] _emoteOptions;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private EmoteWheelItem _itemPrefab;

        private List<EmoteWheelItem> _activeItems = new();
        private EmoteWheelItem _hoveredItem;

        private ShowState _state = ShowState.Closed;

        private void OnValidate()
        {
            if (!_canvasGroup) _canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            transform.localScale = Vector3.zero;
            _canvasGroup.alpha = 0;

            for (var i = 0; i < _emoteOptions.Length; i++)
            {
                var option = _emoteOptions[i];
                var item = Instantiate(_itemPrefab, transform);
                item.Build(option);

                var itemRect = (RectTransform)item.transform;
                var angle = (i / (float)_emoteOptions.Length) * Mathf.PI * 2 - Mathf.PI / 2;
                var position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * _wheelRadius;
                itemRect.anchoredPosition = position;

                _activeItems.Add(item);
            }
        }

        private void Update()
        {
            switch (_state)
            {
                case ShowState.Closed:
                {
                    if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Emote) && _openEmoteWheelAction.action.IsPressed())
                    {
                        _state = ShowState.Opening;

                        Cursor.lockState = CursorLockMode.None;
                        PlayerController.AddControlBlockerFlags(this, PlayerController.ControlBlockerFlags.All);

                        Sequence.Create()
                            .Group(Tween.Scale(transform, Vector3.zero, Vector3.one, _transitionDuration, Ease.OutCubic))
                            .Group(Tween.Alpha(_canvasGroup, 1, _transitionDuration, Ease.OutCubic))
                            .OnComplete(() => _state = ShowState.Open);
                    }

                    break;
                }
                case ShowState.Open:
                {
                    var mouseDirection = Mouse.current.position.ReadValue() - new Vector2(Screen.width, Screen.height) / 2;
                    if (mouseDirection.magnitude > _deadzoneRadius)
                    {
                        var angle = Mathf.Atan2(mouseDirection.y, mouseDirection.x);
                        var selectedIndex = Mathf.RoundToInt((angle + Mathf.PI / 2) / (Mathf.PI * 2) * _emoteOptions.Length) % _emoteOptions.Length;
                        if (selectedIndex < 0) selectedIndex += _emoteOptions.Length;

                        if (_hoveredItem != _activeItems[selectedIndex])
                        {
                            _hoveredItem?.SetSelected(false);
                            _hoveredItem = _activeItems[selectedIndex];
                            _hoveredItem.SetSelected(true);
                        }
                    }
                    else if (_hoveredItem)
                    {
                        _hoveredItem.SetSelected(false);
                        _hoveredItem = null;
                    }

                    if (!_openEmoteWheelAction.action.IsPressed() || _explicitSelectAction.action.WasPressedThisFrame())
                    {
                        _state = ShowState.Closing;

                        Cursor.lockState = CursorLockMode.Locked;
                        PlayerController.RemoveAllControlBlockerFlags(this);

                        Sequence.Create()
                            .Group(Tween.Scale(transform, Vector3.one, Vector3.zero, _transitionDuration, Ease.InCubic))
                            .Group(Tween.Alpha(_canvasGroup, 0, _transitionDuration, Ease.InCubic))
                            .OnComplete(() => _state = ShowState.Closed);

                        if (_hoveredItem)
                        {
                            PlayerController.LocalPlayer.Emoter.PlayEmote(_hoveredItem.Emote.TriggerName);

                            // push outward in direction. first attempt at making it more obvious which item was selected. needs work
                            var itemRect = (RectTransform)_hoveredItem.transform;
                            var originalPos = itemRect.anchoredPosition;
                            Tween.UIAnchoredPosition(itemRect, originalPos * 2.5f, _transitionDuration, Ease.OutCubic)
                                .OnComplete(() => itemRect.anchoredPosition = originalPos);

                            _hoveredItem.SetSelected(false);
                            _hoveredItem = null;
                        }
                    }

                    break;
                }
            }
        }
    }
}