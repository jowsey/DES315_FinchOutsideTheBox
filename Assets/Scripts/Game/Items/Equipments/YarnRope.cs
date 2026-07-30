using System.Collections.Generic;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class YarnRope
    {
        public YarnEquipment ParentEquipment;
        
        public readonly List<YarnSegment> Segments = new();
        public GameObject GroundAnchor;
    }
}