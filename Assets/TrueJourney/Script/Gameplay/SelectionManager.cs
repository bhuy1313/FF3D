using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class SelectionManager : MonoBehaviour
{
    [Header("UI Selection Box")]
    [SerializeField, Tooltip("UI Image đại diện cho khung chọn")]
    private RectTransform selectionBox;

    [Header("Tag cho các Unit có thể chọn")]
    [SerializeField, Tooltip("Tag của các Unit được phép chọn")]
    private string selectableTag = "Unit";

    [Header("Màu sắc khi chọn / bỏ chọn")]
    [SerializeField] private Color selectedColor = Color.green;
    [SerializeField] private Color defaultColor = Color.white;

    private Vector2 startPos;
    private Camera cam;
    private Canvas canvas;
    private bool isDragging;

    [Header("Danh sách Unit được chọn (reset mỗi lần)")]
    [SerializeField] private List<GameObject> units = new List<GameObject>();

    public IReadOnlyList<GameObject> SelectedUnits => units;

    private void Start()
    {
        // Lấy main camera
        cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[SelectionManager] Không tìm thấy Camera.main trong scene.");
        }

        // Kiểm tra selectionBox đã gán chưa
        if (selectionBox == null)
        {
            Debug.LogError("[SelectionManager] selectionBox CHƯA được gán trong Inspector.");
            enabled = false; // tắt script luôn cho đỡ crash
            return;
        }

        // Tìm Canvas cha của selectionBox
        canvas = selectionBox.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[SelectionManager] selectionBox KHÔNG nằm trong bất kỳ Canvas nào (GetComponentInParent<Canvas>() trả về null).");
            enabled = false;
            return;
        }

        selectionBox.gameObject.SetActive(false);
    }

    private void Update()
    {
        // Nếu vì lý do gì đó vẫn bị null thì thôi khỏi xử lý
        if (selectionBox == null || canvas == null || cam == null) return;

        if (Input.GetMouseButtonDown(0))
        {
            startPos = Input.mousePosition;
            isDragging = false;
        }

        if (Input.GetMouseButton(0))
        {
            if (Vector2.Distance(Input.mousePosition, startPos) > 5f)
            {
                if (!isDragging)
                {
                    selectionBox.gameObject.SetActive(true);
                    isDragging = true;
                }
                UpdateSelectionBox(Input.mousePosition);
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging)
            {
                SelectObjectsInBox(Input.mousePosition);
                selectionBox.gameObject.SetActive(false);
            }
            else
            {
                SelectSingleObject();
            }
        }
    }

    private void UpdateSelectionBox(Vector2 currentMousePos)
    {
        if (canvas == null || selectionBox == null) return;

        RectTransform canvasRect = canvas.transform as RectTransform;
        if (canvasRect == null) return;

        // Nếu Canvas là Screen Space - Overlay thì camera cho UI phải là null
        Camera uiCam = null;
        if (canvas.renderMode == RenderMode.ScreenSpaceCamera ||
            canvas.renderMode == RenderMode.WorldSpace)
        {
            uiCam = canvas.worldCamera != null ? canvas.worldCamera : cam;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, startPos, uiCam, out Vector2 localStart);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, currentMousePos, uiCam, out Vector2 localEnd);

        Vector2 size = localEnd - localStart;

        selectionBox.anchoredPosition = localStart + size / 2f;
        selectionBox.sizeDelta = new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
    }

    private void SelectObjectsInBox(Vector2 endPos)
    {
        ResetSelection();

        Vector2 min = Vector2.Min(startPos, endPos);
        Vector2 max = Vector2.Max(startPos, endPos);

        foreach (GameObject unit in GameObject.FindGameObjectsWithTag(selectableTag))
        {
            Vector3 screenPos = cam.WorldToScreenPoint(unit.transform.position);

            if (screenPos.x > min.x && screenPos.x < max.x &&
                screenPos.y > min.y && screenPos.y < max.y)
            {
                units.Add(unit);
                var renderer = unit.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = selectedColor;
                var u = unit.GetComponent<Unit>();
                if (u != null) u.isSelected = true;
            }
        }
    }

    private void SelectSingleObject()
    {
        ResetSelection();

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            GameObject unit = hit.collider.gameObject;
            if (unit.CompareTag(selectableTag))
            {
                units.Add(unit);
                var renderer = unit.GetComponent<Renderer>();
                if (renderer != null) renderer.material.color = selectedColor;
                var u = unit.GetComponent<Unit>();
                if (u != null) u.isSelected = true;
            }
        }
    }

    private void ResetSelection()
    {
        foreach (var unit in units)
        {
            if (unit == null) continue;
            var renderer = unit.GetComponent<Renderer>();
            if (renderer != null) renderer.material.color = defaultColor;
            var u = unit.GetComponent<Unit>();
            if (u != null) u.isSelected = false;
        }

        units.Clear();
    }
}
