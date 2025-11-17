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

    // Referencia al jugador para aplicar efectos
    private TPMovement_Controller playerController;

    private void Start()
    {
        playerController = GetComponent<TPMovement_Controller>();
        
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
                
                // ✅ APLICAR EFECTOS INMEDIATOS según el tipo
                ApplyCollectableEffect(collectable);
                
                Debug.Log($"Collectable obtenido: {collectable.collectableName}. Total: {collectables[i].quantity}");
                return;
            }
        }

        Debug.LogWarning($"Collectable no encontrado en inventario: {collectable.name}");
    }

    private void ApplyCollectableEffect(Collectable collectable)
    {
        if (playerController == null) return;

        switch (collectable.type)
        {
            case Collectable.CollectableType.Ammo:
                ApplyAmmoEffect(collectable);
                break;
                
            case Collectable.CollectableType.Health:
                ApplyHealthEffect(collectable);
                break;
                
            case Collectable.CollectableType.Generic:
                // Solo se acumula en el inventario
                break;
                
            default:
                Debug.LogWarning($"Tipo de collectable no manejado: {collectable.type}");
                break;
        }
    }

    private void ApplyAmmoEffect(Collectable collectable)
    {
        if (collectable.ammoMagazines > 0)
        {
            // Agregar cargadores al jugador
            for (int i = 0; i < collectable.ammoMagazines; i++)
            {
                playerController.AddMag();
            }
            
//            Debug.Log($"🔫 +{collectable.ammoMagazines} cargador(es) añadido(s)");
        }
    }

    private void ApplyHealthEffect(Collectable collectable)
    {
        if (collectable.healthRestore > 0)
        {
            playerController.RegenHeal(collectable.healthRestore);
            Debug.Log($"❤️ +{collectable.healthRestore} HP restaurado");
        }
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

    // ✅ NUEVO: Método para verificar si tiene un collectable específico
    public bool HasCollectable(Collectable collectable)
    {
        foreach (var data in collectables)
        {
            if (data.collectable == collectable && data.quantity > 0)
            {
                return true;
            }
        }
        return false;
    }

    // ✅ NUEVO: Método para usar un collectable del inventario
    public bool UseCollectable(Collectable collectable)
    {
        for (int i = 0; i < collectables.Length; i++)
        {
            if (collectables[i].collectable == collectable && collectables[i].quantity > 0)
            {
                collectables[i].quantity--;
                
                // Aplicar efecto nuevamente al usar
                ApplyCollectableEffect(collectable);
                
                Debug.Log($"Usado: {collectable.collectableName}. Restantes: {collectables[i].quantity}");
                return true;
            }
        }
        
        Debug.Log($"No hay {collectable.collectableName} en el inventario");
        return false;
    }
}
