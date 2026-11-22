using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

public class PickupUseUI : MonoBehaviour
{
    private PlayerPickupController _pickupController;
    
    [SerializeField, Required] private TMP_Text _text;
    [SerializeField] private string _cantUseText = "Can't Use";
    [SerializeField] private string _useText = "E to Use";
    
    private void Awake()
    {
        _pickupController = FindAnyObjectByType<PlayerPickupController>();
    }

    private void Update()
    {
        if (_pickupController.IsHandEmpty)
        {
            _text.gameObject.SetActive(false);
            return;
        }

        if (!_pickupController.HeldPickupItem.Usable)
        {
            _text.gameObject.SetActive(false);
            return;
        }
        
        if(!_pickupController.HeldPickupItem.CanUseAfterValidation(_pickupController))
        {
            _text.gameObject.SetActive(true);
            _text.text = _cantUseText;
            return;
        }
        
        _text.gameObject.SetActive(true);
        _text.text = _useText;
    }
}