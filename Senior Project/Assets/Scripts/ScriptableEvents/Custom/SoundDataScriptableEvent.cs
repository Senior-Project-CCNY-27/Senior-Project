using UnityEngine;

[System.Serializable]
public class SoundData
{
    public float Radius;
    public Vector3 Position;
    public GameObject Source;
    public float Volume;
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