using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Config", menuName = "Enemy System/Enemy Config")]
public class EnemyConfigData : ScriptableObject
{
    [Header("Basic Settings")]
    public string enemyName;
    public EnemyType enemyType;
    
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public bool isInvulnerable = false;
    
    [Header("Movement Settings")]
    public float movementSpeed = 3f;
    public float chaseSpeed = 5f;
    public float rotationSpeed = 5f;
    public bool canMove = true;
    
    [Header("Detection Settings")]
    public float detectionRadius = 10f;
    [Range(0, 360)]
    public float detectionAngle = 90f;
    public LayerMask targetMask;
    public LayerMask obstructionMask;
    
    [Header("Combat Settings")]
    public float damageToPlayer = 5f;
    public bool canDealDamage = true;
    
    [Header("AI Behavior Settings")]
    public bool canPatrol = true;
    public bool canChase = true;
    public bool usePersistentChase = true; // Soldier: true, Camera: false
    public bool useObstacleAvoidance = true; // Soldier: true, Camera: false
    public float chaseTimeout = 0f; // ✅ Ahora puede ser 0 (sin timeout) o un valor alto
    
    [Header("Visual Settings")]
    public Material enemyMaterial;
    public GameObject deathEffect;
    public GameObject hitEffect;
    
    [Header("Audio Settings")]
    public AudioClip detectionSound;
    public AudioClip deathSound;
    public AudioClip hitSound;
}

public enum EnemyType
{
    Soldier,
    SurveillanceCamera,
    Turret,
    Melee
}