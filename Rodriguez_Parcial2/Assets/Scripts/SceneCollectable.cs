using UnityEngine;

public class SceneCollectable : MonoBehaviour
{
    public Collectable collectableData;
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerInventory inventory = other.GetComponent<PlayerInventory>();
            if (inventory != null)
            {
                inventory.AddCollectable(collectableData);
                Destroy(gameObject); // Desaparece del escenario
            }
        }
    }
}
