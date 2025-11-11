using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EnemyStateDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    public GameObject displayPrefab; // Prefab con Canvas y TextMeshPro
    public Vector3 offset = new Vector3(0, 2f, 0); // Offset sobre la cabeza del enemigo
    public float scaleFactor = 0.01f; // Escala para que el Canvas World Space se vea bien
    
    [Header("State Colors")]
    public Color idleColor = Color.gray;
    public Color patrollingColor = Color.blue;
    public Color chasingColor = Color.red;
    public Color damagedColor = Color.yellow;
    public Color deadColor = Color.black;

    private GameObject displayInstance;
    private TextMeshProUGUI stateText;
    private CanvasGroup canvasGroup;
    private AIController aiController;
    private Transform playerTransform;

    void Start()
    {
        aiController = GetComponent<AIController>();
        
        // Buscar al jugador
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        CreateDisplay();
        
        if (aiController != null)
        {
            // Suscribirse a eventos de cambio de estado si es necesario
            aiController.OnHealthChanged += OnHealthChanged;
        }
    }

    void CreateDisplay()
    {
        if (displayPrefab != null)
        {
            displayInstance = Instantiate(displayPrefab, transform);
            displayInstance.transform.localPosition = offset;
            displayInstance.transform.localScale = Vector3.one * scaleFactor;
            
            stateText = displayInstance.GetComponentInChildren<TextMeshProUGUI>();
            canvasGroup = displayInstance.GetComponent<CanvasGroup>();
            
            if (stateText == null)
            {
                Debug.LogError("No se encontró TextMeshProUGUI en el prefab de display");
            }
        }
        else
        {
            // Crear display automáticamente si no hay prefab
            CreateDefaultDisplay();
        }
        
        UpdateDisplay();
    }

    void CreateDefaultDisplay()
    {
        // Crear Canvas
        GameObject canvasObj = new GameObject("StateDisplay");
        displayInstance = canvasObj;
        canvasObj.transform.SetParent(transform);
        canvasObj.transform.localPosition = offset;
        canvasObj.transform.localScale = Vector3.one * scaleFactor;
        
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Crear Texto
        GameObject textObj = new GameObject("StateText");
        textObj.transform.SetParent(canvasObj.transform);
        textObj.transform.localPosition = Vector3.zero;
        textObj.transform.localScale = Vector3.one;
        
        RectTransform rectTransform = textObj.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 50);
        
        stateText = textObj.AddComponent<TextMeshProUGUI>();
        stateText.text = "Initializing...";
        stateText.fontSize = 20;
        stateText.alignment = TextAlignmentOptions.Center;
        stateText.color = Color.white;
        
        // Agregar fondo opcional
        stateText.fontMaterial.EnableKeyword("UNDERLAY_ON");
        stateText.fontSharedMaterial.SetColor("_UnderlayColor", Color.black);
        
        canvasGroup = canvasObj.AddComponent<CanvasGroup>();
    }

    void Update()
    {
        if (displayInstance != null && stateText != null)
        {
            UpdateDisplay();
            FacePlayer();
        }
    }

    void UpdateDisplay()
    {
        if (aiController != null)
        {
            // Obtener el estado actual usando reflexión para acceder al campo protegido
            string currentState = GetCurrentState();
            stateText.text = $"{aiController.GetEnemyName()}\n{currentState}";
            
            // Cambiar color según el estado
            stateText.color = GetStateColor(currentState);
        }
    }

    string GetCurrentState()
    {
        // Usar reflexión para acceder al campo protegido currentState
        var field = typeof(AIController).GetField("currentState", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            return field.GetValue(aiController).ToString();
        }
        
        return "Unknown";
    }

    Color GetStateColor(string state)
    {
        switch (state.ToLower())
        {
            case "idle": return idleColor;
            case "patrolling": return patrollingColor;
            case "chasing": return chasingColor;
            case "damaged": return damagedColor;
            case "dead": return deadColor;
            default: return Color.white;
        }
    }

    void FacePlayer()
    {
        if (playerTransform != null && displayInstance != null)
        {
            // Hacer que el display mire siempre hacia la cámara (jugador)
            displayInstance.transform.LookAt(2 * displayInstance.transform.position - playerTransform.position);
        }
        else if (Camera.main != null)
        {
            // Fallback: usar la cámara principal
            displayInstance.transform.LookAt(2 * displayInstance.transform.position - Camera.main.transform.position);
        }
    }

    void OnHealthChanged(float healthPercent)
    {
        // Opcional: hacer fade out cuando muera
        if (healthPercent <= 0 && canvasGroup != null)
        {
            canvasGroup.alpha = 0.5f;
        }
    }

    void OnDestroy()
    {
        if (displayInstance != null)
        {
            Destroy(displayInstance);
        }
    }
}