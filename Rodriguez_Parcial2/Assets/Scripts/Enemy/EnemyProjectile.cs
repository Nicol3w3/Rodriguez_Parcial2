using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 30f;
    public float damage = 20f;
    public float maxLifetime = 3f;
    
    [Header("Visual Effects")]
    public GameObject impactEffect;
    
    private Vector3 direction;
    private float spawnTime;
    private GameObject owner;

    public void Initialize(GameObject projectileOwner, Vector3 startPosition, Vector3 shootDirection)
    {
        owner = projectileOwner;
        transform.position = startPosition;
        direction = shootDirection.normalized;
        spawnTime = Time.time;
        
        // Rotar el proyectil hacia la dirección
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void Update()
    {
        // Mover el proyectil
        transform.position += direction * speed * Time.deltaTime;
        
        // Verificar tiempo de vida
        if (Time.time - spawnTime >= maxLifetime)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Ignorar colisión con el dueño
        if (other.gameObject == owner) return;
        
        // Ignorar colisión con otros enemigos
        if (other.CompareTag("Enemy")) return;
        
        // Verificar si es el jugador
        if (other.CompareTag("Player"))
        {
            TPMovement_Controller player = other.GetComponent<TPMovement_Controller>();
            if (player != null)
            {
                player.TakeDamage(damage);
            }
        }
        
        // Efecto de impacto
        if (impactEffect != null)
        {
            Instantiate(impactEffect, transform.position, Quaternion.identity);
        }
        
        // Destruir proyectil
        Destroy(gameObject);
    }
}