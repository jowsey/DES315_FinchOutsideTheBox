using Mirror;
using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomObstacleSpawner : NetworkBehaviour
{
    //todo: make it pull from a list of random objects if this shit graduates from being a prototype

    [Tooltip("Time in seconds between spawns")]
    [SerializeField] private float _minSpawnRate;
    [SerializeField] private float _maxSpawnRate;

    [SerializeField] private List<Transform> _objectSpawnLocations = new();
    private int _inARow; //tracks the number of times a spawn location was picked in a row (max of 3 is allowed)
    private Transform _lastObjectSpawnLocation;

    [SerializeField] private GameObject obstacle; //make array if see above comment

    void Start()
    {
        if (isServer)
        {
            _inARow = 0;
            _lastObjectSpawnLocation = null;

            //todo: make api for starting/stopping if necessary and if, once again, see above comment
            StartCoroutine("SpawnObstacles");
        }
    }

    IEnumerator SpawnObstacles()
    {
        while (true)
        {
            Transform t = _objectSpawnLocations[Random.Range(0, _objectSpawnLocations.Count)];

            if (_lastObjectSpawnLocation == t) { ++_inARow; }
            else { _inARow = 1; }

            if (_inARow > 2)
            {
                //Max of 3 in a row allowed, pick another
                do
                {
                    t = _objectSpawnLocations[Random.Range(0, _objectSpawnLocations.Count)];
                }
                while (t == _lastObjectSpawnLocation);
                _inARow = 1;
            }
            _lastObjectSpawnLocation = t;

            GameObject obj = Instantiate(obstacle, t.position, t.rotation);
            NetworkServer.Spawn(obj);
            obj.GetComponent<Rigidbody>().linearVelocity = t.forward * 10.0f;
            yield return new WaitForSeconds(Random.Range(_minSpawnRate, _maxSpawnRate)); //add initial offset to start-time if.... ykno what just pin that top comment jarvis
        }
    }

}
