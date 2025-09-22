using UnityEngine;

namespace UI
{
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        private RectTransform panel;
        private Rect lastSafeArea = new Rect(0, 0, 0, 0);

        private void Awake()
        {
            panel = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            // Reaplica se houver mudança (ex: rotação da tela)
            if (Screen.safeArea != lastSafeArea)
                ApplySafeArea();
        }

        private void ApplySafeArea()
        {
            Rect safeArea = Screen.safeArea;
            lastSafeArea = safeArea;

            // Normaliza para [0-1]
            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            panel.anchorMin = anchorMin;
            panel.anchorMax = anchorMax;
        }
    }
}