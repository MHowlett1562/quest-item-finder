using UnityEngine;
using UnityEngine.XR;
using System.Collections;

public class SavedItemExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
    private bool wasRightTriggerPressed;
    private TextMesh saveFeedbackText;

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

        ShowTemporarySaveFeedback();

        Debug.Log("Saved item: Name=" + savedItem.itemName + ", Id=" + savedItem.itemId + ", Position=" + savedItem.lastKnownPosition + ", SavedAtUtc=" + savedItem.savedAtUtc);
    }

    private void ShowTemporarySaveFeedback()
    {
        if (Camera.main == null)
        {
            return;
        }

        // Temporary save feedback UX: show a short floating message after save.
        if (saveFeedbackText == null)
        {
            GameObject saveFeedbackObject = new GameObject("SaveFeedbackText");
            saveFeedbackText = saveFeedbackObject.AddComponent<TextMesh>();
            saveFeedbackText.fontSize = 64;
            saveFeedbackText.characterSize = 0.01f;
            saveFeedbackText.anchor = TextAnchor.MiddleCenter;
            saveFeedbackText.alignment = TextAlignment.Center;
            saveFeedbackText.color = Color.white;
        }

        saveFeedbackText.transform.SetParent(Camera.main.transform, false);
        saveFeedbackText.transform.localPosition = new Vector3(0f, -0.1f, 1.2f);
        saveFeedbackText.transform.localRotation = Quaternion.identity;
        saveFeedbackText.text = "Item Saved";
        saveFeedbackText.gameObject.SetActive(true);

        StopCoroutine("HideSaveFeedbackAfterDelay");
        StartCoroutine("HideSaveFeedbackAfterDelay");
    }

    private IEnumerator HideSaveFeedbackAfterDelay()
    {
        // Temporary save feedback UX: auto-hide after a short delay.
        yield return new WaitForSeconds(2f);

        if (saveFeedbackText != null)
        {
            saveFeedbackText.gameObject.SetActive(false);
        }
    }
}