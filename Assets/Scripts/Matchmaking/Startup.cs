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
            SceneManager.LoadScene(GameManager.SceneNames.Server.ToString());
#else
            SceneManager.LoadScene(GameManager.SceneNames.Client.ToString());
#endif
        }
    }
}