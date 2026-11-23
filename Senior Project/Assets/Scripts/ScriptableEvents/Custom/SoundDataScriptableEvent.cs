using UnityEngine;

[System.Serializable]
public class SoundData
{
    public float Radius = 10f;
    public Vector3 Position;
    public GameObject Source;
    public float Volume = 100f;
    public bool IsSpatial = true;

    public override string ToString()
    {
        return $"SoundData from Source: {Source.name}, Position: {Position}, Radius: {Radius}, Volume: {Volume}, Is Spatial: {IsSpatial}";
    }
}

[CreateAssetMenu(fileName = "SoundData Event", menuName = "Scriptable Events/SoundData")]
public class SoundDataScriptableEvent : ScriptableEvent<SoundData>
{
    
}