using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
#endif

/// <summary>
/// Automatically pre-generates scene-instance fragments in the editor. Call TriggerDestruction
/// from gameplay code to detach, activate, and animate the generated fragments.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class DestructibleFractureGenerator : MonoBehaviour
{
    private const string HolderName = "FragmentsHolder";

    [SerializeField, MinValue(2), MaxValue(1024), PropertyOrder(-1)]
    private int _fragmentCount = 12;

    [Header("Inside Material")]
    [SerializeField] private bool _useCustomInsideMaterial;
    [SerializeField, ShowIf(nameof(_useCustomInsideMaterial))] private Material _insideMaterial;

    [SerializeField, HideInInspector]
    private GameObject _fragmentsHolder;

    [Header("Config")]
    [SerializeField] private float _explosionForce = 3f;
    [SerializeField] private float _lifeTime = 2f;

    [Header("Fade Away")]
    [SerializeField] private float _fadeAwayDuration = 0.5f;
    [SerializeField] private Ease _fadeAwayEase = Ease.OutCirc;

    private List<Rigidbody> _fragmentRigidBodies = new List<Rigidbody>();
    private bool _destructionTriggered;

#if UNITY_EDITOR
    [ShowInInspector, ReadOnly, LabelText("Fracture Status"), PropertyOrder(-2)]
    private string Status => GetStatusMessage();

    [InfoBox("A missing FragmentsHolder is generated automatically for scene instances. Use Regenerate Fractures to rebuild it after changing the mesh or fragment count.")]
    [Button("Regenerate Fractures", ButtonSizes.Large), EnableIf(nameof(CanGenerateInCurrentContext)), PropertyOrder(0)]
    private void RegenerateFractures()
    {
        GenerateFractures();
    }

    [Button("Trigger Destruction & Destroy Parent", ButtonSizes.Large), EnableIf(nameof(CanTriggerFromInspector)), PropertyOrder(1)]
    private void TriggerDestructionFromInspector()
    {
        TriggerDestruction(transform.position);
    }

    private bool CanTriggerFromInspector => Application.isPlaying && FindHolder() != null;

    private void OnEnable()
    {
        QueueAutomaticGeneration();
    }

    private void OnDisable()
    {
        EditorApplication.delayCall -= TryGenerateOnScenePlacement;
    }

    private void QueueAutomaticGeneration()
    {
        EditorApplication.delayCall -= TryGenerateOnScenePlacement;
        EditorApplication.delayCall += TryGenerateOnScenePlacement;
    }

    private void TryGenerateOnScenePlacement()
    {
        if (this == null || !isActiveAndEnabled || Application.isPlaying || !CanGenerateInCurrentContext)
        {
            return;
        }

        _fragmentsHolder = FindHolder();
        if (_fragmentsHolder == null)
        {
            GenerateFractures();
        }
        else
        {
            RefreshFragmentRigidBodies();
        }
    }

    private bool CanGenerateInCurrentContext =>
        !Application.isPlaying && string.IsNullOrEmpty(GetValidationError());

    private string GetStatusMessage()
    {
        string error = GetValidationError();
        if (!string.IsNullOrEmpty(error))
        {
            return $"{name}: {error}";
        }

        _fragmentsHolder = FindHolder();
        return _fragmentsHolder != null
            ? $"{name}: FragmentsHolder is ready."
            : $"{name}: Fragments will generate when this object is placed in a scene.";
    }

    private string GetValidationError()
    {
        if (Application.isPlaying)
        {
            return "Fracture generation and regeneration are editor-only.";
        }

        if (EditorUtility.IsPersistent(gameObject))
        {
            return "Place this prefab in a scene to generate its fractures.";
        }

        if (PrefabStageUtility.GetPrefabStage(gameObject) != null)
        {
            return "Fractures generate on scene instances, not while editing a prefab.";
        }

        if (!gameObject.scene.IsValid() || !gameObject.scene.isLoaded)
        {
            return "This object must be part of a loaded scene.";
        }

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || GetComponent<MeshRenderer>() == null)
        {
            return "Add a MeshFilter and MeshRenderer to this GameObject.";
        }

        if (meshFilter.sharedMesh == null)
        {
            return "The MeshFilter has no mesh.";
        }

        if (!meshFilter.sharedMesh.isReadable)
        {
            return "Enable Read/Write in the mesh import settings.";
        }

        return null;
    }

    private void GenerateFractures()
    {
        if (!CanGenerateInCurrentContext)
        {
            return;
        }

        MeshFilter sourceFilter = GetComponent<MeshFilter>();
        MeshRenderer sourceRenderer = GetComponent<MeshRenderer>();
        Material cutFaceMaterial = _useCustomInsideMaterial && _insideMaterial != null
            ? _insideMaterial
            : sourceRenderer.sharedMaterial;
        if (sourceRenderer.sharedMaterials.Length > 1)
        {
            Debug.LogWarning(
                "OpenFracture supports one source submesh. Additional source submeshes may not be preserved.",
                gameObject);
        }

        GameObject temporarySource = null;
        Transform generatedRoot = null;
        GameObject registeredHolder = null;
        GameObject previousHolder = FindHolder();
        bool holderRegistered = false;

        try
        {
            Transform parent = transform.parent;
            temporarySource = new GameObject(name + "_FractureSource_" + Guid.NewGuid().ToString("N"));
            if (gameObject.scene.IsValid() && gameObject.scene.isLoaded && temporarySource.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(temporarySource, gameObject.scene);
            }

            if (parent != null)
            {
                temporarySource.transform.SetParent(parent, false);
            }
            temporarySource.transform.localPosition = transform.localPosition;
            temporarySource.transform.localRotation = transform.localRotation;
            temporarySource.transform.localScale = transform.localScale;

            temporarySource.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
            temporarySource.AddComponent<MeshRenderer>().sharedMaterials = sourceRenderer.sharedMaterials;
            temporarySource.AddComponent<MeshCollider>().sharedMesh = sourceFilter.sharedMesh;
            temporarySource.AddComponent<Rigidbody>().isKinematic = true;

            Prefracture prefracture = temporarySource.AddComponent<Prefracture>();
            prefracture.triggerOptions = new TriggerOptions();
            prefracture.fractureOptions = new FractureOptions
            {
                fragmentCount = _fragmentCount,
                xAxis = true,
                yAxis = true,
                zAxis = true,
                asynchronous = false,
                detectFloatingFragments = false,
                insideMaterial = cutFaceMaterial
            };
            prefracture.prefractureOptions = new PrefractureOptions
            {
                unfreezeAll = true,
                saveFragmentsToDisk = false,
                saveLocation = string.Empty
            };
            prefracture.callbackOptions = new CallbackOptions { onCompleted = new UnityEvent() };

            Scene previousActiveScene = SceneManager.GetActiveScene();
            bool restoreActiveScene = gameObject.scene.IsValid() && gameObject.scene.isLoaded &&
                                      previousActiveScene != gameObject.scene &&
                                      SceneManager.SetActiveScene(gameObject.scene);
            try
            {
                prefracture.ComputeFracture();
            }
            finally
            {
                if (restoreActiveScene && previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
            }

            generatedRoot = FindGeneratedRoot(temporarySource.scene, parent, temporarySource.name + "Fragments");
            if (generatedRoot == null || generatedRoot.childCount == 0)
            {
                throw new InvalidOperationException("OpenFracture did not generate any fragments for this mesh.");
            }

            if (generatedRoot.parent == null && generatedRoot.gameObject.scene != gameObject.scene &&
                gameObject.scene.IsValid() && gameObject.scene.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(generatedRoot.gameObject, gameObject.scene);
            }

            registeredHolder = generatedRoot.gameObject;
            Undo.RegisterCreatedObjectUndo(registeredHolder, "Generate Fractures");
            holderRegistered = true;
            generatedRoot.name = HolderName;
            generatedRoot.SetParent(transform, true);

            ApplyFragmentSettings(generatedRoot, sourceRenderer.sharedMaterials, cutFaceMaterial);
            generatedRoot.gameObject.SetActive(false);

            if (previousHolder != null && previousHolder != registeredHolder)
            {
                Undo.DestroyObjectImmediate(previousHolder);
            }

            Undo.RecordObject(this, "Assign Fragments Holder");
            _fragmentsHolder = registeredHolder;
            _destructionTriggered = false;
            RefreshFragmentRigidBodies();

            EditorSceneManager.MarkSceneDirty(gameObject.scene);
            EditorUtility.SetDirty(gameObject);
            EditorUtility.SetDirty(this);
            Debug.Log($"Generated {generatedRoot.childCount} disabled fragments under {HolderName}.", gameObject);
        }
        catch (Exception exception)
        {
            if (generatedRoot == null && temporarySource != null)
            {
                generatedRoot = FindGeneratedRoot(
                    temporarySource.scene,
                    transform.parent,
                    temporarySource.name + "Fragments");
            }

            if (holderRegistered && registeredHolder != null)
            {
                Undo.DestroyObjectImmediate(registeredHolder);
            }
            else if (generatedRoot != null)
            {
                DestroyImmediate(generatedRoot.gameObject);
            }

            Debug.LogException(exception, this);
        }
        finally
        {
            if (temporarySource != null)
            {
                DestroyImmediate(temporarySource);
            }
        }
    }
#endif

    private GameObject FindHolder()
    {
        Transform child = transform.Find(HolderName);
        return child != null ? child.gameObject : _fragmentsHolder;
    }

    private void RefreshFragmentRigidBodies()
    {
        if (_fragmentsHolder == null)
        {
            _fragmentsHolder = FindHolder();
        }

        _fragmentRigidBodies = _fragmentsHolder != null
            ? _fragmentsHolder.GetComponentsInChildren<Rigidbody>(true).ToList()
            : new List<Rigidbody>();
    }

#if UNITY_EDITOR
    private static Transform FindGeneratedRoot(Scene scene, Transform parent, string rootName)
    {
        if (parent != null)
        {
            Transform child = parent.Find(rootName);
            if (child != null)
            {
                return child;
            }
        }

        if (scene.IsValid())
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == rootName)
                {
                    return rootObject.transform;
                }
            }
        }

        foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!EditorUtility.IsPersistent(candidate) && candidate.name == rootName && candidate.scene.IsValid())
            {
                return candidate.transform;
            }
        }

        return null;
    }

    private static void ApplyFragmentSettings(Transform holder, Material[] sourceMaterials, Material insideMaterial)
    {
        Material[] fragmentMaterials = new Material[Mathf.Max(2, sourceMaterials.Length)];
        if (sourceMaterials.Length > 0)
        {
            Array.Copy(sourceMaterials, fragmentMaterials, Mathf.Min(sourceMaterials.Length, fragmentMaterials.Length));
        }
        fragmentMaterials[1] = insideMaterial;

        for (int i = 0; i < holder.childCount; i++)
        {
            Transform fragment = holder.GetChild(i);
            MeshRenderer renderer = fragment.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterials = fragmentMaterials;
            }

            Rigidbody body = fragment.GetComponent<Rigidbody>();
            MeshCollider meshCollider = fragment.GetComponent<MeshCollider>();
            if (meshCollider != null)
            {
                if (body != null)
                {
                    body.isKinematic = true;
                }
                meshCollider.convex = false;
            }
        }
    }
