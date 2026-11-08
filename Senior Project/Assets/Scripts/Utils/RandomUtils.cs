using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Random choices and chance-based actions. Probabilities are in the range 0 to 1.
/// </summary>
public static class RandomUtils
{
    /// <summary>
    /// Returns true with the given probability (0 = never, 1 = always).
    /// </summary>
    public static bool Chance(float probability)
    {
        if (float.IsNaN(probability) || probability < 0f || probability > 1f)
            throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1.");

        return probability >= 1f || (probability > 0f && UnityEngine.Random.value < probability);
    }

    /// <summary>
    /// Runs the action for the selected chance branch and returns whether it succeeded.
    /// </summary>
    public static bool RunChance(float probability, Action onSuccess, Action onFailure = null)
    {
        bool succeeded = Chance(probability);
        if (succeeded)
            onSuccess?.Invoke();
        else
            onFailure?.Invoke();

        return succeeded;
    }

    /// <summary>
    /// A relative weight and the action to run when this branch is selected.
    /// </summary>
    public struct ChanceBranch
    {
        public float Weight;
        public Action Action;

        public ChanceBranch(float weight, Action action)
        {
            Weight = weight;
            Action = action;
        }
    }

    /// <summary>
    /// Selects exactly one branch by relative weight, runs its action, and returns its index.
    /// A zero-weight branch is never selected. For example, weights 3 and 1 mean 75% and 25%.
    /// </summary>
    public static int RunWeighted(params ChanceBranch[] branches)
    {
        if (branches == null)
            throw new ArgumentNullException(nameof(branches));
        if (branches.Length == 0)
            throw new ArgumentException("At least one branch is required.", nameof(branches));

        double totalWeight = 0;
        for (int i = 0; i < branches.Length; i++)
        {
            float weight = branches[i].Weight;
            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
                throw new ArgumentOutOfRangeException(nameof(branches), "Branch weights must be finite and non-negative.");
            totalWeight += weight;
        }

        if (totalWeight <= 0)
            throw new ArgumentException("At least one branch must have a positive weight.", nameof(branches));

        double roll = UnityEngine.Random.value * totalWeight;
        double cumulativeWeight = 0;
        int lastPositiveIndex = -1;
        for (int i = 0; i < branches.Length; i++)
        {
            if (branches[i].Weight <= 0f)
                continue;

            lastPositiveIndex = i;
            cumulativeWeight += branches[i].Weight;
            if (roll < cumulativeWeight)
            {
                branches[i].Action?.Invoke();
                return i;
            }
        }

        // Handles a roll at the upper endpoint and floating-point rounding.
        branches[lastPositiveIndex].Action?.Invoke();
        return lastPositiveIndex;
    }

    /// <summary>
    /// Returns one uniformly selected item from a non-empty list.
    /// </summary>
    public static T Choose<T>(IReadOnlyList<T> items)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (items.Count == 0)
            throw new ArgumentException("At least one item is required.", nameof(items));

        return items[UnityEngine.Random.Range(0, items.Count)];
    }
}
