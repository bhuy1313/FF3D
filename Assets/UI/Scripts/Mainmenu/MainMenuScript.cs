using UnityEngine;

public class MainMenuScript : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private CanvasGroup mainMenuPanel;
    [SerializeField] private CanvasGroup settingPanel;

    // Nếu còn panel khác, kéo vào đây
    [SerializeField] private CanvasGroup[] otherPanelsToHideOnLoad;
    private Setting_UIScript settingUI;

    private void Awake()
    {
        settingUI = GetComponent<Setting_UIScript>();

        // Khi load: MainMenu mở, còn lại ẩn
        SetPanelActive(mainMenuPanel, true);
        SetPanelActive(settingPanel, false);

        if (otherPanelsToHideOnLoad != null)
        {
            foreach (var p in otherPanelsToHideOnLoad)
                SetPanelActive(p, false);
        }
    }

    // Gán cho nút Setting (OnClick)
    public void OpenSettingPanel()
    {
        if (settingUI == null)
        {
            settingUI = GetComponent<Setting_UIScript>();
        }

        if (settingUI != null)
        {
            settingUI.BeginEditSession();
        }

        SetPanelActive(mainMenuPanel, false);
        SetPanelActive(settingPanel, true);
    }

    // Gán cho nút Back (OnClick)
    public void BackToMain()
    {
        if (settingUI == null)
        {
            settingUI = GetComponent<Setting_UIScript>();
        }

        if (settingUI != null && settingUI.HandleBackRequest(BackToMainImmediate))
        {
            return;
        }

        BackToMainImmediate();
    }

    private void BackToMainImmediate()
    {
        // Tắt tất cả panel phụ (bao gồm setting)
        SetPanelActive(settingPanel, false);

        if (otherPanelsToHideOnLoad != null)
        {
            foreach (var p in otherPanelsToHideOnLoad)
                SetPanelActive(p, false);
        }

        // Bật main menu
        SetPanelActive(mainMenuPanel, true);
    }

    private void SetPanelActive(CanvasGroup panel, bool active)
    {
        if (panel == null) return;

        panel.alpha = active ? 1f : 0f;
        panel.interactable = active;
        panel.blocksRaycasts = active;
    }
}
