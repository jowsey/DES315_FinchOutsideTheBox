using System.Collections.Generic;
using Game.Items;
using Mirror;
using UnityEngine;
using UnityEngine.Events;

namespace Game
{
    public abstract class RespawnTarget : NetworkBehaviour
    {
        public class RespawnSnapshot
        {
            public struct CarriedItemSnapshot
            {
                public Vector3 LocalPosition;
                public Quaternion Rotation;
            }

            public struct WorldItemSnapshot
            {
                public Vector3 Position;
                public Quaternion Rotation;
                public Item.ItemState State;
            }
            
            public Dictionary<Item, CarriedItemSnapshot> CarriedItems = new();
            public Dictionary<Item, WorldItemSnapshot> WorldItems = new();
            public Dictionary<Shop, List<Item>> ShopAvailableItems = new();
            
            public int Balance;
        }
        
        public static readonly UnityEvent<RespawnTarget> OnPreRespawn = new();
        public static readonly UnityEvent<RespawnTarget> OnRespawn = new();
        public static readonly UnityEvent<RespawnTarget> OnPostRespawn = new();
        public static readonly UnityEvent<RespawnTarget> OnReachNewTarget = new();
        public static readonly UnityEvent<RespawnSnapshot> OnBuildRespawnSnapshot = new();
        
        [field: SerializeField] public Transform[] PlayerSpawnPoints { get; private set; }
        [field: SerializeField] public Transform CartSpawnPoint { get; private set; }

        public RespawnSnapshot Snapshot;
        [SyncVar] public int? NumCarriedItemsOnReach;
    }
}