using UnityEngine;

public class GoalZone : MonoBehaviour
{
    
    public BoxCollider ObjectiveHitbox;
    void Awake()
    {
        ObjectiveHitbox = GetComponent<BoxCollider>();
    }


    /*
        I decided to go with just toggling the boxcollider on and off since I am under the assumption that the goalzones are 
        predetermined and that it will not be spawning in different areas each time.
            IF that is the case, then I will have to make a manager of some sort and have them be instantiated in specified coordinates,
        which can make things more interesting, but I hope it does not go that direction.
    */
    void Start()
    {
        ObjectiveHitbox.enabled = false;
    }
    public void ObjectivesMet()
    {
        ObjectiveHitbox.enabled = true;
    }

    void OnTriggerEnter(Collider collision)
    {
        if(collision.GetComponent<PlayerController>())
        {
            //Calls the win screen
        WinScreenManager.Instance.Open();
        }
    }
}
