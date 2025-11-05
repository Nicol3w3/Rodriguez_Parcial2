using UnityEngine;

public class AIController : MonoBehaviour
{
    [Header("AI Settings")]
    public Transform[] waypoints;
    public float EnemySpeed = 3f;
    public float ChaseSpeed = 5f;
    public float RotationSpeed = 5f;
    
    [Header("Health Settings")]
    public float maxHealth = 100f;
    public float currentHealth;
    public bool isInvulnerable = false;
    
    [Header("Damage Settings")]
    public float damageToPlayer = 5f;
    public Collider damageCollider;

    // ✅ NUEVO: Sistema de estados mejorado
    public enum EnemyState { Normal, Damaged, Chasing, Dead }
    [SerializeField] private EnemyState currentState = EnemyState.Normal;
    
    private Rigidbody rb;
    private FieldOfView fov;
    private Vector3 lastKnownPlayerPosition;
    private bool isChasing = false;

    // Waypoint para respawn (solo waypoint 1)
    private Transform respawnWaypoint;

    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    private Vector3 deathPosition;
    private bool wasGravityEnabled;
    private bool wasKinematic;

    // ✅ NUEVO: Timer para estado dañado
    private float damagedStateTimer = 0f;
    private float damagedStateDuration = 1f;

    void Start()
    {
        InitializeComponents();

        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.SaveCurrentConfig();
        }
        
        if (damageCollider == null)
        {
            damageCollider = GetComponentInChildren<Collider>();
            if (damageCollider != null)
            {
                damageCollider.isTrigger = true;
            }
        }

