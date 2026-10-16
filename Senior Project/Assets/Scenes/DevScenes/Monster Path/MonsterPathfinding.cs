using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
public class MonsterPathfinding : MonoBehaviour
{
    [SerializeField] Transform trackPos;
    NavMeshAgent navAgent;
    Vector3 monsterPos;
    Vector3 TargetPos;

    [SerializeField]Vector3 currentTrackedPos;

    [SerializeField]List<Transform> Targets = new List<Transform>();

    bool enraged = false;

    public float Timer = 5;
    void Awake()
    {
        navAgent = GetComponent<NavMeshAgent>();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        TargetPos = new Vector3(trackPos.position.x,0,trackPos.position.z);  
        navAgent.destination = CreateNewTarget();     
        navAgent.speed = 2; 
    }

    // Update is called once per frame
    void Update()
    {

        monsterPos = new Vector3(gameObject.transform.position.x,0,gameObject.transform.position.z);

        
        if(monsterPos == currentTrackedPos)
        { 
            Debug.Log("Touched");
           navAgent.destination = CreateNewTarget();
        }
        if(!enraged)
        {
        Timer -= Time.deltaTime;
            if(Timer<=0)
                {
                    enraged = true;
                    navAgent.speed = 5;
                }            
        }

    }

     Vector3 CreateNewTarget()
    {
        if(!enraged){
        int listCount = Random.Range(0,Targets.Count);
        currentTrackedPos = new Vector3(Targets[listCount].position.x,0,Targets[listCount].position.z);
        return currentTrackedPos;}
        else
        {
            return TargetPos;
        }
    }
}
