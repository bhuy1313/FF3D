using System.Collections;
using ProceduralUIEffects.Lite;
using UnityEngine;
using UnityEngine.Events;

/*
Usage:
- CallInScheduler manages when CallInPanel appears.
- Assign skipButtonRoot if you want a visible Skip button while waiting.
- Hook the Skip button's OnClick to CallInSkipButtonBinder.OnSkipPressed().
- Hook any duty-start trigger to CallInScheduler.StartDutySession() or DutyStartButtonBinder.OnStartDutyPressed().
*/
[DisallowMultipleComponent]
public class CallInScheduler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject callPhaseCanvas;
    [SerializeField] private GameObject callInPanel;
    [SerializeField] private GameObject skipButtonRoot;
    [SerializeField] private TMPro.TextMeshProUGUI callInText;
    [SerializeField] private GameObject mainCallPhaseRoot;
    [SerializeField] private GameObject buttonDuty;

    [Header("Timing")]
    [SerializeField] private bool useRandomDelay = false;
    [SerializeField] private float fixedDelaySeconds = 10f;
    [SerializeField] private float randomDelayMinSeconds = 5f;
    [SerializeField] private float randomDelayMaxSeconds = 15f;

    [Header("Behavior")]
    [SerializeField] private bool hideCallInPanelOnAwake = true;
    [SerializeField] private bool hideCanvasOnAwake = true;
    [SerializeField] private bool autoShowSkipButtonWhenWaiting = true;
    [SerializeField] private bool hideSkipButtonAfterCallInShown = true;
    [SerializeField] private bool enableDebugLogs = false;

    [Header("Events")]
    [SerializeField] private UnityEvent onDutyStarted;
    [SerializeField] private UnityEvent onWaitingForIncomingCall;
    [SerializeField] private UnityEvent onCallInShown;
    [SerializeField] private UnityEvent onSkipUsed;

    private bool dutyStarted;
    private bool callInAlreadyShown;
    private Coroutine waitingCoroutine;

    private void Awake()
    {
        if (hideCallInPanelOnAwake && callInPanel != null)
        {
            callInPanel.SetActive(false);
        }

        if (hideCanvasOnAwake && callPhaseCanvas != null)
        {
            callPhaseCanvas.SetActive(false);
        }

        if (skipButtonRoot != null)
        {
            skipButtonRoot.SetActive(false);
        }

        if (callInPanel == null)
        {
            Debug.LogWarning($"{nameof(CallInScheduler)} on {name}: callInPanel is not assigned.", this);
        }
    }

    public void StartDutySession()
    {
        if (dutyStarted)
        {
            return;
        }

        dutyStarted = true;
        callInAlreadyShown = false;

        if (callPhaseCanvas != null)
        {
            callPhaseCanvas.SetActive(true);
        }

        if (callInPanel != null)
        {
            callInPanel.SetActive(false);
        }

        onDutyStarted?.Invoke();
        onWaitingForIncomingCall?.Invoke();

        if (autoShowSkipButtonWhenWaiting && skipButtonRoot != null)
        {
            skipButtonRoot.SetActive(true);
        }

        float delay = GetDelaySeconds();
        waitingCoroutine = StartCoroutine(WaitAndShowCallIn(delay));

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallInScheduler)}: Duty started. Waiting {delay:0.##} seconds.", this);
        }
    }

    public void ShowCallInNow()
    {
        if (!dutyStarted)
        {
            StartDutySession();
        }

        if (waitingCoroutine != null)
        {
            StopCoroutine(waitingCoroutine);
            waitingCoroutine = null;
        }

        onSkipUsed?.Invoke();
        ShowCallInInternal();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallInScheduler)}: ShowCallInNow triggered.", this);
        }
    }

    public void ResetScheduler()
    {
        if (waitingCoroutine != null)
        {
            StopCoroutine(waitingCoroutine);
            waitingCoroutine = null;
        }

        dutyStarted = false;
        callInAlreadyShown = false;

        if (callInPanel != null)
        {
            callInPanel.SetActive(false);
        }

        if (skipButtonRoot != null)
        {
            skipButtonRoot.SetActive(false);
        }

        if (hideCanvasOnAwake && callPhaseCanvas != null)
        {
            callPhaseCanvas.SetActive(false);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallInScheduler)}: Scheduler reset.", this);
        }
    }

    private IEnumerator WaitAndShowCallIn(float delaySeconds)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, delaySeconds));

        waitingCoroutine = null;

        if (!callInAlreadyShown)
        {
            ShowCallInInternal();
        }
    }

    private void ShowCallInInternal()
    {
        if (callInAlreadyShown)
        {
            return;
        }

        callInAlreadyShown = true;

        if (callInPanel != null)
        {
            callInPanel.SetActive(true);
        }

        if (hideSkipButtonAfterCallInShown && skipButtonRoot != null)
        {
            skipButtonRoot.SetActive(false);
        }

        onCallInShown?.Invoke();

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallInScheduler)}: Call In shown.", this);
        }
    }

    private float GetDelaySeconds()
    {
        if (!useRandomDelay)
        {
            return Mathf.Max(0f, fixedDelaySeconds);
        }

        float min = Mathf.Max(0f, randomDelayMinSeconds);
        float max = Mathf.Max(min, randomDelayMaxSeconds);
        return Random.Range(min, max);
    }

    public void AcceptCall()
    {
        if (waitingCoroutine != null)
        {
            StopCoroutine(waitingCoroutine);
            waitingCoroutine = null;
        }

        if (callInText != null)
        {
            CallPhaseUiChromeText.ApplyCurrentFont(callInText);
            string connectingCallText = CallPhaseUiChromeText.Tr("callphase.value.connecting_call", "CONNECTING CALL...");
            PUAP_Core proceduralTextAnimator = callInText.GetComponent<PUAP_Core>();
            if (proceduralTextAnimator != null)
            {
                proceduralTextAnimator.SetText(connectingCallText);
            }
            else
            {
                callInText.text = connectingCallText;
            }

            callInText.ForceMeshUpdate();
        }

        if (skipButtonRoot != null)
        {
            skipButtonRoot.SetActive(false);
        }

        StartCoroutine(ConnectingCallRoutine());
    }

    private IEnumerator ConnectingCallRoutine()
    {
        float delay = Random.Range(0.5f, 1f);
        yield return new WaitForSecondsRealtime(delay);

        if (callInPanel != null)
        {
            callInPanel.SetActive(false);
        }

        if (mainCallPhaseRoot != null)
        {
            mainCallPhaseRoot.SetActive(true);

            CallPhaseScenarioContext scenarioContext = mainCallPhaseRoot.GetComponent<CallPhaseScenarioContext>();
            if (scenarioContext != null)
            {
                scenarioContext.BeginCallSession();
            }
        }

        if (buttonDuty != null)
        {
            buttonDuty.SetActive(false);
        }

        if (enableDebugLogs)
        {
            Debug.Log($"{nameof(CallInScheduler)}: Call Accepted. Transitioned to Main Call Phase.", this);
        }
    }
}
