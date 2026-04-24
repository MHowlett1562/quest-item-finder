using UnityEngine;
using UnityEngine.XR;
using System.Collections;

public class SavedItemExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
    private bool wasRightTriggerPressed;
    private TextMesh saveFeedbackText;
    
    // MVP naming feedback: item name set in the Inspector for testing without a keyboard.
    [SerializeField] private string testItemName = "Keys";

    // Save Placement UX improvement: optional scene reference for right controller ray origin.
    [SerializeField] private Transform rightControllerTransform;
    // Save Placement UX improvement: optional visual marker for current controller hit point.
    [SerializeField] private bool showAimMarker = true;
    [SerializeField] private Material aimMarkerMaterial;
    // Save Placement UX MVP refinement: save point is projected forward from the controller by this distance.
    [SerializeField] private float controllerSaveDistance = 1.0f;
    [SerializeField] private float fallbackDistanceFromCamera = 2f;
    
    private GameObject aimMarker;

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

        UpdateAimMarker(rightTriggerPressed);

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

        // MVP naming feedback: resolve the item name with fallback to "Unnamed Item".
        string resolvedName = string.IsNullOrWhiteSpace(testItemName) ? "Unnamed Item" : testItemName;

        SavedItemData savedItem = new SavedItemData();
        savedItem.itemId = System.Guid.NewGuid().ToString();
        savedItem.itemName = resolvedName;
        savedItem.lastKnownPosition = GetSavePlacementPosition();
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");

        // MVP naming feedback: check for a duplicate name before saving.
        bool isDuplicate = false;
        System.Collections.Generic.List<SavedItemData> existingItems = savedItemManager.GetAllItems();
        for (int i = 0; i < existingItems.Count; i++)
        {
            if (existingItems[i] != null && existingItems[i].itemName == resolvedName)
            {
                isDuplicate = true;
                break;
            }
        }

        savedItemManager.AddItem(savedItem);
        savedItemManager.SaveData();

        // MVP naming feedback: show "Saved duplicate" when the name already existed.
        string feedbackMessage = isDuplicate
            ? "Saved duplicate: " + resolvedName
            : "Saved: " + resolvedName;
        ShowTemporarySaveFeedback(feedbackMessage);

        Debug.Log("Saved item: Name=" + savedItem.itemName + ", Id=" + savedItem.itemId + ", Position=" + savedItem.lastKnownPosition + ", SavedAtUtc=" + savedItem.savedAtUtc);
    }

    private Vector3 GetSavePlacementPosition()
    {
        // Save Placement UX MVP refinement: place the save point directly in front of the assigned right controller.
        if (rightControllerTransform != null)
        {
            return rightControllerTransform.position + (rightControllerTransform.forward * controllerSaveDistance);
        }

        // Save Placement UX MVP refinement: fallback to camera-forward only when no controller transform is assigned.
        if (Camera.main != null)
        {
            return Camera.main.transform.position + (Camera.main.transform.forward * fallbackDistanceFromCamera);
        }

        return transform.position;
    }

    private void UpdateAimMarker(bool rightTriggerPressed)
    {
        if (!showAimMarker)
        {
            return;
        }

        if (aimMarker == null)
        {
            // Save Placement UX improvement: tiny sphere preview for controller hit point.
            aimMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            aimMarker.name = "SaveAimMarker";
            aimMarker.transform.localScale = Vector3.one * 0.05f;

            // Fix for XR stereo rendering / missing material issue.
            if (aimMarkerMaterial != null)
            {
                Renderer markerRenderer = aimMarker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.sharedMaterial = aimMarkerMaterial;
                }
            }

            Collider markerCollider = aimMarker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.enabled = false;
            }
        }

        if (!rightTriggerPressed)
        {
            aimMarker.SetActive(false);
            return;
        }

        // Save Placement UX MVP refinement: preview the same position that will be used for save.
        if (rightControllerTransform != null)
        {
            aimMarker.transform.position = rightControllerTransform.position + (rightControllerTransform.forward * controllerSaveDistance);
            aimMarker.SetActive(true);
            return;
        }

        aimMarker.SetActive(false);
    }

    private void ShowTemporarySaveFeedback(string message)
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
        saveFeedbackText.text = message;
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