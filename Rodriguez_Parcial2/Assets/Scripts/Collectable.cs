using UnityEngine;

[CreateAssetMenu(fileName = "New Collectable", menuName = "Game/Collectable")]
public class Collectable : ScriptableObject
{
    public enum CollectableType
    {
        Ammo,
        Health,
        Key,
        Generic
    }

    public string collectableName;
    public string description;
    public Sprite icon;
    public int value = 1;
    public CollectableType type = CollectableType.Generic;
    
    [Header("Specific Effects")]
    public float healthRestore = 0f;
    public int ammoMagazines = 0;
}