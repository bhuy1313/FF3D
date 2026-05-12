using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GuideBookRuntimeBootstrap : MonoBehaviour
{
    [SerializeField] private SubMenuPanelController subMenuPanelController;
    [SerializeField] private GameObject guideScreenObject;
    [SerializeField] private CanvasGroup guideScreenCanvasGroup;
    [SerializeField] private GuideBookTabSwitcher guideBookTabSwitcher;
    [SerializeField] private Button closeButton;
    [SerializeField] private bool closeSubMenuWhenGuideOpens = true;
    [SerializeField] private string fallbackGuideScreenName = "GuideBookScreen";

    public CanvasGroup GuideScreenCanvasGroup => guideScreenCanvasGroup;
    public bool IsGuideBookVisible =>
        guideScreenCanvasGroup != null &&
        guideScreenCanvasGroup.alpha > 0.001f &&
        guideScreenCanvasGroup.blocksRaycasts;

    private void Awake()
    {
        EnsureInitialized();
        HideGuideScreen();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    public void OpenGuideBook()
    {
        EnsureInitialized();
        if (guideScreenObject == null)
        {
            Debug.LogWarning("GuideBookRuntimeBootstrap: Guide screen target is missing.", this);
            return;
        }

        guideScreenObject.SetActive(true);

        if (guideScreenCanvasGroup != null)
        {
            guideScreenCanvasGroup.alpha = 1f;
            guideScreenCanvasGroup.interactable = true;
            guideScreenCanvasGroup.blocksRaycasts = true;
        }

        if (guideBookTabSwitcher != null)
        {
            guideBookTabSwitcher.EnsureInitialized();
            guideBookTabSwitcher.ShowTab(0);
        }

        if (ShouldCloseSubMenuWhenGuideOpens())
        {
            subMenuPanelController.Close();
        }
    }

    public void HideGuideScreen()
    {
        EnsureInitialized();
        if (guideScreenObject == null)
        {
            return;
        }

        guideScreenObject.SetActive(true);

        if (guideScreenCanvasGroup != null)
        {
            guideScreenCanvasGroup.alpha = 0f;
            guideScreenCanvasGroup.interactable = false;
            guideScreenCanvasGroup.blocksRaycasts = false;
        }
    }

    public void RequestCloseGuideBook()
    {
        HideGuideScreen();
    }

    private void EnsureInitialized()
    {
        if (subMenuPanelController == null)
        {
            subMenuPanelController = GetComponent<SubMenuPanelController>();
        }

        if (subMenuPanelController == null)
        {
            subMenuPanelController = FindAnyObjectByType<SubMenuPanelController>(FindObjectsInactive.Include);
        }

        if (guideScreenObject == null && !string.IsNullOrWhiteSpace(fallbackGuideScreenName))
        {
            GameObject found = GameObject.Find(fallbackGuideScreenName);
            if (found != null)
            {
                guideScreenObject = found;
            }
        }

        if (guideScreenCanvasGroup == null && guideScreenObject != null)
        {
            guideScreenCanvasGroup = guideScreenObject.GetComponent<CanvasGroup>();
            if (guideScreenCanvasGroup == null)
            {
                guideScreenCanvasGroup = guideScreenObject.AddComponent<CanvasGroup>();
            }
        }

        if (guideBookTabSwitcher == null && guideScreenObject != null)
        {
            guideBookTabSwitcher = guideScreenObject.GetComponent<GuideBookTabSwitcher>();
            if (guideBookTabSwitcher == null)
            {
                guideBookTabSwitcher = guideScreenObject.GetComponentInChildren<GuideBookTabSwitcher>(true);
            }
        }

        if (closeButton == null && guideScreenObject != null)
        {
            closeButton = FindNestedButton(guideScreenObject.transform, "btnClose") ??
                          FindNestedButton(guideScreenObject.transform, "BtnClose") ??
                          FindNestedButton(guideScreenObject.transform, "CloseButton");
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(RequestCloseGuideBook);
            closeButton.onClick.AddListener(RequestCloseGuideBook);
        }

        if (subMenuPanelController != null)
        {
            subMenuPanelController.SetGuideAction(OpenGuideBook);
        }
    }

    private bool ShouldCloseSubMenuWhenGuideOpens()
    {
        if (!closeSubMenuWhenGuideOpens || subMenuPanelController == null || guideScreenObject == null)
        {
            return false;
        }

        return !guideScreenObject.transform.IsChildOf(subMenuPanelController.transform);
    }

    private static Button FindNestedButton(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Transform target = FindDeepChild(root, objectName);
        return target != null ? target.GetComponent<Button>() : null;
    }

    private static Transform FindDeepChild(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, objectName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindDeepChild(child, objectName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
