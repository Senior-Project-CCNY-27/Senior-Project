using DavidFDev.DevConsole;
using UnityEngine;

public class DevConsoleManager : Singleton<DevConsoleManager>
{
    [Header("Settings")] 
    [SerializeField] private bool _enabledInBuilds = false;
    
    [Header("References")]
    [SerializeField] private Pickup _spawnFoodPrefab;

    private protected override void Awake()
    {
        base.Awake();
        
        if(_enabledInBuilds)
            DevConsole.EnableConsole();
    }

    [DevConsoleCommand(
        name: "spawnfood", 
        aliases: "", 
        helpText: "Spawns food on the player"
    )]
    public static void SpawnItem()
    {
        if (Instance._spawnFoodPrefab == null)
        {
            Debug.LogError("Spawn Food Prefab is null");
            return;
        }
            
        PlayerController player = FindObjectOfType<PlayerController>();
        if (player == null)
        {
            Debug.LogError("Player not found");
            return;
        }

        Vector3 offset = Vector3.up + player.transform.forward * 0.5f;
        Vector3 spawnPosition = player.transform.position + offset;
        Instantiate(Instance._spawnFoodPrefab, spawnPosition, Quaternion.identity);
    }
    
    [DevConsoleCommand(
        name: "agitate", 
        aliases: "", 
        helpText: "Force agitates the monster"
    )]
    public static void AgitateMonster()
    {
        MonsterStateActions monster = FindObjectOfType<MonsterStateActions>();
        if (monster == null)
        {
            Debug.LogError("Monster not found");
            return;
        }

        monster.ForceAgitated();
    }
    
    [DevConsoleCommand(
        name: "aggress", 
        aliases: "", 
        helpText: "Force aggresses the monster"
    )]
    public static void AggressMonster()
    {
        MonsterStateActions monster = FindObjectOfType<MonsterStateActions>();
        if (monster == null)
        {
            Debug.LogError("Monster not found");
            return;
        }

        monster.ForceAggressive();
    }
    
    [DevConsoleCommand(
        name: "feed", 
        aliases: "", 
        helpText: "Force feeds the monster"
    )]
    public static void FeedMonster()
    {
        MonsterStateActions monster = FindObjectOfType<MonsterStateActions>();
        if (monster == null)
        {
            Debug.LogError("Monster not found");
            return;
        }

        monster.Feed();
    }
}