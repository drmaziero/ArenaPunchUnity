using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class NetworkSpawnManager : MonoBehaviour
    {
        [field: SerializeField]
        private GameObject PlayerPrefab { get; set; }

        private void Awake()
        {
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            Debug.LogWarning("Spawning Players");
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (sceneName == GameManager.SceneNames.Game.ToString())
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
                foreach (ulong clientId in clientsCompleted)
                {
                    Transform spawnPoint = SpawnManager.Instance.GetNextSpawnPoint();
                    if (spawnPoint != null)
                    {
                        GameObject currentPlayer = Instantiate(PlayerPrefab, spawnPoint.position, Quaternion.identity);
                        currentPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                    }
                }
            }

        }
    }
}