using System;
using System.Linq;
using Manager;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Matchmaking
{
    public class Startup : MonoBehaviour
    {
        private void Start()
        {
#if UNITY_SERVER
            Debug.Log("Starting Server...");
            SceneManager.LoadScene(GameManager.SceneNames.Server.ToString());
#else
            Debug.Log("Starting Client...");
            SceneManager.LoadScene(GameManager.SceneNames.Client.ToString());
#endif
        }
    }
}