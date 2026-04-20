using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CutscenePuppeteer : MonoBehaviour
{
    //todo: make not serialise field, pull at runtime
    [SerializeField] private List<PlayerController> _players = new();

    [SerializeField] [Range(0, 1)] private float _maxSpeed;
    [SerializeField] private float _timeToMaxSpeed;


    public void MakePuppets()
    {
        foreach (PlayerController p in _players)
        {
            p.IsPuppet = true;
        }
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
        }
    }

    public void EnableAnimators()
    {
        foreach (PlayerController p in _players)
        {
            p.GetComponent<Animator>().enabled = true;
        }
    }

    public void DisableAnimators()
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
}
