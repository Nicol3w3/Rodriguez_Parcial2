using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [System.Serializable]
    public class CollectableData
    {
        public Collectable collectable;
        public int quantity;
    }

    public CollectableData[] collectables;

    private void Start()
    {
        // Inicializar todas las cantidades en 0
        for (int i = 0; i < collectables.Length; i++)
        {
            collectables[i].quantity = 0;
        }
    }

    public void AddCollectable(Collectable collectable)
    {
        for (int i = 0; i < collectables.Length; i++)
        {
            if (collectables[i].collectable == collectable)
            {
                collectables[i].quantity += collectable.value;
                Debug.Log($"Collectable obtenido: {collectable.collectableName}. Total: {collectables[i].quantity}");
                return;
            }
        }

        Debug.LogWarning($"Collectable no encontrado en inventario: {collectable.name}");
    }

    public void DebugInventory()
    {
        Debug.Log("=== INVENTARIO ===");
        foreach (var data in collectables)
        {
            Debug.Log($"{data.collectable.collectableName}: {data.quantity}");
        }
        Debug.Log("==================");
    }
}
