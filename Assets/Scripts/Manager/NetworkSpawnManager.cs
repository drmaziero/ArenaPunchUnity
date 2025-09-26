using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class NetworkSpawnManager : MonoBehaviour
    {
        [field: SerializeField]
        private GameObject PlayerPrefab { get; set; }
        
        [field:Header("Game Local")]
        [field: SerializeField]
        private GameObject PlayerLocalPrefab { get; set; }
        
        [field: SerializeField]
        private GameObject NoPlayerLocalPrefab { get; set; }

        [field: SerializeField] 
        private int TotalLocalPlayers { get; set; } = 4;

        private async void Awake()
        {
#if UNITY_SERVER
             await MultiplayService.Instance.UnreadyServerAsync();
#endif
#if NOT_SERVER
            for (int i = 0; i < TotalLocalPlayers; i++)
            {
                Transform spawnPoint = SpawnManager.Instance.GetNextSpawnPoint();
                Instantiate(i == 0 ? PlayerLocalPrefab : NoPlayerLocalPrefab, spawnPoint.position, Quaternion.identity);
            }

            await Task.Delay(TimeSpan.FromSeconds(1.0f));
            GameManager.Instance.NotifyPlayerElimination(true);
#else
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
#endif
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
                        GameObject currentPlayer = InstantiatePlayer(spawnPoint);
                        currentPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
                    }
                }
                GameManager.Instance.NotifyPlayerElimination(true);
            }
        }

        private GameObject InstantiatePlayer(Transform spawnPoint)
        {
           return Instantiate(PlayerPrefab, spawnPoint.position, Quaternion.identity);
        }
    }
}