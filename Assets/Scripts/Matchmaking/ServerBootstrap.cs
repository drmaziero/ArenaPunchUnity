using System.Linq;
using System.Threading.Tasks;
using Manager;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Matchmaking
{
    public class ServerBootstrap : MonoBehaviour
    {
        [field: SerializeField] 
        private ushort FallbackPort { get; set; } = 7777;
        [field: SerializeField] 
        private int MinPlayersToStart { get; set; } = 2;
        [field: SerializeField] 
        private int MaxPlayersToStart { get; set; } = 4;

        private IServerQueryHandler serverQuery;
        private int connectedCount;

        private async void Awake()
        {
#if UNITY_SERVER
            await StartHeadlessServerAsync();
#else

        if (System.Environment.GetCommandLineArgs().Any(a => a.ToLower() == "-dedicated"))
            await StartHeadlessServerAsync();
#endif
        }

        private async Task StartHeadlessServerAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            ushort portToBind = FallbackPort;
            try
            {
                var serverConfig = MultiplayService.Instance.ServerConfig;
                if (serverConfig.AllocationId != "0")
                    portToBind = (ushort)serverConfig.Port;
            }
            catch
            {
                Debug.LogWarning("[SERVER] Sem Multiplay config; usando fallbackPort para testes locais.");
            }

            var networkManager = NetworkManager.Singleton;
            var unityTransport = networkManager.GetComponent<UnityTransport>();
            unityTransport.SetConnectionData("0.0.0.0", portToBind);

            networkManager.OnServerStarted += () => Debug.Log($"[SERVER] Started @ :{portToBind}");
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;

            if (!networkManager.StartServer())
            {
                Debug.LogError("[SERVER] Falha ao iniciar o servidor.");
                return;
            }

            serverQuery = await MultiplayService.Instance.StartServerQueryHandlerAsync(
                maxPlayers: (ushort)MaxPlayersToStart,
                serverName: "ArenaPunchServer",
                gameType: "DefaultMatch",
                buildId: "150625",
                map: "Game"
            );
            UpdateServerQuery();
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
            UpdateServerQuery();

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
            UpdateServerQuery();
        }

        private void UpdateServerQuery()
        {
            if (serverQuery == null) return;
            serverQuery.CurrentPlayers = (ushort)connectedCount;
            serverQuery.MaxPlayers = (ushort)MaxPlayersToStart;
            serverQuery.BuildId = "150625";
            serverQuery.Map = "Game";
            serverQuery.ServerName = "ArenaPunchServer";
            serverQuery.UpdateServerCheck();
        }
    }
}