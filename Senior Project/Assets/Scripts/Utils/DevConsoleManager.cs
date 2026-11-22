using AYellowpaper.SerializedCollections;
using DavidFDev.DevConsole;
using UnityEngine;

public class DevConsoleManager : Singleton<DevConsoleManager>
{
    [Header("Settings")] 
    [SerializeField] private bool _enabledInBuilds = false;

    [Header("References")] [SerializeField, SerializedDictionary("Item Name (lowercase)", "Prefab")]
    private SerializedDictionary<string, GameObject> _spawnItemPrefabs = new();

    private protected override void Awake()
    {
        base.Awake();
        
        if(_enabledInBuilds)
            DevConsole.EnableConsole();
    }

    [DevConsoleCommand(
        name: "spawn", 
        aliases: "", 
        helpText: "Spawns an item in front of the player"
    )]
    public static void SpawnItem(string itemName)
    {
        if (!Instance._spawnItemPrefabs.TryGetValue(itemName.ToLower(), out GameObject itemPrefab))
        {
            Debug.Log($"No item found with name {itemName}");
            return;
        }
        
        if (itemPrefab == null)
        {
            Debug.LogError("Spawn item Prefab is null");
            return;
        }
            
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
        {
            Debug.LogError("Player not found");
            return;
        }

        Vector3 offset = Vector3.up + player.transform.forward * 0.5f;
        Vector3 spawnPosition = player.transform.position + offset;
        Instantiate(itemPrefab, spawnPosition, Quaternion.identity);
    }
    
    [DevConsoleCommand(
        name: "agitate", 
        aliases: "", 
        helpText: "Force agitates the monster"
    )]
    public static void AgitateMonster()
    {
        MonsterStateActions monster = FindFirstObjectByType<MonsterStateActions>(FindObjectsInactive.Include);
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
        MonsterStateActions monster = FindFirstObjectByType<MonsterStateActions>(FindObjectsInactive.Include);
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
        MonsterStateActions monster = FindFirstObjectByType<MonsterStateActions>(FindObjectsInactive.Include);
        if (monster == null)
        {
            Debug.LogError("Monster not found");
            return;
        }

        monster.Feed();
    }
    
    [DevConsoleCommand(
        name: "enable_monster", 
        aliases: "", 
        helpText: "Enables/Disables the monster",
        parameterHelpText: "true to enable monster, false to disable monster"
    )]
    public static void EnableMonster(bool isEnabled)
    {
        MonsterStateActions monster = FindFirstObjectByType<MonsterStateActions>(FindObjectsInactive.Include);
        if (monster == null)
        {
            Debug.LogError("Monster not found");
            return;
        }

        monster.gameObject.SetActive(isEnabled);
    }
    
    [DevConsoleCommand(
        name: "take_damage", 
        aliases: "", 
        helpText: "Deals 1 damage to player"
    )]
    public static void TakeDamage()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>(FindObjectsInactive.Include);
        if (player == null)
        {
            Debug.LogError("Player not found");
            return;
        }

        if (!player.TryGetComponent(out Health health))
        {
            Debug.LogError("Player health not found");
            return;
        }
        
        health.TakeDamage(1);
    }

    [DevConsoleCommand(
        name: "enable_regen",
        aliases: "",
        helpText: "Enable health regeneration",
        parameterHelpText: "true to enable the health regeneration"
    )]
    public static void EnableRegen(bool isEnabled)
    {
        PlayerHealthRegen healthRegen = FindFirstObjectByType<PlayerHealthRegen>(FindObjectsInactive.Include);
        if (healthRegen == null)
        {
            Debug.LogError("PlayerHealthRegen not found");
            return;
        }
        
        healthRegen.enabled = isEnabled;
    }
    
    [DevConsoleCommand(
        name: "sensitivity",
        aliases: "sens",
        helpText: "Change mouse sensitivity",
        parameterHelpText: "sensitivity value"
    )]
    public static void SetMouseSensitivity(float sensitivity)
    {
        CameraInputController cameraInput = FindFirstObjectByType<CameraInputController>(FindObjectsInactive.Include);
        if (cameraInput == null)
        {
            Debug.LogError("CameraInput not found");
            return;
        }
        
        cameraInput.SetSensitivity(sensitivity);
    }
}