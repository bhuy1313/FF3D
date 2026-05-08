using UnityEngine;

public class GameMaster : MonoBehaviour
{
    [SerializeField] private bool ensureMovementInputLock = true;
    [SerializeField] private bool ensureProcedureDebugOverlay = true;

    private void Awake()
    {
        if (ensureMovementInputLock && GetComponent<GameMasterUiMovementInputLock>() == null)
        {
            gameObject.AddComponent<GameMasterUiMovementInputLock>();
        }

        if (ensureProcedureDebugOverlay && GetComponent<IncidentProcedureDebugOverlay>() == null)
        {
            gameObject.AddComponent<IncidentProcedureDebugOverlay>();
        }
    }
}

