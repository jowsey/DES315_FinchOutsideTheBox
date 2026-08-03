using Mirror;
using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Game.Items.Equipments
{
    public class Sandcastle : RespawnTarget
    {
        [ReadOnly] [SyncVar] public Checkpoint Parent;

        public override void OnStartServer()
        {
            base.OnStartServer();

            var currentCheckpoint = Cart.Instance.CurrentRespawnTarget switch
            {
                Checkpoint cp => cp,
                Sandcastle sc => sc.Parent,
                _ => null
            };

            Parent = currentCheckpoint;
            currentCheckpoint?.Sandcastles.Add(this);
            Cart.Instance.SetActiveRespawnTarget(this);
        }
    }
}