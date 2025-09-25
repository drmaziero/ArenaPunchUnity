using System;
using Unity.Netcode;
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
       }
       
       public static GameManager Instance { get; private set; }

       private void Awake()
       {
           if (Instance != null && Instance != this)
           {
               Destroy(gameObject);
               return;
           }

           Instance = this;
           DontDestroyOnLoad(gameObject);
       }

       public void LeaveGame()
       {
#if !NOT_SERVER
           if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient)
           {
               NetworkManager.Singleton.Shutdown();
           }
#endif
           GoToLobbyScene();
       }

       private void GoToLobbyScene()
       {
           SceneManager.LoadScene(SceneNames.Lobby.ToString());
       }

       private void OnApplicationQuit()
       {
           LeaveGame();
       }
    }
}