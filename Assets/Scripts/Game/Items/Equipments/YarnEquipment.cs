using System.Collections;
using Mirror;
using PrimeTween;
using UnityEngine;

namespace Game.Items.Equipments
{
    public class YarnEquipment : PlaceableEquipment
    {
        [SerializeField] private ConfigurableJoint _yarnSegmentPrefab;
        [SerializeField] private ConfigurableJoint _yarnBallPrefab;
        [SerializeField, Min(1)] private int _numSegments = 25;

        [SerializeField] public AK.Wwise.RTPC YarnVol;
        [SerializeField] public AK.Wwise.Event YarnStretch;
        [SerializeField] public AK.Wwise.Event YarnOut;

        protected override void OnServerPlace(GameObject instance)
        {
            base.OnServerPlace(instance);
            StartCoroutine(BuildSegments());
            return;

            IEnumerator BuildSegments()
            {
                var segmentLength = _yarnSegmentPrefab.connectedAnchor.z;

                var anchor = instance;
                var anchorCollider = anchor.GetComponentInChildren<Collider>();
                var previousBody = anchor.GetComponent<Rigidbody>();

                const float startingDelay = 0.1f;

                //Yarn Post place init
                YarnStretch.Post(gameObject);

                //Set Yarn Stretch Sfx RTPC to full
                YarnVol.SetGlobalValue(1);

                for (var i = 0; i < _numSegments; i++)
                {
                    yield return new WaitForSecondsRealtime(Mathf.Lerp(startingDelay, 0, (float)i / _numSegments));

                    var offset = i == 0 ? Vector3.zero : previousBody.transform.forward * segmentLength;
                    var segment = Instantiate(
                        _yarnSegmentPrefab,
                        previousBody.position + offset,
                        Quaternion.Euler(-0.5f, previousBody.transform.localEulerAngles.y + Random.Range(30f, 75f), 0f)
                    );
                    NetworkServer.Spawn(segment.gameObject);

                    if (anchorCollider)
                    {
                        Physics.IgnoreCollision(segment.GetComponentInChildren<Collider>(), anchorCollider);
                    }

                    if (i == 0)
                    {
                        // connection to ground anchor is unique
                        segment.connectedAnchor = new Vector3(0f, 0.1f, 0f);
                    }

                    segment.connectedBody = previousBody;
                    previousBody = segment.GetComponent<Rigidbody>();
                }
                
                var yarnBall = Instantiate(
                    _yarnBallPrefab,
                    previousBody.position + previousBody.transform.forward * segmentLength,
                    Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f))
                    
                );
                NetworkServer.Spawn(yarnBall.gameObject);
                yarnBall.connectedBody = previousBody;
                previousBody = yarnBall.GetComponent<Rigidbody>();

                //Stop Yarn Stretch Sfx
                YarnVol.SetGlobalValue(0);

                //Whoosh sfx or sum idk
                YarnStretch.Stop(gameObject);

                Tween.Delay(1f).OnComplete(() => previousBody.AddForce(anchor.transform.forward * 250f, ForceMode.Impulse));

                //Whoosh sfx or sum idk
                YarnOut.Post(gameObject);
            }
        }
    }
}