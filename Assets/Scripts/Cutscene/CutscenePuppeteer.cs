using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutscenePuppeteer : MonoBehaviour
{
    //todo: make not serialise field, pull at runtime
    [SerializeField] private List<PlayerController> _players = new();
    [SerializeField] private Cart _cart;

    [SerializeField] [Range(0, 1)] private float _maxSpeed;
    [SerializeField] private float _timeToMaxSpeed;

    [SerializeField] private Transform _cartNudgeDirection;
    [SerializeField] private float _cartNudgeDuration;
    [SerializeField] private float _cartNudgeScale;

    [SerializeField] private float _rapidJumpSpeedMultiplier;

    [SerializeField] private Transform[] _playerRunTargets;

    private Dictionary<ConfigurableJoint, (ConfigurableJointMotion x, ConfigurableJointMotion y, ConfigurableJointMotion z, ConfigurableJointMotion angX, ConfigurableJointMotion angY, ConfigurableJointMotion angZ)> _savedJointMotions = new();
    private List<(Flask flask, Transform originalParent)> _parentedFlasks = new();


    public void SetPlayer2SkinIndex(int index)
    {
        _players[1].PlayerSkinIndex = index;
        foreach (Renderer renderer in _players[1].SkinnedRenderers)
        {
            if (renderer.transform.name == "eyes_MESH") { continue; }
            renderer.sharedMaterial = PlayerController.LoadedSkins[index].Material;
        }
    }

    public void SetPlayer1Name(string name)
    {
        _players[0].PlayerName = name;
        _players[0].PlayerNameText.text = name;
    }

    public void SetPlayer2Name(string name)
    {
        _players[1].PlayerName = name;
        _players[1].PlayerNameText.text = name;
    }


    public void MakePuppets()
    {
        foreach (PlayerController p in _players)
        {
            p.IsPuppet = true;
            if (p.TryGetComponent<AkAudioListener>(out AkAudioListener l))
            {
                l.enabled = false;
            }
        }
        _cart.IsPuppet = true;
        PlayerController.AddControlBlocker(this);

        foreach (Rigidbody rb in _cart.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.gameObject.CompareTag("Flask")) { continue; }

            rb.interpolation = RigidbodyInterpolation.None;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.position = rb.transform.position;
            rb.rotation = rb.transform.rotation;
            rb.isKinematic = true;
        }

        _savedJointMotions.Clear();
        foreach (ConfigurableJoint joint in _cart.GetComponentsInChildren<ConfigurableJoint>())
        {
            _savedJointMotions[joint] = (
                joint.xMotion, joint.yMotion, joint.zMotion,
                joint.angularXMotion, joint.angularYMotion, joint.angularZMotion
            );
            joint.xMotion = ConfigurableJointMotion.Free;
            joint.yMotion = ConfigurableJointMotion.Free;
            joint.zMotion = ConfigurableJointMotion.Free;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
        }

        Physics.SyncTransforms();
    }

    public void MakeNonPuppets()
    {
        foreach (PlayerController p in _players)
        {
            p.Rb.position = p.transform.position;
            p.Rb.rotation = p.transform.rotation;
            p.Rb.linearVelocity = Vector3.zero;
            p.Rb.angularVelocity = Vector3.zero;

            p.GetComponent<Animator>().enabled = true;
            p.IsPuppet = false;

            if (p.TryGetComponent<AkAudioListener>(out AkAudioListener l))
            {
                l.enabled = true;
            }
        }
        PlayerController.RemoveControlBlocker(this);

        _cart.Rb.position = _cart.transform.position;
        _cart.Rb.rotation = _cart.transform.rotation;
        _cart.Rb.linearVelocity = Vector3.zero;
        _cart.Rb.angularVelocity = Vector3.zero;

        _cart.GetComponent<Animator>().enabled = true;
        _cart.IsPuppet = false;
        Physics.SyncTransforms();
    }

    public void EnablePlayerAnimators()
    {
        foreach (PlayerController p in _players)
        {
            p.GetComponent<Animator>().enabled = true;
        }
    }

    public void DisablePlayerAnimators()
    {
        foreach (PlayerController p in _players)
        {
            p.Rb.position = p.transform.position;
            p.Rb.rotation = p.transform.rotation;
            p.Rb.linearVelocity = Vector3.zero;
            p.Rb.angularVelocity = Vector3.zero;

            p.GetComponent<Animator>().enabled = false;
        }
    }

    public void EnableCartAnimator()
    {
        _cart.GetComponent<Animator>().enabled = true;
    }

    public void DisableCartAnimator()
    {
        Vector3 pos = _cart.transform.position;
        Quaternion rot = _cart.transform.rotation;

        _cart.GetComponent<Animator>().enabled = false;

        foreach (Rigidbody rb in _cart.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.gameObject.CompareTag("Flask")) { continue; }
            rb.position = rb.transform.position;
            rb.rotation = rb.transform.rotation;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        foreach (var kvp in _savedJointMotions)
        {
            if (kvp.Key != null)
            {
                kvp.Key.xMotion = kvp.Value.x;
                kvp.Key.yMotion = kvp.Value.y;
                kvp.Key.zMotion = kvp.Value.z;
                kvp.Key.angularXMotion = kvp.Value.angX;
                kvp.Key.angularYMotion = kvp.Value.angY;
                kvp.Key.angularZMotion = kvp.Value.angZ;
            }
        }

        _cart.transform.position = pos;
        _cart.transform.rotation = rot;
        _cart.Rb.position = pos;
        _cart.Rb.rotation = rot;
        _cart.Rb.linearVelocity = Vector3.zero;
        _cart.Rb.angularVelocity = Vector3.zero;
        _cart.Rb.WakeUp();

        Physics.SyncTransforms();
    }

    public void RunInCircles(float seconds)
    {
        StartCoroutine(RunInCirclesCoroutine(seconds));
    }

    private IEnumerator RunInCirclesCoroutine(float seconds)
    {
        PlayerController p1 = _players[0];
        PlayerController p2 = _players[1];

        //Calculate xz-midpoint
        Vector3 p1Pos = p1.transform.position;
        Vector3 p2Pos = p2.transform.position;
        Vector3 center = new Vector3((p1Pos.x + p2Pos.x) / 2f, 0f, (p1Pos.z + p2Pos.z) / 2f);

        p1.Rb.position = p1.transform.position;
        p2.Rb.position = p2.transform.position;

        float timer = 0.0f;
        while (timer < seconds)
        {
            timer += Time.deltaTime;

            //Update directions
            p1.PuppetWorldSpaceMoveDir = GetOrbitDirection(p1.transform.position, center);
            p2.PuppetWorldSpaceMoveDir = GetOrbitDirection(p2.transform.position, center);

            //Speed up over time
            p1.AnalogueMoveScale = Mathf.Clamp01(Mathf.InverseLerp(0.0f, _timeToMaxSpeed, timer)) * _maxSpeed;
            p2.AnalogueMoveScale = Mathf.Clamp01(Mathf.InverseLerp(0.0f, _timeToMaxSpeed, timer)) * _maxSpeed;

            if (timer >= 2.0f)
            {
                p1.PuppetRequestJump = true;
            }
            if (timer >= 2.5f)
            {
                p2.PuppetRequestJump = true;
            }

            yield return null; //Wait for next frame
        }

        //Time is up, stop moving
        p1.PuppetWorldSpaceMoveDir = Vector3.zero;
        p2.PuppetWorldSpaceMoveDir = Vector3.zero;
    }

    private Vector3 GetOrbitDirection(Vector3 playerPos, Vector3 center)
    {
        //Get XZ offset from centre (flatten y)
        Vector3 offset = playerPos - center;
        offset.y = 0f;

        //Calculate counter-clockwise tangent
        //Vector3(-z, 0, x) rotates a vector by 90 degrees
        Vector3 tangent = new Vector3(-offset.z, 0f, offset.x).normalized;

        //Since we're just setting the direction, moving just according to the tangent will cause the players to slowly drift outwards
        //As a hacky fix we can just add a little 15% lerp towards the centre of the circle
        //^^i actually decided i like them going out over time :bleh:
        Vector3 inward = -offset.normalized;
        Vector3 finalDir = (tangent + inward * 0.0f).normalized;

        return finalDir;
    }


    public void NudgeCart()
    {
        StartCoroutine(NudgeCartCoroutine());
    }

    private IEnumerator NudgeCartCoroutine()
    {
        Vector3 dir = Vector3.ProjectOnPlane(_cartNudgeDirection.forward, Vector3.up).normalized;
        WheelSeat[] wheels = _cart.GetComponentsInChildren<WheelSeat>();

        float timer = 0f;
        while (timer < _cartNudgeDuration)
        {
            foreach (WheelSeat wheel in wheels)
            {
                wheel.ApplyDrive(dir, _cartNudgeScale * (timer / _cartNudgeDuration));
            }

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
    }


    public void RapidJump(float seconds)
    {
        StartCoroutine(RapidJumpCoroutine(seconds));
    }

    private IEnumerator RapidJumpCoroutine(float seconds)
    {
        PlayerController p1 = _players[0];
        PlayerController p2 = _players[1];

        float gravityMult = _rapidJumpSpeedMultiplier;
        float jumpMult = Mathf.Sqrt(Mathf.Sqrt(gravityMult));

        p1.PuppetGravityMultiplier = gravityMult;
        p2.PuppetGravityMultiplier = gravityMult;
        p1.PuppetJumpForceMultiplier = jumpMult;
        p2.PuppetJumpForceMultiplier = jumpMult;

        float timer = 0f;
        while (timer < seconds)
        {
            p1.PuppetWorldSpaceMoveDir = Vector3.zero;
            p2.PuppetWorldSpaceMoveDir = Vector3.zero;

            p1.PuppetRequestJump = true;
            if (timer > 0.2f)
            {
                p2.PuppetRequestJump = true;
            }

            timer += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        p1.PuppetRequestJump = false;
        p2.PuppetRequestJump = false;
        p1.PuppetGravityMultiplier = 1f;
        p2.PuppetGravityMultiplier = 1f;
        p1.PuppetJumpForceMultiplier = 1f;
        p2.PuppetJumpForceMultiplier = 1f;
    }


    public void PlayersRunTowardsTargets()
    {
        StartCoroutine(PlayersRunTowardsTargetsCoroutine());
    }

    private IEnumerator PlayersRunTowardsTargetsCoroutine()
    {
        foreach (Transform target in _playerRunTargets)
        {
            //Move on to next target when either of the players reach the target
            bool targetReached = false;
            while (!targetReached)
            {
                foreach (PlayerController p in _players)
                {
                    Vector2 dir = (new Vector2(target.position.x, target.position.z) - new Vector2(p.transform.position.x, p.transform.position.z));

                    targetReached = (dir.sqrMagnitude < 0.1f);
                    if (targetReached) { break; }

                    //Run towards current target
                    p.PuppetWorldSpaceMoveDir = new Vector3(dir.normalized.x, 0.0f, dir.normalized.y);
                    p.AnalogueMoveScale = 1.0f;
                }
                yield return null;
            }
        }
    }
}
