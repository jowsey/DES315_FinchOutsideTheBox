using System.Collections.Generic;
using Game.Items.Equipments;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game
{
    public class Checkpoint : RespawnTarget
    {
        public string AreaName = "Unnamed Checkpoint";

        [field: SerializeField] [RequiredIn(PrefabKind.PrefabInstanceAndNonPrefabInstance)] public Sprite BannerSprite { get; private set; }

        public List<Sandcastle> Sandcastles = new();

        [SerializeField] private GameObject VFX;

        protected override void OnValidate()
        {
            base.OnValidate();
            if (gameObject.scene.name != null)
            {
                name = $"Checkpoint [{AreaName}]";
            }
        }

        public void ActivateVFX()
        {
            if (!VFX) return;
            VFX.SetActive(true);
        }
    }
}