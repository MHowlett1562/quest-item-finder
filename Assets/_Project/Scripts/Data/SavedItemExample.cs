using UnityEngine;
using UnityEngine.XR;

public class SavedItemExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
    private bool wasRightTriggerPressed;

    private void Start()
    {
        savedItemManager = FindFirstObjectByType<SavedItemManager>();

        if (savedItemManager != null)
        {
            savedItemManager.LoadData();
        }
        else
        {
            Debug.Log("No SavedItemManager found in the scene.");
        }
    }

    private void Update()
    {
        bool rightTriggerPressed = false;
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.isValid)
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerPressed);
        }

        bool rightTriggerPressedThisFrame = rightTriggerPressed && !wasRightTriggerPressed;
        wasRightTriggerPressed = rightTriggerPressed;

        if (!Input.GetKeyDown(KeyCode.K) && !rightTriggerPressedThisFrame)
        {
            return;
        }

        if (savedItemManager == null)
        {
            Debug.Log("No SavedItemManager found in the scene.");
            return;
        }

        SavedItemData savedItem = new SavedItemData();
        savedItem.itemId = System.Guid.NewGuid().ToString();
        savedItem.itemName = "Keys";
        if (Camera.main != null)
        {
            savedItem.lastKnownPosition = Camera.main.transform.position + (Camera.main.transform.forward * 4f);
        }
        else
        {
            savedItem.lastKnownPosition = transform.position;
        }
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");

        savedItemManager.AddItem(savedItem);
        savedItemManager.SaveData();

        Debug.Log("Saved item: Name=" + savedItem.itemName + ", Id=" + savedItem.itemId + ", Position=" + savedItem.lastKnownPosition + ", SavedAtUtc=" + savedItem.savedAtUtc);
    }
}