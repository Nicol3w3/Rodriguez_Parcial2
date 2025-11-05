using UnityEngine;

public class bulletScript : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 50f;
    [SerializeField] private float maxRange = 30f;
    [SerializeField] private float maxLifetime = 5f;
    [SerializeField] private GameObject impactEffect;

    [Header("Damage Settings")]
    public float damage = 25f;

    [Header("Enemy Detection")]
    public string enemyTag = "Enemy";
    public LayerMask enemyLayer;
    public LayerMask obstacleLayer; // ✅ NUEVO: Capa de obstáculos

    public Vector3 target { get; set; }
    public bool hit { get; set; }

    private bool hasHit = false;
    private Vector3 startPosition;
    private float currentDistance;
    private float spawnTime;

    private void Start()
    {
        startPosition = transform.position;
        currentDistance = 0f;
        spawnTime = Time.time;
        
        // ✅ MEJORADO: Configuración de layers automática
        SetupComponents();
        
        // Asignar layer Bullet automáticamente
        gameObject.layer = LayerMask.NameToLayer("Bullet");
    }

    private void SetupComponents()
    {
        // Asegurar collider
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)collider).radius = 0.1f; // Collider más pequeño
        }
        collider.isTrigger = true;

        // ✅ MODIFICADO: Rigidbody NO kinematic para mejor detección
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = false; // ✅ CAMBIADO a false
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // ✅ Mejor detección
    }

    private void Update()
    {
        if (hasHit) return;

        // Timer de vida
        if (Time.time - spawnTime >= maxLifetime)
        {
            DestroyBullet();
            return;
        }

        // Rango máximo
        currentDistance = Vector3.Distance(startPosition, transform.position);
        if (currentDistance >= maxRange)
        {
            DestroyBullet();
            return;
        }

        // Movimiento con Physics (mejor que Transform.Translate)
        Vector3 moveDirection = (target - transform.position).normalized;
        
        // ✅ MEJORADO: Usar Rigidbody para movimiento más consistente
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = moveDirection * speed;
        }
        else
        {
            transform.Translate(moveDirection * speed * Time.deltaTime, Space.World);
        }
        
        if (moveDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(moveDirection);
        }
        
        // Destino alcanzado
        if (!hit && Vector3.Distance(transform.position, target) < 0.3f) // ✅ Reducido umbral
        {
            DestroyBullet();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        
//        Debug.Log($"🔍 Trigger con: {other.gameObject.name} - Tag: {other.tag} - Layer: {LayerMask.LayerToName(other.gameObject.layer)}");

        // ✅ MEJORADO: Ignorar más objetos
        if (other.CompareTag("Player") || other.CompareTag("Bullet") || 
            other.gameObject.layer == LayerMask.NameToLayer("Bullet"))
        {
            return;
        }

        // ✅ NUEVO: Verificar si es obstáculo
        if (obstacleLayer != 0 && ((1 << other.gameObject.layer) & obstacleLayer) != 0)
        {
            Debug.Log($"💥 Impacto con obstáculo: {other.gameObject.name}");
            DestroyBullet();
            return;
        }

        ProcessHit(other.gameObject, other);
    }

    // ✅ MEJORADO: Detección más robusta
    private void ProcessHit(GameObject hitObject, Collider hitCollider)
    {
//        Debug.Log($"💥 Procesando impacto con: {hitObject.name}");

        // ✅ NUEVO: Buscar AIController en toda la jerarquía del objeto impactado
        AIController enemyController = FindAIControllerInHierarchy(hitObject);

        if (enemyController != null && !enemyController.IsDead())
        {
            ApplyDamageToEnemy(enemyController, hitObject);
        }
        else
        {
//            Debug.Log($"💥 Impacto con objeto neutral: {hitObject.name}");
        }

        DestroyBullet();
    }

    // ✅ NUEVO: Búsqueda recursiva de AIController
    private AIController FindAIControllerInHierarchy(GameObject hitObject)
    {
        // Buscar en el objeto actual
        AIController controller = hitObject.GetComponent<AIController>();
        if (controller != null) return controller;

        // Buscar en padres
        Transform parent = hitObject.transform.parent;
        while (parent != null)
        {
            controller = parent.GetComponent<AIController>();
            if (controller != null) return controller;
            parent = parent.parent;
        }

        // Buscar en hijos
        controller = hitObject.GetComponentInChildren<AIController>();
        if (controller != null) return controller;

        return null;
    }

    // ✅ SIMPLIFICADO: Aplicar daño directo al controller encontrado
    private void ApplyDamageToEnemy(AIController enemy, GameObject hitObject)
    {
        if (enemy != null && !enemy.IsDead())
        {
            enemy.TakeDamage(damage);
            Debug.Log($"✅ ¡Daño aplicado al enemigo! Daño: {damage} - Objeto: {hitObject.name}");
        }
    }

    private void DestroyBullet()
    {
        if (hasHit) return;
        
        hasHit = true;
        
//        Debug.Log($"💀 Destruyendo bala - Posición: {transform.position}");

        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }
        
        // ✅ DETENER movimiento antes de destruir
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
        
        Destroy(gameObject);
    }

    // ✅ NUEVO: Para debugging en el editor
    private void OnDrawGizmos()
    {
        if (Application.isPlaying && !hasHit)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, target);
        }
    }
}