using UnityEngine;

[CreateAssetMenu(fileName = "Soldier_Config", menuName = "Enemy System/Soldier Config")]
public class SoldierConfigData : EnemyConfigData
{
    [Header("Soldier Specific")]
    public Transform[] patrolWaypoints;
    public float patrolWaitTime = 2f;
    public float attackRange = 3f;
}
