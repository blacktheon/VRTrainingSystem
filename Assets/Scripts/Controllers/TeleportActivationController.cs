using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TeleportActivationController : MonoBehaviour
{
    [SerializeField] private InputActionProperty _teleportActivateAction;
    [SerializeField] private XRRayInteractor _teleportInteractor;

    private void Start()
    {
        _teleportInteractor.gameObject.SetActive(false);

        _teleportActivateAction.action.performed += ActionPerformed;
    }

    private void Update()
    {
        if (_teleportActivateAction.action.WasReleasedThisFrame())
        {
            _teleportInteractor.gameObject.SetActive(false);
        }
    }

    private void ActionPerformed(InputAction.CallbackContext context)
    {
        _teleportInteractor.gameObject.SetActive(true);
    }
}
