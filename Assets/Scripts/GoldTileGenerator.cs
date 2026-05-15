using UnityEngine;

public class GoldTileGenerator : MonoBehaviour
{
    public GameManager gameManager;
    public int goldPerSecond = 5;

    private float goldTimer = 0f;

    private void Update()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null || gameManager.isGameOver || gameManager.currentPhase != GamePhase.Wave)
        {
            goldTimer = 0f;
            return;
        }

        goldTimer += Time.deltaTime;

        while (goldTimer >= 1f)
        {
            goldTimer -= 1f;
            gameManager.AddGold(goldPerSecond, true, RunGoldSource.Other);
        }
    }
}
