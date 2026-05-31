using System.Collections.Generic;
using System.Linq;
using Mirror;
using PrimeTween;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace UI
{
    public class RespawnVoteBox : NetworkBehaviour
    {
        private enum ShowState
        {
            Closed,
            Closing,
            Open,
            Opening
        }

        [SerializeField] private InputActionReference _respawnAction;
        [SerializeField] private AK.Wwise.Event _respawnPing;

        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private TextMeshProUGUI _countText;
        [SerializeField] private Image _chargeImage;
        [SerializeField] private RectTransform _keyPrompt;
        [SerializeField] private TextMeshProUGUI _flaskCountOnRespawnText;

        [Tooltip("How much vote charge is generated per second of holding the respawn key?")]
        [SerializeField] [SuffixLabel("/second")] private float _chargeSpeed = 1 / 1.5f;

        [Tooltip("How long after the last activity the box should hide?")]
        [SerializeField] [SuffixLabel("seconds")] private float _activityCloseCooldown = 5f;

        [Tooltip("How long after stopping charging should it begin to undo?")]
        [SerializeField] [SuffixLabel("seconds")] private float _chargeUndoCooldown = 1.5f;

        [Tooltip("How much charge per second should be undone?")]
        [SerializeField] [SuffixLabel("/second")] private float _chargeUndoSpeed = 0.5f;

        [Tooltip("How long after a vote is cast should it no longer be valid?")]
        [SerializeField] [SuffixLabel("seconds")] private float _voteExpireCooldown = 15f;

        [Mirror.ShowInInspector] [Mirror.ReadOnly] private ShowState _state = ShowState.Closed;
        [Mirror.ShowInInspector] [Mirror.ReadOnly] private bool _keyPromptHidden;
        [Mirror.ShowInInspector] [Mirror.ReadOnly] private float _voteCharge;

        [SyncVar] private int _votesActive;
        [SyncVar] private int _votesRequired;

        private bool _voteLocked;

        // Timestamp of last change in activity.
        // Activity is anything that forces the panel to remain open.
        private float _lastActivityTime = float.NegativeInfinity;

        // Timestamp of last change in vote charge.
        private float _lastChargeTime = float.NegativeInfinity;

        // Used on server to keep track of which players have voted to respawn.
        private readonly Dictionary<NetworkConnectionToClient, float> _votedClients = new();

        private Vector2 _openPosition;
        private Vector2 _hiddenPosition => _openPosition + ((RectTransform)transform).sizeDelta * Vector2.up;

        private Cart _linkedCart;

        // Tracking when all flasks are lost
        private int _lastKnownFlaskCount;

        private void Awake()
        {
            // Initialize
            _canvasGroup.alpha = 0;
            var rt = (RectTransform)transform;
            _openPosition = rt.anchoredPosition;
            rt.anchoredPosition = _hiddenPosition;
            
            Checkpoint.RespawnEvent.AddListener(OnRespawn);
        }

        private void Start()
        {
            _linkedCart = FindAnyObjectByType<Cart>();
        }

        private void OnDestroy()
        {
            Checkpoint.RespawnEvent.RemoveListener(OnRespawn);
        }

        private void OnRespawn(Checkpoint checkpoint)
        {
            _voteLocked = false;
            _voteCharge = 0;

            ClosePanel(true);
        }

        private void ClosePanel(bool preventReopen = false)
        {
            if (_state is not ShowState.Closed and not ShowState.Closing)
            {
                _state = ShowState.Closing;

                Sequence.Create()
                    .Group(Tween.UIAnchoredPositionY((RectTransform)transform, _hiddenPosition.y, 0.75f, Ease.InBack))
                    .Group(Tween.Alpha(_canvasGroup, 0, 0.75f, Ease.InBack))
                    .OnComplete(() => _state = ShowState.Closed);
            }

            // Prevent re-opening next frame
            if (preventReopen)
            {
                _lastActivityTime = float.NegativeInfinity;
                _lastChargeTime = float.NegativeInfinity;
            }
        }

        private void Update()
        {
            if (isServer)
            {
                // Prune old votes
                foreach (var id in _votedClients.Keys.ToList())
                {
                    if (Time.time - _votedClients[id] > _voteExpireCooldown)
                    {
                        _votedClients.Remove(id);
                        TargetVoteExpired(id);
                    }
                }

                _votesActive = _votedClients.Count;
                _votesRequired = NetworkServer.connections.Values.Count(conn => conn?.identity);

                if (_votesActive >= _votesRequired)
                {
                    var cart = FindAnyObjectByType<Cart>(); // todo clean up, link to individual carts
                    cart.CmdInvokeRespawnEvent(cart.CurrentCheckpointIndex);

                    _votedClients.Clear();
                    _votesActive = 0;
                }
            }

            // Force active if respawn pressed, there are active votes, or we just lost our last flask
            if (_votesActive > 0 || (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Respawn) && _respawnAction.action.WasPressedThisFrame()) ||
                (_lastKnownFlaskCount > 0 && _linkedCart.NumCarriedFlasks == 0))
            {
                _lastActivityTime = Time.time;
            }

            switch (_state)
            {
                case ShowState.Closed:
                {
                    if (Time.time - _lastActivityTime < _activityCloseCooldown)
                    {
                        _state = ShowState.Opening;
                        _respawnPing.Post(gameObject);

                        Sequence.Create()
                            .Group(Tween.UIAnchoredPositionY((RectTransform)transform, _openPosition.y, 0.75f, Ease.OutBack))
                            .Group(Tween.Alpha(_canvasGroup, 1, 0.75f, Ease.OutBack))
                            .OnComplete(() => _state = ShowState.Open);
                    }

                    break;
                }
                case ShowState.Open:
                {
                    if (PlayerController.ControlEnabled(PlayerController.ControlBlockerFlags.Respawn) && _respawnAction.action.IsPressed())
                    {
                        // Charge vote
                        _lastActivityTime = Time.time;
                        _lastChargeTime = Time.time;

                        var oldCharge = _voteCharge;
                        _voteCharge = Mathf.Min(1, _voteCharge + Time.deltaTime * _chargeSpeed);
                        
                        // Detect passing boundary
                        const int divisions = 4;
                        if (Mathf.FloorToInt(oldCharge * divisions) < Mathf.FloorToInt(_voteCharge * divisions))
                        {
                            // We just passed a boundary, juice tick
                            Tween.Scale(_chargeImage.transform, Vector3.one * 1.2f, 0.12f, Ease.InCubic, 2, CycleMode.Rewind);
                        }
                        
                        if (_voteCharge >= 1 && !_voteLocked)
                        {
                            _voteLocked = true;
                            CmdCastVote();
                        }
                    }
                    else if (Time.time - _lastChargeTime > _chargeUndoCooldown && _voteCharge > 0 && !_voteLocked)
                    {
                        // Undo vote charge
                        _lastActivityTime = Time.time;
                        _voteCharge = Mathf.Max(0, _voteCharge - _chargeUndoSpeed * Time.deltaTime);
                    }

                    if (Time.time - _lastActivityTime > _activityCloseCooldown)
                    {
                        ClosePanel();
                    }

                    break;
                }
            }

            // Show/hide key prompt if starting/ending charge
            if (_voteCharge == 0 && _keyPromptHidden)
            {
                // Charge undo ending, animate prompt visible
                Tween.CompleteAll(_keyPrompt);
                Tween.Scale(_keyPrompt, Vector3.one, 0.3f, Ease.OutBack);
                _keyPromptHidden = false;
            }
            else if (_voteCharge > 0 && !_keyPromptHidden)
            {
                // Charge starting, animate prompt hidden
                Tween.CompleteAll(_keyPrompt);
                Tween.Scale(_keyPrompt, Vector3.zero, 0.3f, Ease.InBack);
                _keyPromptHidden = true;
            }

            if (_state != ShowState.Closed)
            {
                _chargeImage.fillAmount = _voteCharge;
                _countText.text = $"<b>{_votesActive}</b>/{_votesRequired}";
                _flaskCountOnRespawnText.text = $"You will respawn with <b>{_linkedCart.CheckpointFlaskCounts[_linkedCart.CurrentCheckpointIndex]}</b> flasks.";
            }

            _lastKnownFlaskCount = _linkedCart.NumCarriedFlasks;
        }

        [Command(requiresAuthority = false)]
        private void CmdCastVote(NetworkConnectionToClient sender = null)
        {
            if (_votedClients.ContainsKey(sender!)) return;

            _votedClients.Add(sender, Time.time);
            _votesActive = _votedClients.Count;
        }

        [TargetRpc]
        private void TargetVoteExpired(NetworkConnectionToClient target)
        {
            _voteLocked = false;
            _voteCharge = 0;
        }
    }
}