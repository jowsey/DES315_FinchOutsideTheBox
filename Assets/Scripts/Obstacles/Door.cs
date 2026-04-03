using UnityEngine;
using Mirror;

public class Door : NetworkBehaviour
{
    [SerializeField] private NetworkAnimator _animator;

    public AK.Wwise.Event DoorSound;

    public void Open()
    {
        if (authority)
        {
            _animator.SetTrigger("Open");
        }
    }

    public void PostDoorSound()
    {
        DoorSound.Post(gameObject);
    }
}