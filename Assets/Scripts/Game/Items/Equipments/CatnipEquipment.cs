using Mirror;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class CatnipEquipment : Equipment
    {
        [SerializeField] private float _effectDuration = 10f;
        [SerializeField] private PlayerController.PlayerStatusEffect.StatusEffectType _effectType;
        [SerializeField] private float _effect = 1.5f;

        [Command(requiresAuthority = false)]
        private void CmdConsume(NetworkConnectionToClient sender = null)
        {
            if (StateData is not HeldStateData heldData) return;
            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != heldData.Holder) return;

            OnServerUse();
        }

        public override void TryUse()
        {
            base.TryUse();

            CmdConsume();
        }

        protected override void OnClientHolderSuccessfulUse()
        {
            base.OnClientHolderSuccessfulUse();

            PlayerController.LocalPlayer.AddStatusEffect(new PlayerController.PlayerStatusEffect("Catnip!", _effectDuration, PlayerController.PlayerStatusEffect.StatusEffectTarget.MoveSpeed, _effect, _effectType));
        }
    }
}