using UnityEngine;

public class AIController : MonoBehaviour
{
    [Header("Enemy Configuration")]
    public EnemyConfigData enemyConfig;
    
    [Header("Ground Detection")]
    public float groundCheckDistance = 0.1f;
    public LayerMask groundMask = 1; // Capa por defecto
    private bool isGrounded;

    [Header("Runtime References")]
    protected Rigidbody rb;
    protected FieldOfView fov;
    protected Collider damageCollider;
    
    // Estados protegidos para herencia
    protected enum AIState { Patrolling, Chasing, Dead, Idle }
    protected AIState currentState = AIState.Idle;
    
    protected float currentHealth;
    protected bool isChasing = false;
    protected Vector3 lastKnownPlayerPosition;

    // Eventos
    public System.Action<float> OnHealthChanged;
    public System.Action OnDeath;

    protected virtual void Start()
    {
        InitializeFromConfig();
        RegisterWithManager();
    }

    protected virtual void InitializeFromConfig()
    {
        if (enemyConfig == null)
        {
            Debug.LogError("No EnemyConfig assigned to " + gameObject.name);
            return;
        }

        // Inicializar desde Scriptable Object
        currentHealth = enemyConfig.maxHealth;
        
        // Configurar componentes
        rb = GetComponent<Rigidbody>();
        fov = GetComponent<FieldOfView>();
        
        if (fov != null && enemyConfig != null)
        {
            fov.radius = enemyConfig.detectionRadius;
            fov.angle = enemyConfig.detectionAngle;
            fov.targetMask = enemyConfig.targetMask;
            fov.obstructionMask = enemyConfig.obstructionMask;
        }

        // Configurar collider de daño
        damageCollider = GetComponentInChildren<Collider>();
        if (damageCollider != null && enemyConfig.canDealDamage)
        {
            damageCollider.isTrigger = true;
        }

        Debug.Log($"✅ {enemyConfig.enemyName} inicializado - Salud: {currentHealth}");
    }

    
    protected virtual void FixedUpdate()
    {
        if (currentState == AIState.Dead) return;
        
        CheckGrounded();
        HandleDetection();
        HandleStateBehavior();
    }

     private void CheckGrounded()
{
    // Raycast simple hacia abajo
    if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, groundCheckDistance, groundMask))
    {
        isGrounded = true;
        
        // Ajustar para mantener una altura adecuada sobre el suelo
        float desiredHeightAboveGround = 1.0f; // Ajusta este valor según la altura de tu personaje
        
        if (transform.position.y < hit.point.y + desiredHeightAboveGround)
        {
            Vector3 pos = transform.position;
            pos.y = hit.point.y + desiredHeightAboveGround;
            transform.position = pos;
            
            // Resetear velocidad vertical
            Vector3 velocity = rb.linearVelocity;
            velocity.y = 0;
            rb.linearVelocity = velocity;
        }
        
//        Debug.Log($"✅ EN SUELO - Distancia: {hit.distance:F3}, Altura ajustada: {transform.position.y:F2}");
    }
    else
    {
        isGrounded = false;
    //    Debug.Log("❌ NO EN SUELO");
    }
    
    // Debug visual
    Debug.DrawRay(transform.position + Vector3.up * 0.5f, Vector3.down * groundCheckDistance, 
                 isGrounded ? Color.green : Color.red);
}

    protected virtual void HandleDetection()
{
    if (fov != null)
    {
        // Debug del FieldOfView
        if (fov.canSeePlayer)
        {
//            Debug.Log($"🎯 JUGADOR DETECTADO - Posición: {fov.playerRef.transform.position}");
        }
        
        if (fov.canSeePlayer)
        {
            if (!isChasing)
            {
                StartChasing();
            }
            lastKnownPlayerPosition = fov.playerRef.transform.position;
        }
        else if (isChasing)
        {
        //    Debug.Log($"❌ PERDIÓ AL JUGADOR - Buscando en última posición conocida");
            StopChasing();
        }
    }
    else
    {
        Debug.LogWarning("FieldOfView no encontrado en el enemy");
    }
}

    protected virtual void HandleStateBehavior()
    {
        switch (currentState)
        {
            case AIState.Patrolling:
                PatrolBehavior();
                break;
            case AIState.Chasing:
                ChaseBehavior();
                break;
            case AIState.Idle:
                IdleBehavior();
                break;
        }
    }

   protected virtual void ChaseBehavior()
{
    if (fov.playerRef == null || !enemyConfig.canMove) return;

    Vector3 direction = (lastKnownPlayerPosition - transform.position).normalized;
    direction.y = 0; // Solo movimiento horizontal
    
    RotateTowards(lastKnownPlayerPosition);
    
    float currentSpeed = isChasing ? enemyConfig.chaseSpeed : enemyConfig.movementSpeed;
    
    // Movimiento simple y directo
    Vector3 targetVelocity = direction * currentSpeed;
    rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);
}

