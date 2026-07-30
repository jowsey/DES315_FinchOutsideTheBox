using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class YarnRope
    {
        public YarnEquipment ParentEquipment;
        
        public readonly List<YarnSegment> Segments = new();
        public GameObject GroundAnchor;
        
        [Server]
        public void ServerDetach(Vector3 itemPosition)
        {
            foreach (var segments in Segments)
            {
                NetworkServer.Destroy(segments.gameObject);
            }

            Segments.Clear();

            ParentEquipment.ServerSetIdle();
            ParentEquipment.Rb.position = itemPosition;
        }
    }
}