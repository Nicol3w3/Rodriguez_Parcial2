using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class EnemyRespawnManager : MonoBehaviour
{
    [Header("Enemy Management")]
    public List<AIController> enemiesInScene = new List<AIController>();
    
    [Header("Respawn Settings")]
    public float spawnHeightAboveGround = 1.0f;
    public LayerMask groundLayerMask = 1;
    
    [Header("Input Settings")]
    public InputActionReference respawnInputReference;
    
    public static EnemyRespawnManager Instance { get; private set; }
    
    private Dictionary<AIController, EnemyRespawnData> enemyRespawnData = new Dictionary<AIController, EnemyRespawnData>();

    [System.Serializable]
    public class EnemyRespawnData
    {
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public EnemyConfigData enemyConfig;
    }

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
        FindAllEnemiesInScene();
    }

    void FindAllEnemiesInScene()
    {
        AIController[] foundEnemies = FindObjectsOfType<AIController>();
        enemiesInScene.Clear();
        enemyRespawnData.Clear();

        foreach (AIController enemy in foundEnemies)
        {
            RegisterEnemy(enemy);
        }

        Debug.Log($"🎯 Total enemigos registrados: {enemiesInScene.Count}");
    }

    public void RegisterEnemy(AIController enemy)
    {
        if (!enemiesInScene.Contains(enemy))
        {
            enemiesInScene.Add(enemy);
            
            EnemyRespawnData data = new EnemyRespawnData
            {
                originalPosition = GetSafeSpawnPosition(enemy.transform.position),
                originalRotation = enemy.transform.rotation,
                enemyConfig = enemy.enemyConfig
            };
            
            enemyRespawnData[enemy] = data;
            
           // Debug.Log($"✅ {enemy.GetEnemyName()} registrado");
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
        RespawnAllDeadEnemies();
    }

    public void RespawnAllDeadEnemies()
    {
        int respawnedCount = 0;
        
        foreach (AIController enemy in enemiesInScene)
        {
            if (enemy != null && enemy.IsDead())
            {
                RespawnEnemy(enemy);
                respawnedCount++;
            }
        }

//        Debug.Log($"🔁 Respawned {respawnedCount} enemigos");
    }

    public void RespawnEnemy(AIController enemy)
    {
        if (enemy == null || !enemyRespawnData.ContainsKey(enemy)) return;

        EnemyRespawnData data = enemyRespawnData[enemy];
        enemy.transform.position = data.originalPosition;
        enemy.transform.rotation = data.originalRotation;
        enemy.Revive();

//        Debug.Log($"🔁 Respawned {enemy.GetEnemyName()}");
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

    public void NotifyEnemyDeath(AIController deadEnemy)
    {
//        Debug.Log($"💀 {deadEnemy.GetEnemyName()} ha muerto. Usa F3 para respawn.");
    }
}