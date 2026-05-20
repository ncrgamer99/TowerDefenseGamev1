using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MainMenuLexiconTowerEntry
{
    [Header("Identity")]
    public string entryId = "tower";
    public TowerRole towerRole = TowerRole.Basic;
    public string title = "Tower";
    public int sortOrder = 0;
    public bool isGeneralInfo = false;

    [Header("Description")]
    [TextArea(2, 6)]
    public string description = "";

    [TextArea(4, 10)]
    public string generalInfoText = "";

    [Header("Base Stats")]
    public int cost = 0;
    public int damage = 0;
    public float range = 0f;
    public float fireRate = 0f;
    public string effectText = "Keine";

    [Header("Use Case")]
    public List<string> strongAgainst = new List<string>();

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(title);
    }
}
