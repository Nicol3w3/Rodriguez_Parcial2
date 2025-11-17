using UnityEngine;

public class SceneCollectable : MonoBehaviour
{
    public Collectable collectableData;
    
    [Header("Visual Settings")]
    public float rotationSpeed = 50f;
    public float floatHeight = 0.5f;
    public float floatSpeed = 2f;
    
    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
        
        // Configurar el collider si no existe
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<BoxCollider>().isTrigger = true;
        }
    }

    private void Update()
    {
        // Animación flotante y rotación
        transform.Rotate(0, rotationSpeed * Time.deltaTime, 0);
        
        // Movimiento flotante
        float newY = startPosition.y + Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddCollectable(collectableData);
                
                // Efecto visual/sonoro al recolectar
                PlayCollectionEffect();
                
                Destroy(gameObject); // Desaparece del escenario
            }
        }
    }

    private void PlayCollectionEffect()
    {
        // Puedes agregar aquí efectos de partículas o sonido
        Debug.Log($"🎁 Recolectado: {collectableData.collectableName}");
        
        // Ejemplo: efecto de partículas
        // if (collectionParticles != null) Instantiate(collectionParticles, transform.position, Quaternion.identity);
        
        // Ejemplo: sonido
        // if (collectionSound != null) AudioSource.PlayClipAtPoint(collectionSound, transform.position);
    }
}
