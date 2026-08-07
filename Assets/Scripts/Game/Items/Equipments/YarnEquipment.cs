using System.Collections;
using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UI;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class YarnEquipment : PlaceableEquipment
    {
        [SerializeField] private YarnSegment _yarnSegmentPrefab;
        [SerializeField] private ConfigurableJoint _yarnBallPrefab;
        [SerializeField] private YarnDistanceVisual _distanceVisualPrefab;
        [SerializeField, Min(1)] private int _maxSegments;

        private YarnDistanceVisual _distanceVisualInstance;

        [SerializeField] public AK.Wwise.RTPC YarnVol;
        [SerializeField] public AK.Wwise.Event YarnStretch;
        [SerializeField] public AK.Wwise.Event YarnOut;

        [SerializeField, Required] private LineRenderer _line;
        [SerializeField] private LayerMask _ropeCollideMask;

        private readonly List<Vector3> _positions = new();
        private YarnHookPoint _hookPoint;

        [field: SyncVar] public bool IsHooking { get; private set; }

        public float TotalLineSize { get; private set; }
        public float MaxLineSize => _maxSegments * _segmentLength;

        private const float _thickness = 0.15f;
        private const float _segmentLength = 0.5f;

        private Vector3 _lastSightedPosition;
        private Vector3 _holderTrackingPosition => StateData is HeldStateData heldData ? heldData.Holder.transform.position + heldData.Holder.transform.up * 0.5f : Vector3.zero;
        private Vector3 _previewTrackingPosition => _previewInstance.transform.position + _previewInstance.transform.up * 0.1f;

        protected override void Awake()
        {
            base.Awake();
            SetPreviewVisible(false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isServer && IsHooking)
            {
                CmdRequestLineState();
            }
        }

        protected override void UpdateState(ItemStateData oldState, ItemStateData newState)
        {
            switch (oldState)
            {
                case HeldStateData:
                {
                    if (isServer)
                    {
                        ServerStopHook();
                    }

                    break;
                }
            }

            base.UpdateState(oldState, newState);
        }

        public void TryStartHook(YarnHookPoint hookPoint)
        {
            CmdStartHook(hookPoint);
        }

        [Command(requiresAuthority = false)]
        private void CmdStartHook(YarnHookPoint hookPoint, NetworkConnectionToClient sender = null)
        {
            if (IsHooking) return;
            if (StateData is not HeldStateData heldData) return;
            if (sender!.identity.GetComponent<PlayerController>() != heldData.Holder) return;
            if (!hookPoint) return;

            _hookPoint = hookPoint;
            _positions.Clear();
            _positions.Add(hookPoint.transform.position);
            _lastSightedPosition = _holderTrackingPosition;

            IsHooking = true;
            RpcStartHooking(hookPoint);
            ClientOnHookStarted(hookPoint);
        }

        [ClientRpc]
        private void RpcStartHooking(YarnHookPoint hookPoint)
        {
            if (isServer) return;
            ClientOnHookStarted(hookPoint);
        }

        [ClientRpc]
        private void RpcAddPosition(Vector3 position)
        {
            if (isServer) return;
            _positions.Add(position);
        }

        [ClientRpc]
        private void RpcRemovePosition()
        {
            if (isServer) return;
            if (_positions.Count > 1)
            {
                _positions.RemoveAt(_positions.Count - 1);
            }
        }

        [ClientRpc]
        private void RpcStopHooking()
        {
            if (isServer) return;
            ClientOnHookStopped();
        }

        private void ClientOnHookStarted(YarnHookPoint hookPoint)
        {
            _hookPoint = hookPoint;
            _positions.Clear();
            _positions.Add(hookPoint.transform.position);

            SetPreviewVisible(true);

            YarnStretch.Post(gameObject);
            YarnVol.SetGlobalValue(1);

            if (StateData is HeldStateData { Holder: { isLocalPlayer: true } } && !_distanceVisualInstance)
            {
                _distanceVisualInstance = Instantiate(_distanceVisualPrefab, UIGlobals.MainCanvas.transform);
                _distanceVisualInstance.Build(this);
            }
        }

        private void ClientOnHookStopped()
        {
            _hookPoint = null;
            _positions.Clear();
            _line.positionCount = 0;

            SetPreviewVisible(false);

            YarnVol.SetGlobalValue(0);
            YarnStretch.Stop(gameObject);

            if (_distanceVisualInstance)
            {
                Destroy(_distanceVisualInstance.gameObject);
                _distanceVisualInstance = null;
            }
        }

        private void ServerStopHook()
        {
            if (!IsHooking) return;

            _hookPoint = null;
            _positions.Clear();
            IsHooking = false;
            RpcStopHooking();
            ClientOnHookStopped();
        }

        [Command(requiresAuthority = false)]
        private void CmdRequestLineState(NetworkConnectionToClient sender = null)
        {
            if (!IsHooking) return;
            TargetRpcLineState(sender, _hookPoint, _positions.ToArray());
        }

        [TargetRpc]
        private void TargetRpcLineState(NetworkConnectionToClient target, YarnHookPoint hookPoint, Vector3[] positions)
        {
            ClientOnHookStarted(hookPoint);

            _positions.Clear();
            _positions.AddRange(positions);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!IsHooking || StateData is not HeldStateData heldData) return;

            if (isServer)
            {
                if (!_hookPoint)
                {
                    ServerStopHook();
                    return;
                }

                var segmentStartPos = _positions[^1];

                // los check
                var ray = new Ray(segmentStartPos, _holderTrackingPosition - segmentStartPos);
                var queriesHitBackfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                var didHit = Physics.SphereCast(ray, _thickness, out var hit, 1000f, _ropeCollideMask, QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = queriesHitBackfaces;

                var hasLineOfSight = didHit && hit.collider.attachedRigidbody == heldData.Holder.Rb;
                if (hasLineOfSight)
                {
                    _lastSightedPosition = _holderTrackingPosition;
                }
                else
                {
                    // add a new point to the line
                    var segmentRoundedLineLength = Mathf.Ceil(hit.distance / _segmentLength) * _segmentLength;
                    var lineEndPosition = segmentStartPos + (_lastSightedPosition - segmentStartPos).normalized * segmentRoundedLineLength;

                    _positions.Add(lineEndPosition);
                    RpcAddPosition(lineEndPosition);
                }

                // check whether we can backtrack
                while (_positions.Count >= 2)
                {
                    var previousPos = _positions[^2];
                    ray = new Ray(previousPos, _holderTrackingPosition - previousPos);
                    didHit = Physics.SphereCast(ray, _thickness, out hit, 1000f, _ropeCollideMask, QueryTriggerInteraction.Ignore);

                    hasLineOfSight = didHit && hit.collider.attachedRigidbody == heldData.Holder.Rb;
                    if (!hasLineOfSight) break;

                    // remove the last point
                    _positions.RemoveAt(_positions.Count - 1);
                    RpcRemovePosition();
                }
            }
            else if (_positions.Count == 0)
            {
                _line.positionCount = 0;
                return;
            }

            // track local point for latency
            if (_hookPoint)
            {
                _positions[0] = _hookPoint.transform.position;
            }

            // update line
            _line.positionCount = _positions.Count + (_previewInstance ? 2 : 1);
            for (var i = 0; i < _positions.Count; i++)
            {
                _line.SetPosition(i, _positions[i]);
            }

            // dynamic final positions
            _line.SetPosition(_positions.Count, _holderTrackingPosition);
            if (_previewInstance) _line.SetPosition(_positions.Count + 1, _previewTrackingPosition);

            TotalLineSize = 0f;
            for (var i = 0; i < _line.positionCount - 1; i++)
            {
                TotalLineSize += Vector3.Distance(_line.GetPosition(i), _line.GetPosition(i + 1));
            }
        }

        public override void TryUse()
        {
            if (!IsHooking || !_hookPoint) return;
            if (TotalLineSize > MaxLineSize) return;

            base.TryUse();
        }

        protected override void OnServerUse()
        {
            if (!_hookPoint) return;
            if (TotalLineSize > MaxLineSize) return;

            var hookPoint = _hookPoint;
            var positions = new List<Vector3>(_positions) { _holderTrackingPosition, _placeInstance.transform.position };
            StartCoroutine(BuildSegments(hookPoint, positions));

            base.OnServerUse();
        }

        protected override void OnClientHolderSuccessfulUse()
        {
            base.OnClientHolderSuccessfulUse();
            YarnOut.Post(gameObject);
        }

        private IEnumerator BuildSegments(YarnHookPoint hookPoint, List<Vector3> regeneratedPositions)
        {
            var yarnRope = new YarnRope
            {
                ParentEquipment = this,
                GroundAnchor = _placeInstance
            };
            hookPoint.AttachedRopes.Add(yarnRope);

            var previousBody = hookPoint.AttachedBody;

            const float segmentInterval = 0.02f;

            for (var pointI = 0; pointI < regeneratedPositions.Count - 1; pointI++)
            {
                var point = regeneratedPositions[pointI];
                var nextPoint = regeneratedPositions[pointI + 1];
                var numSegments = Mathf.CeilToInt(Vector3.Distance(point, nextPoint) / _segmentLength) + 1; // extra segment for leeway

                for (var segmentI = 0; segmentI < numSegments; segmentI++)
                {
                    yield return new WaitForSecondsRealtime(segmentInterval);

                    var lineDirection = (nextPoint - previousBody.position).normalized;

                    var firstSegment = pointI == 0 && segmentI == 0;
                    var segment = Instantiate(
                        _yarnSegmentPrefab,
                        firstSegment ? hookPoint.transform.position : previousBody.position + lineDirection * _segmentLength,
                        Quaternion.LookRotation(lineDirection, Vector3.up)
                    );
                    NetworkServer.Spawn(segment.gameObject);

                    if (firstSegment)
                    {
                        // connection to hook point
                        segment.Joint.connectedBody = hookPoint.AttachedBody;
                        segment.Joint.connectedAnchor = hookPoint.transform.localPosition;
                    }
                    else
                    {
                        // connection to previous segment
                        segment.Joint.connectedBody = previousBody;
                        segment.Joint.connectedAnchor = Vector3.forward * _segmentLength;
                    }

                    segment.ParentRope = yarnRope;
                    yarnRope.Segments.Add(segment);

                    previousBody = segment.Rb;
                }
            }

            _placeInstance.GetComponent<ConfigurableJoint>().connectedBody = previousBody;
        }
    }
}