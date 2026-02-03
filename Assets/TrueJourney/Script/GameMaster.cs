using UnityEngine;

public class GameMaster : MonoBehaviour
{
    [SerializeField] private GameObject canvas;
    private void Awake()
    {
        canvas.SetActive(true);
    }
}

