using UnityEngine;
using TMPro;

public class EnemyStateDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    public Vector3 worldOffset = new Vector3(0, 2.5f, 0);
    public float textScale = 1f;
    
    [Header("State Colors")]
    public Color idleColor = Color.gray;
    public Color patrollingColor = Color.blue;
    public Color chasingColor = Color.red;
    public Color damagedColor = Color.yellow;
    public Color deadColor = Color.black;

    private TextMeshPro stateText;
    private AIController aiController;
    private Transform playerCamera;

    void Start()
    {
        aiController = GetComponent<AIController>();
        
        // Buscar la cámara del jugador (no el GameObject del jugador)
        playerCamera = Camera.main.transform;
        
        CreateTextDisplay();
//        Debug.Log($"✅ EnemyStateDisplay creado para: {gameObject.name}");
    }

    void CreateTextDisplay()
    {
        // Crear GameObject para el texto
        GameObject textObj = new GameObject("StateText");
        
        // Hacerlo hijo del enemigo para que siga su movimiento
        textObj.transform.SetParent(transform);
        
        // Configurar TextMeshPro
        stateText = textObj.AddComponent<TextMeshPro>();
        stateText.text = "Initializing...";
        stateText.fontSize = 3;
        stateText.alignment = TextAlignmentOptions.Center;
        stateText.color = Color.white;
        
        // Mejorar legibilidad
        stateText.outlineWidth = 0.2f;
        stateText.outlineColor = Color.black;
        stateText.fontStyle = FontStyles.Bold;
        
        // Aplicar escala
        textObj.transform.localScale = Vector3.one * textScale;
    }

    void Update()
    {
        if (stateText != null && aiController != null && playerCamera != null)
        {
            UpdateTextPosition();
            UpdateTextContent();
            RotateTextToCamera();
        }
    }

    void UpdateTextPosition()
    {
        // Posición fija sobre la cabeza del enemigo en coordenadas locales
        stateText.transform.localPosition = worldOffset;
    }

    void UpdateTextContent()
    {
        // Actualizar texto y color
        string state = aiController.GetCurrentState();
        stateText.text = $"{aiController.GetEnemyName()}\n{state}";
        stateText.color = GetStateColor(state);
    }

    void RotateTextToCamera()
    {
        // Rotar el texto para que siempre mire a la cámara
        stateText.transform.rotation = playerCamera.rotation;
        
        // Opcional: Si quieres que solo rote en el eje Y
        // Vector3 lookPos = playerCamera.position;
        // lookPos.y = stateText.transform.position.y;
        // stateText.transform.LookAt(2 * stateText.transform.position - lookPos);
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

    // Métodos para ajustar fácilmente desde el inspector
    [ContextMenu("Ajustar Altura +0.5")]
    void IncreaseHeight()
    {
        worldOffset.y += 0.5f;
        Debug.Log($"🔼 Altura ajustada a: {worldOffset.y}");
    }

    [ContextMenu("Ajustar Altura -0.5")]
    void DecreaseHeight()
    {
        worldOffset.y -= 0.5f;
        Debug.Log($"🔽 Altura ajustada a: {worldOffset.y}");
    }

    [ContextMenu("Debug: Mostrar Posición Texto")]
    void DebugTextPosition()
    {
        if (stateText != null)
        {
            Debug.Log($"📝 Posición texto - Mundial: {stateText.transform.position}, Local: {stateText.transform.localPosition}");
            Debug.Log($"🎯 Posición enemigo: {transform.position}");
        }
    }

    void OnDestroy()
    {
        if (stateText != null && stateText.gameObject != null)
        {
            Destroy(stateText.gameObject);
        }
    }
}