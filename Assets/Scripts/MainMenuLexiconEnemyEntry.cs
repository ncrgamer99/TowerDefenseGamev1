using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MainMenuLexiconEnemyEntry
{
    [Header("Identity")]
    public string entryId = "enemy";
    public EnemyRole enemyRole = EnemyRole.Standard;
    public EnemyVariantType variantType = EnemyVariantType.Normal;
    public string title = "Gegner";
    public int sortOrder = 0;

    [Header("Description")]
    [TextArea(2, 5)]
    public string description = "";

    [Header("Base Stats")]
    public float hp = 10f;
    public float speed = 2f;
    public int armor = 0;
    public int baseDamage = 1;
    public string effectResistance = "Keine";

    [Header("Rewards")]
    public int goldReward = 0;
    public int xpReward = 0;
    public int globalXPReward = 0;

    [Header("Weakness")]
    public List<string> strongTowerNames = new List<string>();

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(title);
    }
}
