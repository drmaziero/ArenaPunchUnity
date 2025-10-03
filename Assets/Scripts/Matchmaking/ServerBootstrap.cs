using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Multiplay;
using Unity.Services.Matchmaker;
using System.Threading.Tasks;
using Manager;
using Unity.Services.Matchmaker.Models;

namespace Matchmaking
{
    public class ServerBootstrap : MonoBehaviour
    {
        [field: SerializeField] private int MinPlayersToStart { get; set; } = 2;
        [field: SerializeField] private int MaxPlayersToStart { get; set; } = 8;

        private int connectedCount;
        private List<string> connectedClients;
        private IServerQueryHandler serverQuery;
        private string backfillTickedId;
        private PayloadAllocation payloadAllocation;

        private float backfillHeartbeatTimer = 5f; // novo heartbeat para manter ticket vivo

        public static ServerBootstrap Instance;

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        private void Start()
        {
#if UNITY_SERVER
            connectedClients = new List<string>();
            DontDestroyOnLoad(gameObject);
            StartServer();
#endif
        }

        private async void StartServer()
        { 
#if !LOCAL_SERVER
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            GameManager.Instance.Reset();

            var callbacks = new MultiplayEventCallbacks();
            callbacks.Allocate += OnAllocate;
            callbacks.Deallocate += OnDeallocate;
            callbacks.Error += OnError;
            callbacks.SubscriptionStateChanged += OnSubscriptionStateChanged;
            await MultiplayService.Instance.SubscribeToServerEventsAsync(callbacks);

            serverQuery = await MultiplayService.Instance.StartServerQueryHandlerAsync(
                (ushort)MaxPlayersToStart, "MyServerName", "ArenaPunch", "1.0", "Default");

            var serverConfig = MultiplayService.Instance.ServerConfig;
            if (!string.IsNullOrEmpty(serverConfig.AllocationId))
                OnAllocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
#else
            Debug.Log("[Local Server] Starting Local Server...");
            GameManager.Instance.Reset();

            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("127.0.0.1", 7777);

            if (NetworkManager.Singleton.StartServer())
            {
                NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
#endif
        }

        private void Update()
        {
            if (serverQuery != null && NetworkManager.Singleton.IsServer)
            {
                serverQuery.CurrentPlayers = (ushort)NetworkManager.Singleton.ConnectedClientsIds.Count;
                serverQuery.MaxPlayers = (ushort)MaxPlayersToStart;
                serverQuery.UpdateServerCheck();
            }

            // 🔥 Heartbeat do backfill
            if (!string.IsNullOrEmpty(backfillTickedId))
            {
                backfillHeartbeatTimer -= Time.deltaTime;
                if (backfillHeartbeatTimer <= 0f)
                {
                    backfillHeartbeatTimer = 5f;
                    HandleUpdateBackfillTickets();
                }
            }
        }

        private void OnSubscriptionStateChanged(MultiplayServerSubscriptionState obj) 
            => Debug.Log($"Subscription state changed: {obj}");

        private void OnError(MultiplayError obj) 
            => Debug.Log($"On Error: {obj}");

        private async void OnDeallocate(MultiplayDeallocation obj) 
            => Debug.Log($"Deallocation received: {obj}");

        private async void OnAllocate(MultiplayAllocation obj)
        {
            Debug.Log($"[Server] Allocation received: {obj}");

            payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<PayloadAllocation>();
            backfillTickedId = payloadAllocation.BackfillTicketId;
            Debug.Log($"[Server] BackfillTicketId: {backfillTickedId}");

            var serverConfig = MultiplayService.Instance.ServerConfig;
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData("0.0.0.0", serverConfig.Port, "0.0.0.0");
            NetworkManager.Singleton.StartServer();

            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            await MultiplayService.Instance.ReadyServerForPlayersAsync();
        }

        private void OnClientConnected(ulong clientId)
        {
            connectedCount++;
            HandleUpdateBackfillTickets();

            if (NetworkManager.Singleton.IsServer &&
                connectedCount >= MinPlayersToStart &&
                SceneManager.GetActiveScene().name != GameManager.SceneNames.Game.ToString())
            {
                Debug.Log("[SERVER] Jogadores suficientes. Carregando cena Game...");
                NetworkManager.Singleton.SceneManager.LoadScene(GameManager.SceneNames.Game.ToString(), LoadSceneMode.Single);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            connectedCount = Mathf.Max(0, connectedCount - 1);
            HandleUpdateBackfillTickets();
        }

        private async void HandleUpdateBackfillTickets()
        {
            if (!string.IsNullOrEmpty(backfillTickedId) && payloadAllocation != null)
            {
                var playerList = new List<Player>();
                foreach (var authId in connectedClients)
                    playerList.Add(new Player(authId));

                var matchProperties = new MatchProperties(
                    payloadAllocation.MatchProperties.Teams,
                    playerList,
                    payloadAllocation.MatchProperties.Region,
                    backfillTickedId
                );

                try
                {
                    await MatchmakerService.Instance.UpdateBackfillTicketAsync(
                        backfillTickedId,
                        new BackfillTicket(backfillTickedId, null, null,
                            new BackfillTicketProperties(matchProperties))
                    );
                    Debug.Log("[Server] Backfill atualizado.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Server] Erro ao atualizar backfill: {e}");
                }
            }
        }

        [ServerRpc]
        public void RegisterPlayerIdServerRpc(string clientId)
        {
            if (!connectedClients.Contains(clientId))
                connectedClients.Add(clientId);

            HandleUpdateBackfillTickets();
        }

        [ServerRpc]
        public void UnregisterPlayerIdServerRpc(string clientId)
        {
            connectedClients.Remove(clientId);
            HandleUpdateBackfillTickets();
        }

        [Serializable]
        public class PayloadAllocation
        {
            public MatchProperties MatchProperties;
            public string BackfillTicketId;
        }
    }
}