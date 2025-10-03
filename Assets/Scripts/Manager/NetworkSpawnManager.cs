using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Controllers;
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
        
        [field: Header("Game Local")]
        [field: SerializeField]
        private GameObject PlayerLocalPrefab { get; set; }
        
        [field: SerializeField]
        private GameObject NoPlayerLocalPrefab { get; set; }

        [field: SerializeField] 
        private int TotalLocalPlayers { get; set; } = 4;

        private void Awake()
        {
#if NOT_SERVER
            for (int i = 0; i < TotalLocalPlayers; i++)
            {
                Transform spawnPoint = SpawnManager.Instance.GetNextSpawnPoint();
                var newPlayer = Instantiate(i == 0 ? PlayerLocalPrefab : NoPlayerLocalPrefab, spawnPoint.position, Quaternion.identity);
                newPlayer.GetComponent<PlayerController>().MyPlayerId = $"{i}";
                GameManager.Instance.Register(newPlayer.GetComponent<PlayerController>(), newPlayer.GetComponent<PlayerController>().MyPlayerId);
            }

            _ = DelayedNotify(); // async separado para evitar travar Awake
#else
            NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnSceneLoaded;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected; // late join aqui
#endif
        }

        private async Task DelayedNotify()
        {
            await Task.Delay(TimeSpan.FromSeconds(1.0f));
            GameManager.Instance.NotifyPlayerElimination(true);
        }

        private void OnSceneLoaded(string sceneName, LoadSceneMode loadMode, List<ulong> clientsCompleted,
            List<ulong> clientsTimedOut)
        {
            Debug.LogWarning("Spawning Players (scene load)");
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (sceneName == GameManager.SceneNames.Game.ToString())
            {
                NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnSceneLoaded;
                
                foreach (ulong clientId in clientsCompleted)
                {
                    SpawnPlayerForClient(clientId);
                }
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            // Late join: só spawna se já estamos no jogo
            if (!NetworkManager.Singleton.IsServer)
                return;

            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GameManager.SceneNames.Game.ToString())
            {
                Debug.LogWarning($"Late join detected: spawning player for client {clientId}");
                SpawnPlayerForClient(clientId);
            }
        }

        private void SpawnPlayerForClient(ulong clientId)
        {
            Transform spawnPoint = SpawnManager.Instance.GetNextSpawnPoint();
            if (spawnPoint != null)
            {
                GameObject currentPlayer = InstantiatePlayer(spawnPoint);
                currentPlayer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
            }
        }

        private GameObject InstantiatePlayer(Transform spawnPoint)
        {
            return Instantiate(PlayerPrefab, spawnPoint.position, Quaternion.identity);
        }
    }
}