using System;
using TMPro;
using UnityEngine;

namespace UI
{
    public class PlayerCounterUI : MonoBehaviour
    {
        [field: SerializeField]
        private TextMeshProUGUI CounterLabel { get; set; }
        public static PlayerCounterUI Instance;
        private int TotalCounter { get; set; }
        
        public void Awake()
        {
            TotalCounter = 0;
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
            Show();
        }

        private void Show() => gameObject.SetActive(true);

        private void Hide() => gameObject.SetActive(false);

        public void UpdateCounter(int counter)
        {
            if (gameObject.activeSelf == false)
                return;
            
            CounterLabel.text = $"{counter} To Escape";
            if (TotalCounter > 0)
                Show();
            else
                Hide();
            
        }
    }
}