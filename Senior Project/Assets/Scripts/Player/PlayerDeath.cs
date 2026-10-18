using UnityEngine;
using UnityEngine.SceneManagement;


public class PlayerDeath : MonoBehaviour
{

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Monster"))
        {
            GameManager.Instance.ChangeState(GameManager.State.GameOver);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Monster"))
        {
            GameManager.Instance.ChangeState(GameManager.State.GameOver);
        }
    }

}