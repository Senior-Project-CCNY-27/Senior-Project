using System;
using AYellowpaper.SerializedCollections;
using Sirenix.OdinInspector;
using UnityEngine;

public class MonsterMeter : MonoBehaviour
{
    public const float MaxMeterValue = 100f;

    public enum Meter
    {
        Curiosity,
        Rage,
        Patience
    }

    [field: SerializeField, SerializedDictionary("Meter", "Value"), ReadOnly]
    public SerializedDictionary<Meter, float> Meters { get; private set; } = new()
    {
        [Meter.Curiosity] = 0f,
        [Meter.Rage] = 0f,
        [Meter.Patience] = 0f
    };

    public event Action<Meter> OnMeterFull = delegate { }; 
    public event Action<Meter, float> OnMeterChanged = delegate { };

    public void AddToMeter(Meter meter, float value)
    {
        if (!Meters.ContainsKey(meter))
            return;

        if (value == 0)
            return;

        if (value > 0f) // Positive
        {
            if (Meters[meter] >= MaxMeterValue)
                return;
            
            Meters[meter] += value;
            if (Meters[meter] >= MaxMeterValue)
            {
                Meters[meter] = MaxMeterValue;
                OnMeterFull?.Invoke(meter);
            }
        }
        else // Negative
        {
            if (Meters[meter] <= 0f)
                return;
            
            Meters[meter] += value;
            if(Meters[meter] <= 0f)
                Meters[meter] = MaxMeterValue;
        }
        
        OnMeterChanged?.Invoke(meter, value);
    }
}