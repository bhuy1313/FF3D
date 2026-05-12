using UnityEngine;

public class GameMaster : MonoBehaviour
{
    [SerializeField] private bool ensureMovementInputLock = true;
    [SerializeField] private bool ensureUiBlurController = true;
    [SerializeField] private bool ensureProcedureDebugOverlay = false;

    private void Awake()
    {
        if (ensureMovementInputLock && GetComponent<GameMasterUiMovementInputLock>() == null)
        {
            gameObject.AddComponent<GameMasterUiMovementInputLock>();
        }

        if (ensureUiBlurController && GetComponent<GameMasterUiBlurController>() == null)
        {
            gameObject.AddComponent<GameMasterUiBlurController>();
        }

        if (ensureProcedureDebugOverlay && GetComponent<IncidentProcedureDebugOverlay>() == null)
        {
            gameObject.AddComponent<IncidentProcedureDebugOverlay>();
        }
    }
}

