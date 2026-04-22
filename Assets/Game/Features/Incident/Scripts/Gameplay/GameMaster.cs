using UnityEngine;

public class GameMaster : MonoBehaviour
{
    [SerializeField] private GameObject canvas;
    [SerializeField] private bool ensureMovementInputLock = true;

    private void Awake()
    {
        if (canvas != null)
        {
            canvas.SetActive(true);
        }

        if (ensureMovementInputLock && GetComponent<GameMasterUiMovementInputLock>() == null)
        {
            gameObject.AddComponent<GameMasterUiMovementInputLock>();
        }
    }
}

