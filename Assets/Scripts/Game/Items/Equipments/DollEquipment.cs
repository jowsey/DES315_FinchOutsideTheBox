using Mirror;

namespace Game.Items.Equipments
{
    public class DollEquipment : Equipment
    {
        [Command(requiresAuthority = false)]
        private void CmdTryUse(NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;
            if (sender != heldData.Holder.connectionToClient) return;
            
            OnServerUse();
        }
        
        public override void TryUse()
        {
            base.TryUse();
            CmdTryUse();
        }
    }
}