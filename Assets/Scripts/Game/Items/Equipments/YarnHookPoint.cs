using Mirror;
using UnityEngine;

namespace Game
{
    public class YarnHookPoint : NetworkBehaviour
    {
        public Rigidbody AttachedBody;

        protected override void OnValidate()
        {
            base.OnValidate();
            AttachedBody = GetComponentInParent<Rigidbody>();
        }
    }
}