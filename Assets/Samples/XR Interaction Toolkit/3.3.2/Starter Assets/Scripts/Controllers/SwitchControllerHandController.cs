using UnityEngine;
using UnityEngine.InputSystem;

public class SwitchControllerHandController : MonoBehaviour
{
    [SerializeField] private InputActionProperty _leftControllerTracking;
    [SerializeField] private InputActionProperty _rightControllerTracking;

    [SerializeField] private GameObject _leftController;
    [SerializeField] private GameObject _rightController;
    [SerializeField] private GameObject _leftHand;
    [SerializeField] private GameObject _rightHand;

    public bool isHandTracking = false;


    private void Start()
    {
        TurnOnControllers();
    }

    private void TurnOnControllers()
    {
        if (isHandTracking)
        {
            isHandTracking = false;
            _leftController.SetActive(true);
            _rightController.SetActive(true);
            _leftHand.SetActive(false);
            _rightHand.SetActive(false);
        }
    }

    private void TurnOnHands()
    {
        if (!isHandTracking) 
        {
            isHandTracking = true;
            _leftController.SetActive(false);
            _rightController.SetActive(false);
            _leftHand.SetActive(true);
            _rightHand.SetActive(true);
        }
    }

    private void Update()
    {
        int value = _leftControllerTracking.action.ReadValue<int>();
        int value2 = _rightControllerTracking.action.ReadValue<int>();

        if (value == 0 && value2 == 0 && !isHandTracking)
        {
            TurnOnHands();
        }

        if (value != 0 || value2 != 0 && isHandTracking)
        {
            TurnOnControllers();
        }
    }
}
