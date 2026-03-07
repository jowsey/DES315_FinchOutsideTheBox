using System.Linq;
using Mirror;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

[RequireComponent(typeof(Animator))]
[InfoBox("Schedules an idle breaker to play on the attached [Network]Animator roughly every N loops.")]
public class IdleBreakerScheduler : MonoBehaviour
{
    private static readonly int IdleBreakerTrigger = Animator.StringToHash("Idle_Break");
    
    private Animator _animator;
    private NetworkAnimator _networkAnimator;
    
    [Tooltip("The average number of idle animation loops to play before an idle breaker animation")]
    [SerializeField] private float _idleBreakerFrequency;
    private int _idleBreakerFrequencyTicks; //Impl for _idlBreakerFrequency - same thing but measured in fixed update ticks rather than idle anim loops
    
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _networkAnimator = GetComponent<NetworkAnimator>();
        
        AnimationClip idleClip = _animator.runtimeAnimatorController.animationClips.First(clip => clip.name == "Idle");
        int numFixedUpdatesPerIdleAnim = (int)(idleClip.length / Time.fixedDeltaTime);
        _idleBreakerFrequencyTicks = (int)(numFixedUpdatesPerIdleAnim * _idleBreakerFrequency);
    }

    private void FixedUpdate()
    {
        //Idle-breaker
        AnimatorClipInfo[] animatorInfo = _animator.GetCurrentAnimatorClipInfo(0);
        if (animatorInfo.Length > 0 && animatorInfo[0].clip.name == "Idle")
        {
            //Check passes roughly once every _idleBreakerFrequencyTicks ticks
            if (Random.Range(0, _idleBreakerFrequencyTicks) > 0) return;
            
            if (_networkAnimator && _networkAnimator.authority)
            {
                _networkAnimator.SetTrigger(IdleBreakerTrigger);
            }
            else
            {
                _animator.SetTrigger(IdleBreakerTrigger);
            }
        }
    }
}