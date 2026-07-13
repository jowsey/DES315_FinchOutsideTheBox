using UnityEngine;

namespace Game.Items.Equipments
{
    public class Rope : Equipment
    {
        [SerializeField] private GameObject _ropeAnchorPrefab;
        [SerializeField] private ConfigurableJoint _ropeSegmentPrefab;
        [SerializeField, Min(1)] private int _numSegments = 20;
        
        public override void Use()
        {
            base.Use();

            var anchor = Instantiate(_ropeAnchorPrefab, _holder.transform.TransformPoint(1f, 0f, -1f), Quaternion.identity);
            var previousSegment = anchor.GetComponent<Rigidbody>();

            for (var i = 0; i < _numSegments; i++)
            {
                var segment = Instantiate(_ropeSegmentPrefab, previousSegment.position + Vector3.forward * 0.5f, Quaternion.identity);
                segment.connectedBody = previousSegment;
                previousSegment = segment.GetComponent<Rigidbody>();
            }
        }
    }
}