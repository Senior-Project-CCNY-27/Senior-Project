using System;
using NaughtyAttributes;
using TMPro;
using UnityEngine;

public class NearbyInteractableUI : MonoBehaviour
{
    [SerializeField, Required] private NearbyInteractableDetector _detector;
    [SerializeField, Required] private GameObject _container;
    [SerializeField, Required] private TMP_Text _nameText;

    private void Start()
    {
        _container.SetActive(false);
    }

    private void OnEnable()
    {
        _detector.OnInteractableDetected.AddListener(OnInteractableDetected);
    }

    private void OnDisable()
    {
        _detector.OnInteractableDetected.RemoveListener(OnInteractableDetected);
    }

    private void LateUpdate()
    {
        UpdateContainerScreenPosition();
    }

    private void OnInteractableDetected(Interactable interactable)
    {
        if (interactable == null)
        {
            _container.SetActive(false);
            return;
        }

        _nameText.text = interactable.DisplayName;
        _container.SetActive(true);
    }

    private void UpdateContainerScreenPosition()
    {
        if (_detector.CurrentInteractable == null)
            return;
        
        Vector3 screenPosition = Camera.main.WorldToScreenPoint(_detector.CurrentInteractable.transform.position);
        _container.transform.position = screenPosition;
    }
}