// En AIController.cs
protected virtual void PatrolBehavior()
{
    // Comportamiento base vacío - será overrideado en SoldierAIController
    currentState = AIState.Idle; // Por defecto pasar a idle si no hay patrulla
}

    protected virtual void IdleBehavior()
    {
        // Comportamiento cuando está inactivo
    }

    protected virtual void RotateTowards(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 
                enemyConfig.rotationSpeed * Time.deltaTime);
        }
    }

    protected virtual void StartChasing()
    {
        isChasing = true;
        currentState = AIState.Chasing;
        
        // Efecto de sonido
        if (enemyConfig.detectionSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.detectionSound, transform.position);
        }
        
//        Debug.Log($"{enemyConfig.enemyName} comenzó a perseguir al jugador!");
    }

    protected virtual void StopChasing()
    {
        isChasing = false;
        currentState = GetDefaultState();
    }

    protected virtual AIState GetDefaultState()
    {
        return AIState.Idle;
    }

    public virtual void TakeDamage(float damageAmount)
    {
        if (enemyConfig.isInvulnerable || currentState == AIState.Dead) return;

        currentHealth -= damageAmount;
        OnHealthChanged?.Invoke(currentHealth / enemyConfig.maxHealth);
        
        // Efecto de golpe
        if (enemyConfig.hitEffect != null)
        {
            Instantiate(enemyConfig.hitEffect, transform.position, Quaternion.identity);
        }
        
        if (enemyConfig.hitSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.hitSound, transform.position);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        currentHealth = 0;
        currentState = AIState.Dead;
        
        // Efectos de muerte
        if (enemyConfig.deathEffect != null)
        {
            Instantiate(enemyConfig.deathEffect, transform.position, Quaternion.identity);
        }
        
        if (enemyConfig.deathSound != null)
        {
            AudioSource.PlayClipAtPoint(enemyConfig.deathSound, transform.position);
        }

        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.NotifyEnemyDeath(this);
        }
        
        OnDeath?.Invoke();
        SetEnemyVisible(false);
        
        Debug.Log($"{enemyConfig.enemyName} ha sido derrotado!");
    }

    public virtual void Revive()
    {
        currentState = GetDefaultState();
        currentHealth = enemyConfig.maxHealth;
        isChasing = false;
        lastKnownPlayerPosition = Vector3.zero;
        
        if (fov != null)
        {
            fov.canSeePlayer = false;
        }
        
        SetEnemyVisible(true);
        OnHealthChanged?.Invoke(1f);
    }

    protected virtual void SetEnemyVisible(bool visible)
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
    }

    protected virtual void RegisterWithManager()
    {
        if (EnemyRespawnManager.Instance != null)
        {
            EnemyRespawnManager.Instance.RegisterEnemy(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && enemyConfig.canDealDamage)
        {
            TPMovement_Controller player = other.GetComponent<TPMovement_Controller>();
            if (player != null)
            {
                player.TakeDamage(enemyConfig.damageToPlayer);
            }
        }
    }

    public bool IsDead()
    {
        return currentState == AIState.Dead;
    }

    public string GetEnemyName()
    {
        return enemyConfig != null ? enemyConfig.enemyName : "Unnamed Enemy";
    }
}
