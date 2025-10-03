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
        [field: SerializeField] 
        private int MinPlayersToStart { get; set; } = 2;
        [field: SerializeField] 
        private int MaxPlayersToStart { get; set; } = 8;

        private int connectedCount;
        private List<string> connectedClients;
        private float autoAllocateTimer = 999999f;
        private bool alreadyAutoAllocated;
        private IServerQueryHandler serverQuery;
        private string backfillTickedId;
        private float acceptBackfillTicketsTimer;
        private float acceptBackfillTicketsTimerMax = 1.1f;
        private PayloadAllocation payloadAllocation;
        
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
            {
                Debug.Log("[Server] Init State");
                await UnityServices.InitializeAsync();

                GameManager.Instance.Reset();

                var callbacks = new MultiplayEventCallbacks();
                callbacks.Allocate += OnAllocate;
                callbacks.Deallocate += OnDeallocate;
                callbacks.Error += OnError;
                callbacks.SubscriptionStateChanged += OnSubscriptionStateChanged;
                IServerEvents serverEvents = await MultiplayService.Instance.SubscribeToServerEventsAsync(callbacks);

                serverQuery =
                    await MultiplayService.Instance.StartServerQueryHandlerAsync(
                        (ushort)MaxPlayersToStart, "MyServerName", "ArenaPunch", "1.0", "Default");

                var serverConfig = MultiplayService.Instance.ServerConfig;
                if (serverConfig.AllocationId != "")
                {
                    OnAllocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
                }
            }
            else
            {
                Debug.Log("[Server] Already Init State");
                var serverConfig = MultiplayService.Instance.ServerConfig;
                if (serverConfig.AllocationId != "")
                {
                    OnAllocate(new MultiplayAllocation("", serverConfig.ServerId, serverConfig.AllocationId));
                }
            }
#else
            Debug.Log("[Local Server] Starting Local Server...");
            GameManager.Instance.Reset();

            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("127.0.0.1",7777);

            if (!NetworkManager.Singleton.StartServer())
            {
                Debug.LogError("[Local Server] server not started");
            }
            else
            {
                NetworkManager.Singleton.OnClientConnectedCallback += (ulong id) =>
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
                };
                
                NetworkManager.Singleton.OnClientDisconnectCallback += (ulong id) =>
                {
                    connectedCount = Mathf.Max(0, connectedCount - 1);
                };
            }
