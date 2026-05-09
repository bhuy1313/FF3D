using UnityEngine;

[RequireComponent(typeof(Animator))]
public class DisableAnimatorEvent : MonoBehaviour
{
    private Animator animator;

    private void Awake()
    {
        // Lấy component Animator nằm trên cùng GameObject này
        animator = GetComponent<Animator>();
    }

    /// <summary>
    /// Hàm này được dùng để gọi từ Animation Event.
    /// Khi được gọi, nó sẽ tắt component Animator đi.
    /// </summary>
    public void DisableAnimator()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    public void Start()
    {

    }
}