        // ✅ Configurar waypoint de respawn
        if (waypoints != null && waypoints.Length > 0)
        {
            respawnWaypoint = waypoints[0]; // Siempre waypoint 1 para respawn
        }
    }

    void InitializeComponents()
    {
        rb = GetComponent<Rigidbody>();
        fov = GetComponent<FieldOfView>();
        currentHealth = maxHealth;
        
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
        }
        
        if (fov == null)
        {
            Debug.LogError("FieldOfView component not found!");
        }

        // ✅ Estado inicial
        ChangeState(EnemyState.Normal);
    }

    void FixedUpdate()
    {
        if (currentState == EnemyState.Dead) 
        {
            if (fov != null && fov.canSeePlayer)
            {
                fov.canSeePlayer = false;
            }
            return;
        }

        // ✅ Manejar timer del estado dañado
        if (currentState == EnemyState.Damaged)
        {
            damagedStateTimer -= Time.deltaTime;
            if (damagedStateTimer <= 0f)
            {
                // Volver al estado normal después del tiempo de daño
                if (fov != null && fov.canSeePlayer)
                {
                    ChangeState(EnemyState.Chasing);
                }
                else
                {
                    ChangeState(EnemyState.Normal);
                }
            }
        }

        // Detección del jugador
        if (fov != null && fov.canSeePlayer)
        {
            if (!isChasing)
            {
                StartChasing();
            }
            lastKnownPlayerPosition = fov.playerRef.transform.position;
            
            // ✅ Cambiar a estado Chasing si no está dañado
            if (currentState != EnemyState.Damaged)
            {
                ChangeState(EnemyState.Chasing);
            }
        }
        else if (isChasing)
        {
            StopChasing();
        }

        // ✅ COMPORTAMIENTO POR ESTADO
        switch (currentState)
        {
            case EnemyState.Normal:
                IdleBehavior();
                break;
            case EnemyState.Damaged:
                DamagedBehavior();
                break;
            case EnemyState.Chasing:
                ChaseBehavior();
                break;
            case EnemyState.Dead:
                // No hacer nada
                break;
        }
    }

    // ✅ NUEVO: Comportamiento en estado Normal/Idle
    private void IdleBehavior()
    {
        // Comportamiento simple en idle - podría agregar rotación lenta o animación
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    // ✅ NUEVO: Comportamiento en estado Dañado
    private void DamagedBehavior()
    {
        // Podría agregar retroceso o comportamiento especial al recibir daño
        // Por ahora solo mantiene el estado temporal
    }

    // ✅ NUEVO: Comportamiento en estado Chasing
    private void ChaseBehavior()
    {
        if (fov.playerRef == null) return;

        Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
        RotateTowards(lastKnownPlayerPosition);
        
        float currentSpeed = isChasing ? ChaseSpeed : EnemySpeed;
        rb.MovePosition(transform.position + direction * currentSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, lastKnownPlayerPosition) < 1.5f)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    // ✅ NUEVO: Método para cambiar estado con debug
    private void ChangeState(EnemyState newState)
    {
        if (currentState == newState) return;

        EnemyState previousState = currentState;
        currentState = newState;

        // Debug del estado actual (solo muestra el nuevo estado)
        Debug.Log($"Estado del enemigo: {newState}");

        // Acciones específicas al entrar en cada estado
        switch (newState)
        {
            case EnemyState.Damaged:
                damagedStateTimer = damagedStateDuration;
                break;
            case EnemyState.Dead:
                Die();
                break;
        }
    }

    void Die()
    {
        currentHealth = 0;
        ChangeState(EnemyState.Dead);
        
        if (fov != null)
        {
            fov.SetDetectionEnabled(false);
            fov.canSeePlayer = false;
        }
        
        deathPosition = transform.position;
        
        if (rb != null)
        {
            wasGravityEnabled = rb.useGravity;
            wasKinematic = rb.isKinematic;
            
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.useGravity = false;
            rb.isKinematic = true;
        }
        
        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.NotifyEnemyDeath(this);
        }
        
        OnDeath?.Invoke();
        Debug.Log("Estado del enemigo: Dead");
        
        SetEnemyVisible(false);
    }

    public void ReviveAtWaypoint1()
    {
        if (currentState != EnemyState.Dead) return;
        
        // Restaurar físicas
        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.useGravity = wasGravityEnabled;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Reactivar detección
        if (fov != null)
        {
            fov.SetDetectionEnabled(true);
            fov.ResetDetection();
        }
        
        // ✅ TELEPORTAR AL WAYPOINT 1 para respawn
        if (respawnWaypoint != null)
        {
            transform.position = respawnWaypoint.position;
           // Debug.Log($"Enemigo revivido en Waypoint 1: {respawnWaypoint.position}");
        }
        
        // Restaurar estado
        ChangeState(EnemyState.Normal);
        currentHealth = maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        
        // Hacer visible
        SetEnemyVisible(true);
        
        OnHealthChanged?.Invoke(1f);
        Debug.Log("Estado del enemigo: Normal");
    }

    public void TakeDamage(float damageAmount)
    {
        if (isInvulnerable || currentState == EnemyState.Dead) return;

        currentHealth -= damageAmount;
        OnHealthChanged?.Invoke(currentHealth / maxHealth);
        
        // ✅ CAMBIAR A ESTADO DAÑADO
        ChangeState(EnemyState.Damaged);
        
//        Debug.Log($"💥 Enemigo recibió {damageAmount} de daño. Vida: {currentHealth} - Estado: Damaged");
        
        if (currentHealth <= 0)
        {
            ChangeState(EnemyState.Dead);
        }
    }

    // ✅ NUEVO: Método público para obtener el estado actual
    public EnemyState GetCurrentState()
    {
        return currentState;
    }

    // ✅ NUEVO: Métodos para verificar estados específicos
    public bool IsInNormalState() => currentState == EnemyState.Normal;
    public bool IsInDamagedState() => currentState == EnemyState.Damaged;
    public bool IsInChasingState() => currentState == EnemyState.Chasing;
    public bool IsDead() => currentState == EnemyState.Dead;

    void StartChasing()
    {
        isChasing = true;
//        Debug.Log("Enemigo comenzó a perseguir");
    }

    void StopChasing()
    {
        isChasing = false;
        ChangeState(EnemyState.Normal);
//        Debug.Log("Estado del enemigo: Normal");
    }

    // ✅ ELIMINADO: Método Patrol() ya no se usa

    void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, RotationSpeed * Time.deltaTime);
        }
    }

    public void Revive()
    {
        ReviveAtWaypoint1();
    }

    void SetEnemyVisible(bool visible)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
        
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (Collider c in colliders)
        {
            c.enabled = visible;
        }
        
        if (visible && !gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }

    // ✅ NUEVO: Debug visual en el editor
    private void OnDrawGizmosSelected()
    {
        // Color del gizmo según el estado
        Gizmos.color = currentState switch
        {
            EnemyState.Normal => Color.green,
            EnemyState.Damaged => Color.yellow,
            EnemyState.Chasing => Color.red,
            EnemyState.Dead => Color.gray,
            _ => Color.white
        };

        // Dibujar esfera de estado
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2f, 0.5f);

        // Dibujar waypoint de respawn si existe
        if (respawnWaypoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, respawnWaypoint.position);
            Gizmos.DrawWireCube(respawnWaypoint.position, Vector3.one * 0.5f);
        }
    }

    // ✅ NUEVO: Debug en UI (opcional)
    private void OnGUI()
    {
        #if UNITY_EDITOR
        if (Camera.current != null)
        {
            Vector3 screenPos = Camera.current.WorldToScreenPoint(transform.position + Vector3.up * 2f);
            if (screenPos.z > 0)
            {
                GUI.color = currentState switch
                {
                    EnemyState.Normal => Color.green,
                    EnemyState.Damaged => Color.yellow,
                    EnemyState.Chasing => Color.red,
                    EnemyState.Dead => Color.gray,
                    _ => Color.white
                };

                Rect labelRect = new Rect(screenPos.x - 50, Screen.height - screenPos.y - 30, 100, 20);
                GUI.Label(labelRect, $"Estado: {currentState}");
                GUI.Label(new Rect(labelRect.x, labelRect.y + 20, 100, 20), $"Vida: {currentHealth}");
            }
        }
        #endif
    }
}