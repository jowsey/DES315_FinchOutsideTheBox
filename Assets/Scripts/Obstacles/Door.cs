using UnityEngine;
using Mirror;

public class Door : NetworkBehaviour
{
    [SerializeField] private NetworkAnimator _animator;

    public void Open()
    {
        if (authority)
        {
            _animator.SetTrigger("Open");
        }
    }
}