using UnityEngine;

public abstract class BulletBase : MonoBehaviour
{
    [Header("Base Bullet Settings")]
    [SerializeField] protected float damage = 25f;
    [SerializeField] protected float maxLifetime = 5f;
    [SerializeField] protected LayerMask hitLayers;
    [SerializeField] protected LayerMask obstacleLayers;
    [SerializeField] protected GameObject impactEffect;
    
    protected float spawnTime;
    protected bool isActive;
    protected Vector3 startPosition;
    protected GameObject owner;

    // Eventos para comunicación
    public System.Action<BulletBase, GameObject> OnBulletHit;
    public System.Action<BulletBase> OnBulletDestroyed;

    public virtual void Initialize(GameObject bulletOwner, Vector3 position, Vector3 direction, float bulletDamage = -1)
    {
        owner = bulletOwner;
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(direction);
        startPosition = position;
        
        if (bulletDamage > 0) damage = bulletDamage;
        
        spawnTime = Time.time;
        isActive = true;
        
        gameObject.SetActive(true);
    }

    protected abstract void UpdateBullet();

    protected virtual void ProcessHit(GameObject hitObject, Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!isActive) return;
        
        // Ignorar al dueño de la bala
        if (hitObject == owner) return;
        
        // Aplicar daño si es aplicable
        if (CanDamageObject(hitObject))
        {
            ApplyDamage(hitObject, damage, hitPoint);
        }
        
        // Efecto de impacto
        SpawnImpactEffect(hitPoint, hitNormal);
        
        // Notificar eventos
        OnBulletHit?.Invoke(this, hitObject);
        
        // Destruir/reciclar bala
        Deactivate();
    }

    protected virtual bool CanDamageObject(GameObject target)
    {
        // Verificar por tag (flexible)
        if (target.CompareTag("Enemy") && owner.CompareTag("Player")) return true;
        if (target.CompareTag("Player") && owner.CompareTag("Enemy")) return true;
        
        // Verificar por layer
        if (hitLayers != 0 && ((1 << target.layer) & hitLayers) != 0) return true;
        
        return false;
    }

    protected virtual void ApplyDamage(GameObject target, float damageAmount, Vector3 hitPoint)
{
    // Buscar componentes de salud en toda la jerarquía
    AIController enemy = target.GetComponentInParent<AIController>();
    
    // ✅ BUSCAR MÁS PROFUNDAMENTE SI NO SE ENCUENTRA
    if (enemy == null)
    {
        enemy = target.GetComponent<AIController>();
    }
    if (enemy == null && target.transform.parent != null)
    {
        enemy = target.transform.parent.GetComponent<AIController>();
    }
    if (enemy == null && target.transform.root != null)
    {
        enemy = target.transform.root.GetComponent<AIController>();
    }
    
    TPMovement_Controller player = target.GetComponentInParent<TPMovement_Controller>();

    if (enemy != null && !enemy.IsDead())
    {
        enemy.TakeDamage(damageAmount);
//        Debug.Log($"✅ Daño aplicado a enemigo: {damageAmount}. Objeto: {target.name}");
        
        // ✅ DEBUG: Mostrar información adicional
//        Debug.Log($"🎯 Enemigo: {enemy.GetEnemyName()}, Salud: {damageAmount} aplicado");
    }
    else if (player != null)
    {
        player.TakeDamage(damageAmount);
        Debug.Log($"✅ Daño aplicado a jugador: {damageAmount}");
    }
    else
    {
//        Debug.Log($"❌ No se pudo aplicar daño. Objeto: {target.name}, Layer: {LayerMask.LayerToName(target.layer)}");
    }
}

    protected virtual void SpawnImpactEffect(Vector3 position, Vector3 normal)
    {
        if (impactEffect != null)
        {
            Instantiate(impactEffect, position, Quaternion.LookRotation(normal));
        }
    }

    protected virtual void CheckLifetime()
    {
        if (Time.time - spawnTime >= maxLifetime)
        {
            Deactivate();
        }
    }

    public virtual void Deactivate()
    {
        isActive = false;
        OnBulletDestroyed?.Invoke(this);
        gameObject.SetActive(false);
    }

    protected virtual void Update()
    {
        if (!isActive) return;
        
        CheckLifetime();
        UpdateBullet();
    }
}