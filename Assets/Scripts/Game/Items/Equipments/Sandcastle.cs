using Sirenix.OdinInspector;

namespace Game.Items.Equipments
{
    public class Sandcastle : RespawnTarget
    {
        [ReadOnly] public Checkpoint Parent;

        public override void OnStartServer()
        {
            base.OnStartServer();

            var cart = FindAnyObjectByType<Cart>();

            var currentCheckpoint = cart.CurrentRespawnTarget switch
            {
                Checkpoint cp => cp,
                Sandcastle sc => sc.Parent,
                _ => null
            };

            Parent = currentCheckpoint;
            currentCheckpoint?.Sandcastles.Add(this);
            cart.SetActiveRespawnTarget(this);
        }
    }
}