using UnityEngine;

[CreateAssetMenu(fileName = "Soldier_Config", menuName = "Enemy System/Soldier Config")]
public class SoldierConfigData : EnemyConfigData
{
    [Header("Soldier Specific")]
    public Transform[] patrolWaypoints;
    public float patrolWaitTime = 2f;
    public float attackRange = 3f;

    [Header("Shooting Settings")]
    public bool canShoot = true;
    public float shootRange = 15f;
    public float fireRate = 1.5f;
    public float bulletDamage = 20f;
    public GameObject bulletPrefab;
    public Transform shootPoint;
    public AudioClip shootSound;
}
