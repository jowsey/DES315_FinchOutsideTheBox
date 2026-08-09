using UnityEngine;

namespace Game
{
    public class WheelSeatTrigger : MonoBehaviour
    {
        [field: SerializeField] public WheelSeat Seat { get; private set; }
    }
}