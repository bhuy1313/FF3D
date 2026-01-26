using UnityEngine;
using UnityEngine.EventSystems;

public class UI : MonoBehaviour
{
    [Header("Panels (must have CanvasGroup)")]
    public GameObject mainPanel, loadPanel, settingsPanel, startPanel;

    [Header("Animators of menu buttons (optional)")]
    [SerializeField] private Animator[] menuButtonAnimators;

    private CanvasGroup mainCG, loadCG, settingsCG, startCG;

    void Awake()
    {
        mainCG = GetCG(mainPanel);
        loadCG = GetCG(loadPanel);
        settingsCG = GetCG(settingsPanel);
        startCG = GetCG(startPanel);
    }

    void Start() => ShowPanel(mainCG);

    private CanvasGroup GetCG(GameObject go)
    {
        if (!go) return null;
        var cg = go.GetComponent<CanvasGroup>();
        if (!cg) Debug.LogError($"{go.name} is missing CanvasGroup!");
        return cg;
    }

    private void SetVisible(CanvasGroup cg, bool visible)
    {
        if (cg == null) return;
        cg.alpha = visible ? 1f : 0f;
        cg.interactable = visible;
        cg.blocksRaycasts = visible;
        // Không cần SetActive(false) nữa
        // cg.gameObject.SetActive(true); // panel vẫn luôn active
    }

    void ResetMenuVisual()
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);

        if (menuButtonAnimators != null)
        {
            foreach (var a in menuButtonAnimators)
            {
                if (!a) continue;
                a.Rebind();
                a.Update(0f);
            }
        }
    }

    private void ShowPanel(CanvasGroup target)
    {
        // Ẩn tất cả
        SetVisible(mainCG, false);
        SetVisible(loadCG, false);
        SetVisible(settingsCG, false);
        SetVisible(startCG, false);

        // Hiện panel cần
        SetVisible(target, true);

        // Reset UI state để không kẹt màu
        ResetMenuVisual();
    }

    // Button events
    public void btn_mainMenu_Click() => ShowPanel(mainCG);
    public void btn_loadMenu_Click() => ShowPanel(loadCG);
    public void btn_settingsMenu_Click() => ShowPanel(settingsCG);
    public void btn_startMenu_Click() => ShowPanel(startCG);
}
