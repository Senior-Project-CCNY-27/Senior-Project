using UnityEngine;

public class GoalZone : MonoBehaviour
{

    void OnTriggerEnter(Collider collision)
    {
        if(collision.GetComponent<PlayerController>())
        {
           GameManager.Instance.ChangeState(GameManager.State.Win);
        }
    }
}
