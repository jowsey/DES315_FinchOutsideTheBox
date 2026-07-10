using Mirror;
using UnityEngine;

public class Door : NetworkBehaviour
{
    [SerializeField] private NetworkAnimator _animator;

    [SerializeField] private AK.Wwise.Event _doorSound;

    public void Open()
    {
        if (authority)
        {
            _animator.SetTrigger("Open");
        }
    }

    public void PostDoorSound()
    {
        _doorSound.Post(gameObject);
    }
}