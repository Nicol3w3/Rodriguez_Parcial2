using UnityEngine;
using UnityEngine.InputSystem;

public class EnemyRespawnManager : MonoBehaviour
{
    [Header("Enemy Reference")]
    public AIController enemyInScene;
    
    [Header("Respawn Settings")]
    public float spawnHeightAboveGround = 1.0f;
    public LayerMask groundLayerMask = 1;
    public bool alwaysRespawnAtWaypoint1 = true; // ✅ NUEVA OPCIÓN
    
    [Header("Input Settings")]
    public InputActionReference respawnInputReference;
    
    public static EnemyRespawnManager Instance { get; private set; }
    
    private Transform[] originalWaypoints;
    private float originalEnemySpeed;
    private float originalChaseSpeed;
    private float originalMaxHealth;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private bool hasSavedOriginalConfig = false;

    // ✅ NUEVO: Guardar referencia al waypoint 1
    private Transform waypoint1;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (enemyInScene != null && !hasSavedOriginalConfig)
        {
            SaveEnemyOriginalConfiguration();
        }
    }

    void OnEnable()
    {
        if (respawnInputReference != null)
        {
            respawnInputReference.action.Enable();
            respawnInputReference.action.performed += OnRespawnInput;
        }
    }

    void OnDisable()
    {
        if (respawnInputReference != null)
        {
            respawnInputReference.action.performed -= OnRespawnInput;
            respawnInputReference.action.Disable();
        }
    }

    void OnRespawnInput(InputAction.CallbackContext context)
    {
        RespawnEnemy();
    }

    public void SaveCurrentConfig()
    {
        if (enemyInScene != null)
        {
            SaveEnemyOriginalConfiguration();
        }
    }

    void SaveEnemyOriginalConfiguration()
    {
        if (enemyInScene == null) return;

        originalWaypoints = enemyInScene.waypoints;
        originalEnemySpeed = enemyInScene.EnemySpeed;
        originalChaseSpeed = enemyInScene.ChaseSpeed;
        originalMaxHealth = enemyInScene.maxHealth;
        
        // ✅ MODIFICADO: Guardar waypoint 1 si existe
        if (alwaysRespawnAtWaypoint1 && originalWaypoints != null && originalWaypoints.Length > 0)
        {
            waypoint1 = originalWaypoints[0];
            originalPosition = GetSafeSpawnPosition(waypoint1.position);
        }
        else
        {
            originalPosition = GetSafeSpawnPosition(enemyInScene.transform.position);
        }
        
        originalRotation = enemyInScene.transform.rotation;
        
        hasSavedOriginalConfig = true;
        
//        Debug.Log($"Configuración guardada. Respawn en Waypoint 1: {alwaysRespawnAtWaypoint1}");
    }

    Vector3 GetSafeSpawnPosition(Vector3 desiredPosition)
    {
        RaycastHit hit;
        Vector3 raycastStart = desiredPosition + Vector3.up * 10f;
        float maxDistance = 20f;

        if (Physics.Raycast(raycastStart, Vector3.down, out hit, maxDistance, groundLayerMask))
        {
            return hit.point + Vector3.up * spawnHeightAboveGround;
        }
        else
        {
            return new Vector3(desiredPosition.x, desiredPosition.y + spawnHeightAboveGround, desiredPosition.z);
        }
    }

    public void RespawnEnemy()
    {
        if (enemyInScene == null || IsEnemyDead())
        {
            ReviveExistingEnemy();
        }
        else
        {
            Debug.Log("El enemigo ya está vivo en la escena.");
        }
    }

    bool IsEnemyDead()
    {
        if (enemyInScene == null) return true;
        if (enemyInScene.gameObject == null) return true;
        
        return enemyInScene.IsDead();
    }

    void ReviveExistingEnemy()
    {
        if (enemyInScene == null || enemyInScene.gameObject == null)
        {
            Debug.LogError("No hay enemigo para revivir.");
            return;
        }

        // ✅ MODIFICADO: Usar waypoint 1 si está configurado
        Vector3 spawnPosition;
        if (alwaysRespawnAtWaypoint1 && waypoint1 != null)
        {
            spawnPosition = GetSafeSpawnPosition(waypoint1.position);
//            Debug.Log($"🔁 Respawn en Waypoint 1: {waypoint1.position}");
        }
        else
        {
            spawnPosition = GetSafeSpawnPosition(originalPosition);
            Debug.Log($"🔁 Respawn en posición original: {originalPosition}");
        }

        AIController aiController = enemyInScene.GetComponent<AIController>();
        if (aiController == null)
        {
            aiController = enemyInScene.gameObject.AddComponent<AIController>();
            aiController.waypoints = originalWaypoints;
            aiController.EnemySpeed = originalEnemySpeed;
            aiController.ChaseSpeed = originalChaseSpeed;
            aiController.maxHealth = originalMaxHealth;
            
            enemyInScene = aiController;
        }

        enemyInScene.transform.position = spawnPosition;
        enemyInScene.transform.rotation = originalRotation;

        aiController.ReviveAtWaypoint1(); // ✅ NUEVO MÉTODO

        Debug.Log($"Enemigo revivido en: {spawnPosition}");
    }

    public void NotifyEnemyDeath(AIController deadEnemy)
    {
        if (deadEnemy == enemyInScene)
        {
            Debug.Log("Enemigo ha muerto. Presiona F3 para respawnear en Waypoint 1.");
        }
    }

    // ✅ NUEVO: Método para cambiar waypoint de respawn en tiempo de ejecución
    public void SetRespawnWaypoint(int waypointIndex)
    {
        if (originalWaypoints != null && waypointIndex >= 0 && waypointIndex < originalWaypoints.Length)
        {
            waypoint1 = originalWaypoints[waypointIndex];
            Debug.Log($"Waypoint de respawn cambiado a: {waypointIndex}");
        }
    }
}