#endif
        }

        private void Update()
        {
            if (serverQuery != null)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    serverQuery.CurrentPlayers = (ushort)NetworkManager.Singleton.ConnectedClientsIds.Count;
                    serverQuery.MaxPlayers = (ushort)MaxPlayersToStart;
                }
                serverQuery.UpdateServerCheck();
            }

            if (backfillTickedId != null)
            {
                acceptBackfillTicketsTimer -= Time.deltaTime;
                if (acceptBackfillTicketsTimer <= 0.0f)
                {
                    acceptBackfillTicketsTimer = acceptBackfillTicketsTimerMax;
                    HandleBackfillTickets();
                }
            }
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
        }

        private async void OnAllocate(MultiplayAllocation obj)
        {
            Debug.Log($"Allocation received: {obj}");

            if (alreadyAutoAllocated)
            {
                Debug.Log("Already auto allocated");
                return;
            }
            
            SetupBackfillTickets();
            alreadyAutoAllocated = true;
            var serverConfig = MultiplayService.Instance.ServerConfig;
            Debug.Log($"[Server] Server ID: {serverConfig.ServerId}");
            Debug.Log($"[Server] Allocation ID: {serverConfig.AllocationId}");
            Debug.Log($"[Server] Port: {serverConfig.Port}");
            Debug.Log($"[Server] Query Port: {serverConfig.QueryPort}");
            Debug.Log($"[Server] Log Directory: {serverConfig.ServerLogDirectory}");

            string ipv4Address = "0.0.0.0";
            ushort port = serverConfig.Port;
            NetworkManager.Singleton.GetComponent<UnityTransport>().SetConnectionData(ipv4Address,port,"0.0.0.0");
            NetworkManager.Singleton.StartServer();
            
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            await MultiplayService.Instance.ReadyServerForPlayersAsync();
        }

        private async void SetupBackfillTickets()
        {
            Debug.Log("[Server] Setup Backfill Tickets");
            payloadAllocation = await MultiplayService.Instance.GetPayloadAllocationFromJsonAs<PayloadAllocation>();

            backfillTickedId = payloadAllocation.BackfillTicketId;
            Debug.Log($"[Server] backfillTicketId: {backfillTickedId}");

            acceptBackfillTicketsTimer = acceptBackfillTicketsTimerMax;
        }

        private async void HandleBackfillTickets()
        {
            BackfillTicket backfillTicket =
                await MatchmakerService.Instance.ApproveBackfillTicketAsync(backfillTickedId);
            backfillTickedId = backfillTicket.Id;
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

        private async void OnClientConnected(ulong clientId)
        {
            connectedCount++;
            
            if (GameManager.Instance.AuthIdByClientId.TryGetValue(clientId, out var authId))
            {
                connectedClients.Add(authId.ToString());
            }
            
            HandleUpdateBackfillTickets();

            //Late Join: garantir que quem entrar depois receba o estado atual
            SyncGameStateToLateJoiner(clientId);

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

            if (GameManager.Instance.AuthIdByClientId.TryGetValue(clientId, out var authId))
            {
                connectedClients.Remove(authId.ToString());
            }

            HandleUpdateBackfillTickets(); // garantir update no backfill

            if (connectedCount <= 0)
            {
                Debug.Log("[Server] Nenhum jogador conectado. Encerrando partida...");
                EndMatch();
            }
        }
        
        private async void EndMatch()
        {
            Debug.Log("[Server] Ending match...");
            
            if (!string.IsNullOrEmpty(backfillTickedId))
            {
                try
                {
                    await MatchmakerService.Instance.DeleteBackfillTicketAsync(backfillTickedId);
                    Debug.Log("[Server] Backfill ticket deleted.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Server] Failed to delete backfill ticket: {e}");
                }
            }
            
            try
            {
                await MultiplayService.Instance.UnreadyServerAsync();
                Debug.Log("[Server] Server unready.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Server] Failed to unready: {e}");
            }
            
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        private async void HandleUpdateBackfillTickets()
        {
            if (backfillTickedId != null && payloadAllocation != null)
            {
                List<Player> playerList = new List<Player>();
                foreach (var authId in connectedClients)
                    playerList.Add(new Player(authId));

                MatchProperties matchProperties = new MatchProperties(payloadAllocation.MatchProperties.Teams,
                    playerList, payloadAllocation.MatchProperties.Region,
                    payloadAllocation.MatchProperties.BackfillTicketId);

                try
                {
                    await MatchmakerService.Instance.UpdateBackfillTicketAsync(payloadAllocation.BackfillTicketId,
                        new BackfillTicket(backfillTickedId,
                            properties: new BackfillTicketProperties(matchProperties)));
                }
                catch (MatchmakerServiceException e)
                {
                    Debug.Log($"[Server] Error: {e}");
                }
            }
        }
        
        private void OnApplicationQuit()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        [Serializable]
        public class PayloadAllocation
        {
            public MatchProperties MatchProperties;
            public string GeneratorName;
            public string QueueName;
            public string PoolName;
            public string EnvironmentId;
            public string BackfillTicketId;
            public string PoolId;
        }

        [ServerRpc]
        public void RegisterPlayerIdServerRpc(string clientId)
        {
            connectedClients.Add(clientId);
            HandleUpdateBackfillTickets();
        }

        [ServerRpc]
        public void UnregisterPlayerIdServerRpc(string clientId)
        {
            connectedClients.Remove(clientId);
            HandleUpdateBackfillTickets();
        }

        // Hook para você implementar sincronização de estado do jogo para late joiners
        private void SyncGameStateToLateJoiner(ulong clientId)
        {
            Debug.Log($"[SERVER] Late join detectado para player {clientId}. Enviando estado atual do jogo...");
        }
    }
}