using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class PlayerCounterUI : MonoBehaviour
    {
        [field: SerializeField]
        private TextMeshProUGUI CounterLabel { get; set; }
        [field: SerializeField]
        private Button EscapeButton { get; set; }
        
        public static PlayerCounterUI Instance;
        
        public void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
            
            EscapeButton.onClick.AddListener(()=>
            {
                EndGameUI.Instance.Show();
                EscapeButton.gameObject.SetActive(false);
            });

#if UNITY_SERVER
          Hide();  
#endif
        }

        private void Show() => gameObject.SetActive(true);

        private void Hide()
        {
            EscapeButton.gameObject.SetActive(true);
            gameObject.SetActive(false);
        }

        public void UpdateCounter(int counter)
        {
            if (gameObject.activeSelf == false)
                return;
            
            CounterLabel.text = $"{counter} To Escape";
            if (counter > 0)
                Show();
            else
                Hide();
        }

        public void Disable()
        {
            Hide();
            EscapeButton.gameObject.SetActive(false);
        }
    }
}