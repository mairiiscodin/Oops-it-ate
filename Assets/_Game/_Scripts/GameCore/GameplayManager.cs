using UnityEngine;
using UnityEngine.Tilemaps;

public class GameplayManager : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    private void Start()
    {
        gridManager.Initialize();
    }
}
