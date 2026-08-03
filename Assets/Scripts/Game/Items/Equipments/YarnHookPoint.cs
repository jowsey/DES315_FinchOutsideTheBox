using System.Collections.Generic;
using Game.Items.Equipments;
using Mirror;
using UnityEngine;

namespace Game
{
    public class YarnHookPoint : NetworkBehaviour
    {
        public Rigidbody AttachedBody;
        public List<YarnRope> AttachedRopes = new();

        protected override void OnValidate()
        {
            base.OnValidate();
            if (!AttachedBody) AttachedBody = GetComponentInParent<Rigidbody>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RespawnTarget.OnPreRespawn.AddListener(OnServerPreRespawn);
        }
        
        public override void OnStopServer()
        {
            base.OnStopServer();
            RespawnTarget.OnPreRespawn.RemoveListener(OnServerPreRespawn);
        }

        [Server]
        private void OnServerPreRespawn(RespawnTarget target)
        {
            foreach (var rope in AttachedRopes)
            {
                // todo eventually this should probably move it back to where it was at the checkpoint, so position won't matter?
                var anchorTrans = rope.GroundAnchor.transform;
                rope.ServerDetach(anchorTrans.position + anchorTrans.up * 0.5f);
            }
        }
    }
}