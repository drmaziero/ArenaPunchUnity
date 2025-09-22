using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Manager
{
    public class SpawnManager : NetworkBehaviour
    {
        [field: SerializeField]
        private List<Transform> SpawnPoints { get; set; }
        
        private int NextIndex { get; set; }
        public static SpawnManager Instance;

        private void Awake()
        {
            NextIndex = 0;
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        public Transform GetNextSpawnPoint()
        {
            if (SpawnPoints.Count == 0)
            {
                Debug.LogError("Nothing Spawn Points on Spawn Manager");
                return null;
            }

            if (NextIndex >= SpawnPoints.Count)
            {
                Debug.LogError("There is not Spawn Point Available on Spawn Manager");
                return null;
            }

            Transform spawnPoint = SpawnPoints[NextIndex];
            NextIndex++;
            return spawnPoint;
        }
    }
}