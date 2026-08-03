using System.Collections.Generic;
using Game.Items.Equipments;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class Checkpoint : RespawnTarget
    {
        public string AreaName = "Unnamed Checkpoint";

        [field: SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] public RuntimeAnimatorController AnimatorController { get; private set; }

        public List<Sandcastle> Sandcastles = new();
        
        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.scene.name != null)
            {
                name = $"Checkpoint [{AreaName}]";
            }
        }
    }
}