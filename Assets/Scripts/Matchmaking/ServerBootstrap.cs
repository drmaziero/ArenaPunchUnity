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
using UnityEditor.Il2Cpp;
using UnityEngine;
using UnityEngine.SceneManagement;


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
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                Debug.Log("[Server] Init State");
                await UnityServices.InitializeAsync();

                var callbacks = new MultiplayEventCallbacks();
                callbacks.Allocate += OnAllocate;
                callbacks.Deallocate += OnDeallocate;
                callbacks.Error += OnError;
                callbacks.SubscriptionStateChanged += OnSubscriptionStateChanged;
                IServerEvents serverEvents = await MultiplayService.Instance.SubscribeToServerEventsAsync(callbacks);

                serverQuery =
                    await MultiplayService.Instance.StartServerQueryHandlerAsync(4, "MyServerName", "ArenaPunch", "1.0",
                        "Default");

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

            /*

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
            
            

            var events = await MultiplayService.Instance.SubscribeToServerEventsAsync(callbacks);
            await CreateBackfillTicket();
            */
        }
        
        private void Update()
        {
            autoAllocateTimer -= Time.deltaTime;
            if (autoAllocateTimer <= 0.0f)
            {
                autoAllocateTimer = 9999.0f;
                OnAllocate(null);
            }

            if (serverQuery != null)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    serverQuery.CurrentPlayers = (ushort)NetworkManager.Singleton.ConnectedClientsIds.Count;
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
        
        /*
        private async Task CreateBackfillTicket()
        {
            MatchmakingResults results = null;

            try
            {
                results = await MultiplayService.Instance
                    .GetPayloadAllocationFromJsonAs<MatchmakingResults>();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SERVER] Sem payload disponível (provavelmente teste manual ou sem matchmaking): {e.Message}");
                return; // não cria ticket se não tem payload
            }

            if (results == null)
            {
                Debug.LogWarning("[SERVER] Payload retornou nulo, cancelando criação de backfill ticket.");
                return;
            }

            Debug.Log(
                $"Environment: {results.EnvironmentId} MatchId: {results.MatchId} " +
                $"Match Properties: {results.MatchProperties}");

            var backfillTicketProperties = new BackfillTicketProperties(results.MatchProperties);

            string queueName = "ArenaPunchQueue"; // precisa existir igual no Dashboard
            string connectionString =
                $"{MultiplayService.Instance.ServerConfig.IpAddress}:{MultiplayService.Instance.ServerConfig.Port}";

            var options = new CreateBackfillTicketOptions(
                queueName,
                connectionString,
                new Dictionary<string, object>(), // custom properties se precisar
                backfillTicketProperties);

            /*
            Debug.Log("[SERVER] Requesting backfill ticket...");
            try
            {
                ticketId = await MatchmakerService.Instance.CreateBackfillTicketAsync(options);
                Debug.Log($"[SERVER] Backfill ticket criado com sucesso: {ticketId}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SERVER] Falha ao criar backfill ticket: {e.Message}");
            }
            */
        //}

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
            HandleUpdateBackfillTickets();

            if (NetworkManager.Singleton.IsServer &&
                NetworkManager.Singleton.SceneManager != null &&
                connectedCount >= MinPlayersToStart &&
                SceneManager.GetActiveScene().name != GameManager.SceneNames.Game.ToString())
            {
                await MatchmakerService.Instance.DeleteBackfillTicketAsync(backfillTickedId);
                Debug.Log("[SERVER] Jogadores suficientes. Carregando cena Game...");
                NetworkManager.Singleton.SceneManager.LoadScene(GameManager.SceneNames.Game.ToString(),
                    LoadSceneMode.Single);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            connectedCount = Mathf.Max(0, connectedCount - 1);
        }

        /*
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
        */

        private async void HandleUpdateBackfillTickets()
        {
            if (backfillTickedId != null && payloadAllocation != null)
            {
                List<Player> playerList = new List<Player>();
                foreach (var connectedClientId in connectedClients)
                    playerList.Add(new Player(connectedClientId));

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
        }

        [ServerRpc]
        public void UnregisterPlayerIdServerRpc(string clientId)
        {
            connectedClients.Remove(clientId);
        }
    }
}