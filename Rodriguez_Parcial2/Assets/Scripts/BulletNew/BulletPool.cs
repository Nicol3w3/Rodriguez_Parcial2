using System.Collections.Generic;
using UnityEngine;

public class BulletPool : MonoBehaviour
{
    [Header("Hybrid Bullet Pool")]
public BulletPoolSettings hybridBulletPool = new BulletPoolSettings()
{
    bulletPrefab = null, // Asignar el prefab HybridBullet
    poolSize = 20,
    expandable = true
};
    [System.Serializable]
    public class BulletPoolSettings
    {
        public BulletBase bulletPrefab;
        public int poolSize = 20;
        public bool expandable = true;
    }

    [Header("Pool Settings")]
    [SerializeField] private BulletPoolSettings[] bulletPools;
    
    private Dictionary<System.Type, Queue<BulletBase>> poolDictionary;
    private Dictionary<System.Type, BulletPoolSettings> settingsDictionary;
    private Dictionary<System.Type, int> activeBulletsCount;

    public static BulletPool Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<System.Type, Queue<BulletBase>>();
        settingsDictionary = new Dictionary<System.Type, BulletPoolSettings>();
        activeBulletsCount = new Dictionary<System.Type, int>();

        foreach (var settings in bulletPools)
        {
            if (settings.bulletPrefab == null) continue;
            
            System.Type bulletType = settings.bulletPrefab.GetType();
            Queue<BulletBase> objectPool = new Queue<BulletBase>();
            
            for (int i = 0; i < settings.poolSize; i++)
            {
                BulletBase bullet = CreateBullet(settings.bulletPrefab);
                objectPool.Enqueue(bullet);
            }
            
            poolDictionary.Add(bulletType, objectPool);
            settingsDictionary.Add(bulletType, settings);
            activeBulletsCount.Add(bulletType, 0);
        }
    }

    private BulletBase CreateBullet(BulletBase prefab)
    {
        BulletBase bullet = Instantiate(prefab);
        bullet.gameObject.SetActive(false);
        bullet.OnBulletDestroyed += OnBulletReturnedToPool;
        bullet.transform.SetParent(transform);
        return bullet;
    }

    public T GetBullet<T>(GameObject owner, Vector3 position, Vector3 direction, float damage = -1) where T : BulletBase
    {
        System.Type bulletType = typeof(T);
        
        if (!poolDictionary.ContainsKey(bulletType))
        {
            Debug.LogError($"No pool found for bullet type: {bulletType}");
            return null;
        }

        BulletBase bullet;
        
        if (poolDictionary[bulletType].Count > 0)
        {
            bullet = poolDictionary[bulletType].Dequeue();
        }
        else if (settingsDictionary[bulletType].expandable)
        {
            bullet = CreateBullet(settingsDictionary[bulletType].bulletPrefab);
        }
        else
        {
            Debug.LogWarning($"Pool for {bulletType} is empty and not expandable");
            return null;
        }

        activeBulletsCount[bulletType]++;
        bullet.Initialize(owner, position, direction, damage);
        
        return bullet as T;
    }

    private void OnBulletReturnedToPool(BulletBase bullet)
    {
        System.Type bulletType = bullet.GetType();
        
        if (poolDictionary.ContainsKey(bulletType))
        {
            poolDictionary[bulletType].Enqueue(bullet);
            activeBulletsCount[bulletType]--;
        }
    }

    public int GetActiveBulletsCount<T>() where T : BulletBase
    {
        System.Type bulletType = typeof(T);
        return activeBulletsCount.ContainsKey(bulletType) ? activeBulletsCount[bulletType] : 0;
    }
}
