using UnityEngine;

public class AnimationTest : MonoBehaviour
{
   private Animator animator;

    void Start()
    {
        // Automatically get the Animator component attached to this GameObject
        animator = GetComponent<Animator>();
        animator.SetBool("Shoot",false);
    }

    void Update()
    {
        
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Activate the trigger parameter we set up in the Animator
            animator.SetBool("Shoot",true);
            Debug.Log("Shot");
        }

        if (Input.GetKeyUp(KeyCode.Space)){
            animator.SetBool("Shoot",false);
            Debug.Log("Shot done");
        }
    }
}
