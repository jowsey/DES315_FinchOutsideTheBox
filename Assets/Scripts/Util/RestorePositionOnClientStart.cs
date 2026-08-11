using Mirror;
using UnityEngine;

namespace Util
{
    public class RestorePositionOnClientStart : NetworkBehaviour
    {
        private RectTransform _rt => (RectTransform)transform;
        private Vector3 _originalAnchoredPosition;

        private void Awake()
        {
            _originalAnchoredPosition = _rt.anchoredPosition;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _rt.anchoredPosition = _originalAnchoredPosition;
        }
    }
}