using UnityEngine;

[RequireComponent(typeof(Animator))]
public class ResetHoverAnimator : MonoBehaviour
{
    [SerializeField]
    private Animator anim;

    private void Awake()
    {
        if (anim == null)
            anim = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        if (anim == null)
            return;

        // Rebind and force the animator to the start of its current state
        anim.Rebind();
        var state = anim.GetCurrentAnimatorStateInfo(0);
        anim.Play(state.fullPathHash, 0, 0f);
        anim.Update(0f);
    }

    private void OnDisable()
    {
        if (anim == null)
            return;

        // Ensure animator is in a clean state when disabled
        anim.Rebind();
        anim.Update(0f);
    }
}
