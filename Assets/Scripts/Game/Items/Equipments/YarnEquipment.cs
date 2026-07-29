using System.Collections;
using System.Collections.Generic;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class YarnEquipment : PlaceableEquipment
    {
        [SerializeField] private ConfigurableJoint _yarnSegmentPrefab;
        [SerializeField] private ConfigurableJoint _yarnBallPrefab;
        [SerializeField, Min(1)] private int _maxSegments;

        [SerializeField, Required] private LineRenderer _line;
        [SerializeField] private LayerMask _ropeCollideMask;

        private readonly List<Transform> _positions = new();
        public bool IsHooking { get; private set; }

        private static readonly Color ValidColour = new(0f, 1f, 0f, 0.5f);
        private static readonly Color InvalidColour = new(1f, 0f, 0f, 0.5f);

        private const float _thickness = 0.15f;
        private const float _segmentLength = 0.5f;

        private Vector3 _lastSightedPosition;
        private Vector3 _holderTrackingPosition => _holder.transform.position + _holder.transform.up * 0.5f;
        private Vector3 _previewTrackingPosition => _previewInstance.transform.position + _previewInstance.transform.up * 0.1f;

        private YarnHookPoint _hookPoint;

        protected override void OnStateChanged(ItemState oldState, ItemState newState)
        {
            switch (oldState)
            {
                case ItemState.Held:
                {
                    if (_holder.isLocalPlayer)
                    {
                        SetPreviewVisible(false);
                        IsHooking = false;

                        _positions.Clear();
                        _line.positionCount = 0;
                    }

                    break;
                }
            }

            base.OnStateChanged(oldState, newState);
        }

        protected override void Awake()
        {
            base.Awake();
            SetPreviewVisible(false);
        }

        public void TryStartHook(YarnHookPoint hookPoint)
        {
            _hookPoint = hookPoint;

            var hookChild = new GameObject("YarnPoint");
            hookChild.transform.parent = hookPoint.transform;
            hookChild.transform.localPosition = Vector3.zero;

            _positions.Clear();
            _positions.Add(hookChild.transform);

            IsHooking = true;
            SetPreviewVisible(true);
        }

        private float GetTotalLineLength()
        {
            var total = 0f;
            for (var i = 0; i < _line.positionCount - 1; i++)
            {
                total += Vector3.Distance(_line.GetPosition(i), _line.GetPosition(i + 1));
            }

            return total;
        }

        private void FixedUpdate()
        {
            if (IsHooking)
            {
                _line.positionCount = _positions.Count + 2;
                for (var i = 0; i < _positions.Count; i++)
                {
                    _line.SetPosition(i, _positions[i].position);
                }

                _line.SetPosition(_line.positionCount - 2, _holderTrackingPosition);
                _line.SetPosition(_line.positionCount - 1, _previewTrackingPosition);

                // los check
                var segmentStartPos = _positions[^1].position;
                var ray = new Ray(segmentStartPos, _holderTrackingPosition - segmentStartPos);
                var queriesHitBackfaces = Physics.queriesHitBackfaces;
                Physics.queriesHitBackfaces = true;
                var didHit = Physics.SphereCast(ray, _thickness, out var hit, 1000f, _ropeCollideMask, QueryTriggerInteraction.Ignore);
                Physics.queriesHitBackfaces = queriesHitBackfaces;

                var hasLineOfSight = didHit && hit.collider.attachedRigidbody == _holder.Rb;

                var lineColour = hasLineOfSight && GetTotalLineLength() < _segmentLength * _maxSegments
                    ? ValidColour
                    : InvalidColour;
                _line.startColor = lineColour;
                _line.endColor = lineColour;

                if (hasLineOfSight)
                {
                    _lastSightedPosition = _holderTrackingPosition;
                }
                else
                {
                    // add a new point to the line
                    var segmentRoundedLineLength = Mathf.Ceil(hit.distance / _segmentLength) * _segmentLength;
                    var lineEndPosition = segmentStartPos + (_lastSightedPosition - segmentStartPos).normalized * segmentRoundedLineLength;

                    var newPoint = new GameObject("YarnPoint");
                    newPoint.transform.position = lineEndPosition;
                    _positions.Add(newPoint.transform);
                }

                // check whether we can backtrack
                if (_positions.Count >= 2)
                {
                    do
                    {
                        var previousPos = _positions[^2].position;
                        ray = new Ray(previousPos, _holderTrackingPosition - previousPos);
                        didHit = Physics.SphereCast(ray, _thickness, out hit, 1000f, _ropeCollideMask, QueryTriggerInteraction.Ignore);

                        hasLineOfSight = didHit && hit.collider.attachedRigidbody == _holder.Rb;
                        if (!hasLineOfSight) break;

                        // remove the last point
                        var lastPoint = _positions[^1];
                        _positions.RemoveAt(_positions.Count - 1);
                        Destroy(lastPoint.gameObject);
                    } while (_positions.Count >= 2);
                }
            }
        }

        public override void TryUse()
        {
            if (!IsHooking || !_hookPoint) return;
            if (GetTotalLineLength() > _segmentLength * _maxSegments) return;

            base.TryUse();
        }

        protected override void OnServerUse()
        {
            // we defer until CmdPlaceRope completes, ask client for rope positions instead
            // todo waterfall
            TargetOnClientPlace(_holderIdentity.connectionToClient);
        }

        [TargetRpc]
        private void TargetOnClientPlace(NetworkConnectionToClient target)
        {
            var positions = new Vector3[_line.positionCount];
            _line.GetPositions(positions);
            CmdPlaceRope(_hookPoint, positions[1..^1]);

            IsHooking = false;
        }

        [Command(requiresAuthority = false)]
        private void CmdPlaceRope(YarnHookPoint hookPoint, Vector3[] positions)
        {
            Debug.Log($"Received {positions.Length} positions");

            var regeneratedPositions = new List<Vector3>(positions.Length + 2);
            regeneratedPositions.Add(hookPoint.transform.position);
            regeneratedPositions.AddRange(positions);
            // regeneratedPositions.Add(_holderTrackingPosition);
            regeneratedPositions.Add(_placeInstance.transform.position);

            StartCoroutine(BuildSegments());

            base.OnServerUse();
            return;

            IEnumerator BuildSegments()
            {
                var previousBody = hookPoint.AttachedBody;

                const float segmentInterval = 0.02f;

                for (var pointI = 0; pointI < regeneratedPositions.Count - 1; pointI++)
                {
                    var point = regeneratedPositions[pointI];
                    var nextPoint = regeneratedPositions[pointI + 1];
                    var numSegments = Mathf.CeilToInt(Vector3.Distance(point, nextPoint) / _segmentLength) + 1; // extra segment for leeway

                    Debug.Log($"Line {pointI} has {numSegments} segments");

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
                            segment.connectedBody = hookPoint.AttachedBody;
                            segment.connectedAnchor = hookPoint.transform.localPosition;
                        }
                        else
                        {
                            // connection to previous segment
                            segment.connectedBody = previousBody;
                            segment.connectedAnchor = Vector3.forward * _segmentLength;
                        }

                        var segmentBody = segment.GetComponent<Rigidbody>();
                        previousBody = segmentBody;
                    }
                }

                _placeInstance.GetComponent<ConfigurableJoint>().connectedBody = previousBody;

                // var yarnBall = Instantiate(
                //     _yarnBallPrefab,
                //     previousBody.position + previousBody.transform.forward * _segmentLength,
                //     Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f))
                // );
                // NetworkServer.Spawn(yarnBall.gameObject);
                // yarnBall.connectedBody = previousBody;

                // Tween.Delay(1f).OnComplete(() => yarnBall.GetComponent<Rigidbody>().AddForce(anchor.transform.forward * 250f, ForceMode.Impulse));
            }
        }
    }
}