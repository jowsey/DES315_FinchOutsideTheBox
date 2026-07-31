using Sirenix.OdinInspector;

namespace Game.Items.Equipments
{
    public class Sandcastle : RespawnTarget
    {
        [ReadOnly] public Checkpoint Parent;

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