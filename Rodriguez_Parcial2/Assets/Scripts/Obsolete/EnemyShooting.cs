using UnityEngine;

public class EnemyShootingSystem : MonoBehaviour
{
    [Header("Shooting Configuration")]
    [SerializeField] private bool canShoot = true;
    [SerializeField] private float shootRange = 15f;
    [SerializeField] private float fireRate = 1.5f;
    [SerializeField] private float bulletDamage = 20f;
    
    [Header("Projectile Settings")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private Transform shootPoint;
    [SerializeField] private float projectileSpeed = 30f;

    [Header("Target Settings")]
    [SerializeField] private TargetAcquisitionType targetType = TargetAcquisitionType.ByTag;
    [SerializeField] private GameObject playerTarget; // Para asignar manualmente
    [SerializeField] private string playerTag = "Player";
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject muzzleFlash;
    [SerializeField] private AudioClip shootSound;

    [Header("Debug")]
    [SerializeField] private bool enableDebug = true;
    
    private float nextFireTime = 0f;
    private AudioSource audioSource;
    private GameObject player;

     public enum TargetAcquisitionType
    {
        ByTag,
        ByReference,
        Automatic
    }

     private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
            
        FindPlayerTarget();
    }

     private void FindPlayerTarget()
    {
        switch (targetType)
        {
            case TargetAcquisitionType.ByTag:
                player = GameObject.FindGameObjectWithTag(playerTag);
                if (player == null && enableDebug)
                    Debug.LogError($"❌ No se encontró jugador con tag: {playerTag}");
                break;
                
            case TargetAcquisitionType.ByReference:
                player = playerTarget;
                if (player == null && enableDebug)
                    Debug.LogError("❌ playerTarget no asignado en el inspector");
                break;
                
            case TargetAcquisitionType.Automatic:
                // Buscar por tag primero, luego por nombre
                player = GameObject.FindGameObjectWithTag(playerTag);
                if (player == null)
                    player = GameObject.Find("Player"); // Buscar por nombre
                if (player == null && enableDebug)
                    Debug.LogError("❌ No se pudo encontrar al jugador automáticamente");
                break;
        }
        
        if (player != null && enableDebug)
            Debug.Log($"✅ Jugador encontrado: {player.name}");
    }

   public void TryShootAtPlayer()
    {
        if (!canShoot) return;
        if (Time.time < nextFireTime) return;
        if (player == null) 
        {
            // Intentar encontrar al jugador nuevamente
            FindPlayerTarget();
            if (player == null) return;
        }
        
        // Verificar distancia al jugador
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        if (distanceToPlayer > shootRange) 
        {
            if (enableDebug) Debug.Log($"📏 Fuera de rango: {distanceToPlayer} > {shootRange}");
            return;
        }
        
        // Verificar línea de visión
        if (!HasLineOfSightToPlayer()) 
        {
            if (enableDebug) Debug.Log("🚧 Línea de visión bloqueada por obstáculo");
            return;
        }
        
        if (enableDebug) Debug.Log("🔫 DISPARANDO PROYECTIL!");
        Shoot();
        nextFireTime = Time.time + fireRate;
    }

private bool HasLineOfSightToPlayer()
    {
        if (player == null) return false;
        
        Vector3 shootPosition = GetShootPosition();
        Vector3 playerPosition = player.transform.position + Vector3.up * 1f; // Apuntar al pecho
        
        RaycastHit hit;
        if (Physics.Raycast(shootPosition, (playerPosition - shootPosition).normalized, out hit, shootRange))
        {
            // Solo verificar si hay obstáculos
            bool isObstacle = hit.collider.CompareTag("Wall") || 
                             hit.collider.CompareTag("Obstacle");
            return !isObstacle;
        }
        
        return true;
    }

    private void Shoot()
{
    Vector3 shootPosition = GetShootPosition();
    Vector3 playerPosition = player.transform.position + Vector3.up * 1f; // Apuntar al pecho
    
    // ✅ CORREGIDO: Calcular dirección SIN componente Y para el movimiento
    Vector3 directionToPlayer = (playerPosition - shootPosition).normalized;
    
    // ✅ IMPORTANTE: Para el proyectil, usar dirección horizontal solamente
    // Mantener la dirección Y del shootPosition, no del jugador
    Vector3 shootDirection = new Vector3(directionToPlayer.x, 0, directionToPlayer.z).normalized;
    
    if (enableDebug)
    {
        Debug.Log($"🎯 Disparando desde: {shootPosition}");
        Debug.Log($"🎯 Hacia jugador: {playerPosition}");
        Debug.Log($"🎯 Dirección original: {directionToPlayer}");
        Debug.Log($"🎯 Dirección corregida: {shootDirection}");
    }
    
    // Verificar que tenemos prefab
    if (projectilePrefab == null)
    {
        Debug.LogError("❌ No hay projectilePrefab asignado!");
        return;
    }
    
    // Crear proyectil
    GameObject projectileObj = Instantiate(projectilePrefab, shootPosition, Quaternion.identity);
    EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
    
    if (projectile != null)
    {
        projectile.Initialize(gameObject, shootPosition, shootDirection);
        projectile.damage = bulletDamage;
        projectile.speed = projectileSpeed;
        
        if (enableDebug) Debug.Log("✅ Proyectil creado y lanzado");
    }
    else
    {
        Debug.LogError("❌ El projectilePrefab no tiene componente EnemyProjectile!");
    }
    
    // Efectos
    if (muzzleFlash != null)
    {
        GameObject flash = Instantiate(muzzleFlash, shootPosition, Quaternion.LookRotation(shootDirection));
        Destroy(flash, 0.5f);
    }
    
    if (shootSound != null)
    {
        audioSource.PlayOneShot(shootSound);
    }
    
    // Debug visual
    Debug.DrawRay(shootPosition, shootDirection * shootRange, Color.red, 1f);
}

    private Vector3 GetShootPosition()
    {
        if (shootPoint != null)
            return shootPoint.position;
        
        return transform.position + Vector3.up * 1.5f + transform.forward * 0.5f;
    }

    public void SetShootingEnabled(bool enabled)
    {
        canShoot = enabled;
    }
    
    public void SetFireRate(float newFireRate)
    {
        fireRate = newFireRate;
    }
    
    public void SetShootRange(float newRange)
    {
        shootRange = newRange;
    }
    
    public void SetBulletDamage(float newDamage)
    {
        bulletDamage = newDamage;
    }
    
    public void SetProjectilePrefab(GameObject prefab)
    {
        projectilePrefab = prefab;
    }
    
    public void SetShootPoint(Transform newShootPoint)
    {
        shootPoint = newShootPoint;
    }
    
    public void SetPlayerTarget(GameObject target)
    {
        player = target;
    }
}