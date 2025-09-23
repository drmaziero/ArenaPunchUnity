using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Controllers;
using Manager;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Matchmaker;
using Unity.Services.Matchmaker.Models;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Matchmaking
{
    public class ServerBootstrap : MonoBehaviour
    {
        [field: SerializeField] 
        private int MinPlayersToStart { get; set; } = 2;
        [field: SerializeField] 
        private int MaxPlayersToStart { get; set; } = 4;

        private IServerQueryHandler serverQuery;
        private int connectedCount;
        private string ticketId;

        private void Start()
        {
            DontDestroyOnLoad(gameObject);
            StartServer();
            StartCoroutine(ApproveBackfillTicketEverySecond());
        }

        private async void StartServer()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            var server = MultiplayService.Instance.ServerConfig;
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("0.0.0.0", server.Port);
            Debug.Log($"Network Transport {unityTransport.ConnectionData.Address}:{unityTransport.ConnectionData.Port}");

            if (!NetworkManager.Singleton.StartServer())
            {
                Debug.LogError("Failed to start Server");
                throw new Exception("Failed o start server");
            }
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnServerStopped += b => { Debug.Log("Server Stopped"); };
            Debug.Log($"Starter Server {unityTransport.ConnectionData.Address}:{unityTransport.ConnectionData.Port}");

            var callbacks = new MultiplayEventCallbacks();
            callbacks.Allocate += OnAllocate;
            callbacks.Deallocate += OnDeallocate;
            callbacks.Error += OnError;
            callbacks.SubscriptionStateChanged += OnSubscriptionStateChanged;

            var events = await MultiplayService.Instance.SubscribeToServerEventsAsync(callbacks);
            await CreateBackfillTicket();
        }

        private async Task CreateBackfillTicket()
        {
            MatchmakingResults results =
                await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<MatchmakingResults>();

            Debug.Log(
                $"Environment: {results.EnvironmentId} MatchId: {results.MatchId} Match Properties: {results.MatchProperties}");

            var backfillTicketProperties = new BackfillTicketProperties(results.MatchProperties);
            string queueName = "ArenaPunchQueue";
            string connectionString = MultiplayService.Instance.ServerConfig.IpAddress + ":" +
                                      MultiplayService.Instance.ServerConfig.Port;

            var options = new CreateBackfillTicketOptions(queueName, connectionString, new Dictionary<string, object>(),
                backfillTicketProperties);
            
            Debug.Log("Requesting backfill ticket");
            ticketId = await MatchmakerService.Instance.CreateBackfillTicketAsync(options);
        }

        private void OnSubscriptionStateChanged(MultiplayServerSubscriptionState obj)
        {
            Debug.Log($"Subscription state changed: {obj}");
        }

        private void OnError(MultiplayError obj)
        {
            Debug.Log($"On Error: {obj}");
        }

        private async void OnDeallocate(MultiplayDeallocation obj)
        {
           Debug.Log($"Deallocation received: {obj}");
           await MultiplayService.Instance.UnreadyServerAsync();
        }

        private async void OnAllocate(MultiplayAllocation obj)
        {
            Debug.Log($"Allocation received: {obj}");
            await MultiplayService.Instance.ReadyServerForPlayersAsync();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }

            serverQuery?.Dispose();
        }

        private void OnClientConnected(ulong clientId)
        {
            connectedCount++;

            if (NetworkManager.Singleton.IsServer &&
                NetworkManager.Singleton.SceneManager != null &&
                connectedCount >= MinPlayersToStart &&
                SceneManager.GetActiveScene().name != GameManager.SceneNames.Game.ToString())
            {
                Debug.Log("[SERVER] Jogadores suficientes. Carregando cena Game...");
                NetworkManager.Singleton.SceneManager.LoadScene(GameManager.SceneNames.Game.ToString(),
                    LoadSceneMode.Single);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            connectedCount = Mathf.Max(0, connectedCount - 1);
        }

        private IEnumerator ApproveBackfillTicketEverySecond()
        {
            for (int i = 4; i >= 0; i--)
            {
                Debug.Log($"Waiting {i} seconds to start backfill");
                yield return new WaitForSeconds(1);
            }

            while (true)
            {
                yield return new WaitForSeconds(1);
                if (string.IsNullOrEmpty(ticketId))
                {
                    Debug.Log("No backfill ticket to approve");
                    continue;
                }
                
                Debug.Log($"Doing backfill approval for ticket ID: {ticketId}");
                yield return MatchmakerService.Instance.ApproveBackfillTicketAsync(ticketId);
                Debug.Log($"Approved backfill ticket {ticketId}");
            }
        }
    }
}