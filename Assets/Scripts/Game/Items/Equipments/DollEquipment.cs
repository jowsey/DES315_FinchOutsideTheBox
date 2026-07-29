using Mirror;

namespace Game.Items.Equipments
{
    public class DollEquipment : Equipment
    {
        [Command(requiresAuthority = false)]
        private void CmdTryUse(NetworkConnectionToClient sender = null)
        {
            if (State != ItemState.Held) return;
            if (sender != _holder.connectionToClient) return;
            
            OnServerUse();
        }
        
        public override void TryUse()
        {
            base.TryUse();
            CmdTryUse();
        }
    }
}