using Mirror;
using PrimeTween;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class Rope : Equipment
    {
        [SerializeField] private GameObject _ropeAnchorPrefab;
        [SerializeField] private ConfigurableJoint _ropeSegmentPrefab;
        [SerializeField, Min(1)] private int _numSegments = 25;

        protected override bool OnServerUse()
        {
            if (!base.OnServerUse()) return false;

            var anchorPosition = _holder.transform.TransformPoint(-0.5f, 0f, 1f);
            var segmentLength = _ropeSegmentPrefab.connectedAnchor.z;

            const float groundSeekDepth = 2.5f;
            var ray = new Ray(anchorPosition + Vector3.up * groundSeekDepth, Vector3.down);
            if (Physics.Raycast(ray, out var hit, groundSeekDepth * 2, ~LayerMask.GetMask("Player", "Rope"), QueryTriggerInteraction.Ignore))
            {
                anchorPosition = hit.point;
            }
            else
            {
                return false;
            }
            
            var anchor = Instantiate(_ropeAnchorPrefab, anchorPosition, Quaternion.Euler(0f, _holder.transform.localEulerAngles.y, 0f));
            var anchorCollider = anchor.GetComponentInChildren<Collider>();
            NetworkServer.Spawn(anchor);

            var previousBody = anchor.GetComponent<Rigidbody>();

            for (var i = 0; i < _numSegments; i++)
            {
                var offset = i == 0 ? Vector3.zero : previousBody.transform.forward * segmentLength;
                var segment = Instantiate(
                    _ropeSegmentPrefab,
                    previousBody.position + offset,
                    Quaternion.Euler(-0.5f, previousBody.transform.localEulerAngles.y + Random.Range(30f, 75f), 0f)
                );
                NetworkServer.Spawn(segment.gameObject);

                if (anchorCollider)
                {
                    Physics.IgnoreCollision(segment.GetComponentInChildren<Collider>(), anchorCollider);
                }

                if (i == 0)
                {
                    // connection to ground anchor is unique 
                    segment.connectedAnchor = new Vector3(0f, 0.1f, 0f);
                }

                segment.connectedBody = previousBody;
                previousBody = segment.GetComponent<Rigidbody>();
            }

            Tween.Delay(2f).OnComplete(() => previousBody.AddForce(anchor.transform.forward * 250f, ForceMode.Impulse));
            return true;
        }
    }
}