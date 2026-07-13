using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class Rope : Equipment
    {
        [SerializeField] private GameObject _ropeAnchorPrefab;
        [SerializeField] private ConfigurableJoint _ropeSegmentPrefab;
        [SerializeField, Min(1)] private int _numSegments = 25;

        protected override void OnServerUse()
        {
            base.OnServerUse();

            var anchorPosition = _holder.transform.TransformPoint(1f, 0f, -1f);

            // todo have them place it on the ground themselves?
            const float groundSeekDepth = 2.5f;
            var anchorGroundPosition = anchorPosition;
            var ray = new Ray(anchorPosition + Vector3.up * groundSeekDepth, Vector3.down);
            if (Physics.Raycast(ray, out var hit, groundSeekDepth * 2, ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
            {
                anchorGroundPosition = hit.point;
            }
            
            var anchor = Instantiate(_ropeAnchorPrefab, anchorGroundPosition, Quaternion.identity);
            NetworkServer.Spawn(anchor);

            var previousBody = anchor.GetComponent<Rigidbody>();

            for (var i = 0; i < _numSegments; i++)
            {
                var offset = i == 0 ? Vector3.zero : _holder.transform.forward * 0.5f;
                var segment = Instantiate(_ropeSegmentPrefab, previousBody.position + offset, _holder.transform.rotation);
                NetworkServer.Spawn(segment.gameObject);

                if (i == 0)
                {
                    // connection to ground anchor is unique 
                    segment.connectedAnchor = new Vector3(0f, 0.1f, 0f);
                }

                segment.connectedBody = previousBody;
                previousBody = segment.GetComponent<Rigidbody>();
            }
        }
    }
}