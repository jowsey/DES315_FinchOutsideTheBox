namespace Game.Items.Equipments
{
    public class SandcastleEquipment : PlaceableEquipment
    {
        public override void TryUse()
        {
            if (_previewInstance is SandcastlePreview { ValidPosition: false }) return;
            base.TryUse();
        }
    }
}