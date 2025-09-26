using System;
using UI;
using Unity.Netcode;
using Unity.Services.Multiplay;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Manager
{
    public class GameManager : MonoBehaviour
    {
       public enum SceneNames
       {
           Lobby = 0,
           Game = 1,
           Client = 2,
           Server = 3,
           Startup = 4,
       }
       
       public static GameManager Instance { get; private set; }
       private const int TargetPlayersToEscape = 2;

       
#if NOT_SERVER
        private int TotalPlayersEliminated { get; set; } = 0; 
#else
        private NetworkVariable<int> TotalPlayersEliminated { get; set; } = new NetworkVariable<int>(0);
#endif

       private void Awake()
       {
           if (Instance != null && Instance != this)
           {
               Destroy(gameObject);
               return;
           }

           Instance = this;
           DontDestroyOnLoad(gameObject);
           Reset();
       }

       public void Reset()
       {
#if NOT_SERVER
           TotalPlayersEliminated = 0; 
#else
            TotalPlayersEliminated.Value = new NetworkVariable<int>(0);
#endif
       }

       public void LeaveGame()
       {
           GoToStartup();
       }

       private async void GoToStartup()
       {
#if NOT_SERVER
           Reset(); 
           SceneManager.LoadScene(SceneNames.Startup.ToString()); 
#else
           if (NetworkManager.Singleton.IsClient)
           {
               NetworkManager.Singleton.Shutdown();
               SceneManager.LoadScene(SceneNames.Startup.ToString());
           }

           if (NetworkManager.Singleton.IsServer)
           {
               Application.Quit();
           }
#endif
       }

       private async void OnApplicationQuit()
       {
#if UNITY_SERVER
           if (NetworkManager.Singleton.IsServer)
           {
               NetworkManager.Singleton.Shutdown();
               await MultiplayService.Instance.UnreadyServerAsync();
           }
#endif
       }

       public void NotifyPlayerElimination(bool notUpdate = false)
       {
        #if NOT_SERVER
           if (!notUpdate) 
               TotalPlayersEliminated++;
           PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - TotalPlayersEliminated);
        #else
            if (!notUpdate) 
                TotalPlayersEliminated.Value++;
            PlayerCounterUI.Instance.UpdateCounter(TargetPlayersToEscape - TotalPlayersEliminated.Value);
        #endif
       }
    }
}