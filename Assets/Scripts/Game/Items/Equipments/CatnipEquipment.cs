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
            if (State != ItemState.Held) return;
            var player = sender!.identity.GetComponent<PlayerController>();
            if (player != _holder) return;

            OnServerUse();
        }

        public override void TryUse()
        {
            base.TryUse();

            CmdConsume();
        }

        protected override void OnClientSuccessfulUse()
        {
            base.OnClientSuccessfulUse();

            PlayerController.LocalPlayer.AddStatusEffect(new PlayerController.PlayerStatusEffect("Catnip!", _effectDuration, PlayerController.PlayerStatusEffect.StatusEffectTarget.MoveSpeed, _effect, _effectType));
        }
    }
}