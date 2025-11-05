using UnityEngine;

[CreateAssetMenu(fileName = "New Collectable", menuName = "Game/Collectable")]
public class Collectable : ScriptableObject
{
    public string collectableName;
    public string description;
    public Sprite icon;
    public int value = 1;
}
