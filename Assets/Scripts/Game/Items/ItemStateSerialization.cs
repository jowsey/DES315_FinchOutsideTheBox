using System;
using JetBrains.Annotations;
using Mirror;

namespace Game.Items
{
    [UsedImplicitly]
    public static class ItemStateSerialization
    {
        private enum ItemStateTag : byte
        {
            Idle = 0,
            Held = 1,
            PuttingDown = 2,
            Smashed = 3,
            Inactive = 4,
            Frozen = 5,
            SackCarried = 6,
        }

        public static void WriteItemStateData(this NetworkWriter writer, Item.ItemStateData state)
        {
            var tag = state switch
            {
                Item.IdleStateData => ItemStateTag.Idle,
                Item.HeldStateData => ItemStateTag.Held,
                Item.PuttingDownStateData => ItemStateTag.PuttingDown,
                Item.SmashedStateData => ItemStateTag.Smashed,
                Item.InactiveStateData => ItemStateTag.Inactive,
                Item.FrozenStateData => ItemStateTag.Frozen,
                Item.SackCarriedStateData => ItemStateTag.SackCarried,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, $"Unknown item state type {state.GetType().Name}")
            };

            writer.WriteByte((byte)tag);
            switch (state)
            {
                case Item.HeldStateData held:
                    writer.WriteNetworkBehaviour(held.Holder);
                    break;
                case Item.PuttingDownStateData puttingDown:
                    writer.WriteNetworkBehaviour(puttingDown.Target);
                    break;
                case Item.SackCarriedStateData sackCarried:
                    writer.WriteNetworkBehaviour(sackCarried.Sack);
                    break;
            }
        }

        public static Item.ItemStateData ReadItemStateData(this NetworkReader reader)
        {
            var tag = (ItemStateTag)reader.ReadByte();
            return tag switch
            {
                ItemStateTag.Idle => new Item.IdleStateData(),
                ItemStateTag.Held => new Item.HeldStateData { Holder = reader.ReadNetworkBehaviour<PlayerController>() },
                ItemStateTag.PuttingDown => new Item.PuttingDownStateData { Target = reader.ReadNetworkBehaviour<HeldObjectPutdownTarget>() },
                ItemStateTag.Smashed => new Item.SmashedStateData(),
                ItemStateTag.Inactive => new Item.InactiveStateData(),
                ItemStateTag.Frozen => new Item.FrozenStateData(),
                ItemStateTag.SackCarried => new Item.SackCarriedStateData { Sack = reader.ReadNetworkBehaviour<UpgradeSack>() },
                _ => throw new ArgumentOutOfRangeException(nameof(tag), tag, "Unknown item state tag")
            };
        }
    }
}