#endif

    /// <summary>
    /// Activates and detaches the pre-generated fragments, applies an outward impulse, then fades them out.
    /// Call this from gameplay code when the object should break apart.
    /// </summary>
    public void TriggerDestruction(Vector3 hitPoint)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("TriggerDestruction can only be called while the game is running.", this);
            return;
        }

        if (_destructionTriggered)
        {
            return;
        }

        if (_fragmentsHolder == null)
        {
            _fragmentsHolder = FindHolder();
        }

        if (_fragmentsHolder == null)
        {
            Debug.LogWarning($"{name} has no generated {HolderName}.", this);
            return;
        }

        RefreshFragmentRigidBodies();
        _destructionTriggered = true;

        _fragmentsHolder.transform.SetParent(null, true);
        _fragmentsHolder.SetActive(true);

        foreach (Rigidbody rigidBody in _fragmentRigidBodies)
        {
            if (rigidBody == null)
            {
                continue;
            }

            foreach (MeshCollider meshCollider in rigidBody.GetComponents<MeshCollider>())
            {
                meshCollider.convex = true;
            }

            rigidBody.constraints = RigidbodyConstraints.None;
            rigidBody.isKinematic = false;

            Vector3 direction = rigidBody.worldCenterOfMass - hitPoint;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = UnityEngine.Random.onUnitSphere;
            }

            rigidBody.AddForceAtPosition(
                direction.normalized * _explosionForce,
                _fragmentsHolder.transform.position,
                ForceMode.Impulse);
        }

        GameObject holderToFade = _fragmentsHolder;
        List<Rigidbody> fragmentsToFade = new List<Rigidbody>(_fragmentRigidBodies);
        float fadeDuration = _fadeAwayDuration;
        Ease fadeEase = _fadeAwayEase;
        DOVirtual.DelayedCall(
            Mathf.Max(0f, _lifeTime),
            () => FadeAway(holderToFade, fragmentsToFade, fadeDuration, fadeEase));

        Destroy(gameObject);
    }

    private static void FadeAway(GameObject holder, List<Rigidbody> fragmentRigidBodies, float fadeDuration, Ease fadeEase)
    {
        if (holder == null)
        {
            return;
        }

        if (fadeDuration <= 0f)
        {
            Destroy(holder);
            return;
        }

        Sequence sequence = DOTween.Sequence();
        int tweenCount = 0;
        foreach (Rigidbody rigidBody in fragmentRigidBodies)
        {
            if (rigidBody == null)
            {
                continue;
            }

            sequence.Join(rigidBody.transform.DOScale(0f, fadeDuration).SetEase(fadeEase));
            tweenCount++;
        }

        sequence.OnComplete(() =>
        {
            if (holder != null)
                Destroy(holder);
        });

        if (tweenCount == 0)
        {
            sequence.Complete();
        }
    }

    private void OnValidate()
    {
        _fragmentCount = Mathf.Clamp(_fragmentCount, 2, 1024);
        _explosionForce = Mathf.Max(0f, _explosionForce);
        _lifeTime = Mathf.Max(0f, _lifeTime);
        _fadeAwayDuration = Mathf.Max(0f, _fadeAwayDuration);

#if UNITY_EDITOR
        RefreshFragmentRigidBodies();
#endif
    }
}
