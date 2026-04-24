using UnityEngine;
using UnityEngine.XR;
using System.Collections;

public class SavedItemExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
    private bool wasRightTriggerPressed;
    private bool wasRightPrimaryButtonPressed;
    private bool wasRightSecondaryButtonPressed;
    private TextMesh saveFeedbackText;
    private LineRenderer controllerRayLine;

    // MVP input conflict cleanup: expose whether save-name menu is open so other scripts can gate shared inputs.
    public bool IsNameSelectionMenuOpen => isNameSelectionMenuOpen;
    private TextMesh nameSelectionMenuText;
    // MVP preset name selection UI: true while preset-name menu is shown.
    private bool isNameSelectionMenuOpen;
    // MVP preset name selection UI: currently selected preset index in the name menu.
    private int selectedPresetNameIndex;
    
    // MVP naming feedback: item name set in the Inspector for testing without a keyboard.
    [SerializeField] private string testItemName = "Keys";
    // MVP preset name selection UI: simple preset names for headset-friendly item naming.
    [SerializeField] private string[] presetItemNames = { "Keys", "Wallet", "Remote" };

    // Save Placement UX improvement: optional scene reference for right controller ray origin.
    [SerializeField] private Transform rightControllerTransform;
    // Save Placement UX improvement: optional visual marker for current controller hit point.
    [SerializeField] private bool showAimMarker = true;
    [SerializeField] private Material aimMarkerMaterial;
    // Save Placement UX MVP refinement: save point is projected forward from the controller by this distance.
    [SerializeField] private float controllerSaveDistance = 1.0f;
    [SerializeField] private float fallbackDistanceFromCamera = 2f;
    [SerializeField] private float controllerRayWidth = 0.0075f;
    private static readonly Vector3 nameSelectionMenuLocalOffset = new Vector3(0f, 0.06f, 1.2f);
    
    private GameObject aimMarker;

    private void Start()
    {
        savedItemManager = FindFirstObjectByType<SavedItemManager>();

		if (savedItemFinderExample == null)
		{
			savedItemFinderExample = FindFirstObjectByType<SavedItemFinderExample>();
		}

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
        bool rightPrimaryButtonPressed = false;
        bool rightSecondaryButtonPressed = false;
        InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightHandDevice.isValid)
        {
            rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerPressed);
            rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimaryButtonPressed);
            rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out rightSecondaryButtonPressed);
        }

        UpdateAimMarker(rightTriggerPressed);
		UpdateControllerRayVisual();

        bool rightTriggerPressedThisFrame = rightTriggerPressed && !wasRightTriggerPressed;
        bool rightPrimaryButtonPressedThisFrame = rightPrimaryButtonPressed && !wasRightPrimaryButtonPressed;
        bool rightSecondaryButtonPressedThisFrame = rightSecondaryButtonPressed && !wasRightSecondaryButtonPressed;

        wasRightTriggerPressed = rightTriggerPressed;
        wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;
        wasRightSecondaryButtonPressed = rightSecondaryButtonPressed;

        if (isNameSelectionMenuOpen)
        {
            UpdateNameSelectionMenuTransform();
            HandleNameSelectionMenuCyclingInput(rightPrimaryButtonPressedThisFrame, rightSecondaryButtonPressedThisFrame);
        }

        bool saveInputPressedThisFrame = Input.GetKeyDown(KeyCode.K) || rightTriggerPressedThisFrame;

        if (!saveInputPressedThisFrame)
        {
            return;
        }

        if (savedItemManager == null)
        {
            Debug.Log("No SavedItemManager found in the scene.");
            return;
        }

        if (saveInputPressedThisFrame && !isNameSelectionMenuOpen)
        {
            // Symmetric UI state conflict cleanup: close find UI before opening save UI.
            if (savedItemFinderExample != null && (savedItemFinderExample.IsFindItemSelectionMenuOpen || savedItemFinderExample.IsFindModeActive))
            {
                savedItemFinderExample.CancelFindModeAndMenu();
            }

            ShowNameSelectionMenu();
            return;
        }

        if (!isNameSelectionMenuOpen)
        {
            ShowNameSelectionMenu();
            return;
        }

        string resolvedName = GetSelectedNameForSave();
        HideNameSelectionMenu();

        // MVP naming feedback: resolve the item name with fallback to "Unnamed Item".
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

    private void EnsureNameSelectionMenuText()
    {
        if (nameSelectionMenuText != null)
        {
            return;
        }

        GameObject nameSelectionMenuObject = new GameObject("SaveNameSelectionMenuText");
        nameSelectionMenuText = nameSelectionMenuObject.AddComponent<TextMesh>();
        nameSelectionMenuText.fontSize = 56;
        nameSelectionMenuText.characterSize = 0.01f;
        nameSelectionMenuText.anchor = TextAnchor.MiddleCenter;
        nameSelectionMenuText.alignment = TextAlignment.Center;
        nameSelectionMenuText.color = Color.white;
        nameSelectionMenuText.gameObject.SetActive(false);
        UpdateNameSelectionMenuTransform();
    }

    private void UpdateNameSelectionMenuTransform()
    {
        if (nameSelectionMenuText == null || Camera.main == null)
        {
            return;
        }

        if (nameSelectionMenuText.transform.parent != Camera.main.transform)
        {
            nameSelectionMenuText.transform.SetParent(Camera.main.transform, false);
        }

        nameSelectionMenuText.transform.localPosition = nameSelectionMenuLocalOffset;
        nameSelectionMenuText.transform.localRotation = Quaternion.identity;
    }

    private void ShowNameSelectionMenu()
    {
        // MVP preset name selection UI: open simple headset menu before save instead of typing a name.
        isNameSelectionMenuOpen = true;
        selectedPresetNameIndex = 0;

        EnsureNameSelectionMenuText();
        UpdateNameSelectionMenuText();
        nameSelectionMenuText.gameObject.SetActive(true);
    }

    private void HideNameSelectionMenu()
    {
        isNameSelectionMenuOpen = false;

        if (nameSelectionMenuText != null)
        {
            nameSelectionMenuText.gameObject.SetActive(false);
        }

        if (aimMarker != null)
        {
            aimMarker.SetActive(false);
        }

        DisableControllerRayVisual();
    }

    public void CancelNameSelectionMenu()
    {
        // UI state conflict cleanup: close save UI visuals without saving anything.
        HideNameSelectionMenu();
    }

    private void HandleNameSelectionMenuCyclingInput(bool rightPrimaryButtonPressedThisFrame, bool rightSecondaryButtonPressedThisFrame)
    {
        if (!isNameSelectionMenuOpen || presetItemNames == null || presetItemNames.Length == 0)
        {
            return;
        }

        // MVP preset name selection UI: cycle presets with controller A/B and keyboard fallback.
        if (rightPrimaryButtonPressedThisFrame || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
        {
            selectedPresetNameIndex++;
            if (selectedPresetNameIndex >= presetItemNames.Length)
            {
                selectedPresetNameIndex = 0;
            }

            UpdateNameSelectionMenuText();
        }

        if (rightSecondaryButtonPressedThisFrame || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
        {
            selectedPresetNameIndex--;
            if (selectedPresetNameIndex < 0)
            {
                selectedPresetNameIndex = presetItemNames.Length - 1;
            }

            UpdateNameSelectionMenuText();
        }
    }

    private void UpdateNameSelectionMenuText()
    {
        if (nameSelectionMenuText == null)
        {
            return;
        }

        nameSelectionMenuText.text = "Name item\n" + GetSelectedNameForSave() + "\nA/B to cycle\nRight Trigger to save";
    }

    private string GetSelectedNameForSave()
    {
        if (presetItemNames != null && presetItemNames.Length > 0)
        {
            if (selectedPresetNameIndex < 0 || selectedPresetNameIndex >= presetItemNames.Length)
            {
                selectedPresetNameIndex = 0;
            }

            string selectedPresetName = presetItemNames[selectedPresetNameIndex];
            if (!string.IsNullOrWhiteSpace(selectedPresetName))
            {
                return selectedPresetName;
            }
        }

        // Keep Inspector fallback for keyboard/debug usage when presets are unavailable.
        if (!string.IsNullOrWhiteSpace(testItemName))
        {
            return testItemName;
        }

        return "Unnamed Item";
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
            DisableControllerRayVisual();
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

        bool shouldShowAimMarker = isNameSelectionMenuOpen || rightTriggerPressed;
        if (!shouldShowAimMarker)
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

    private void EnsureControllerRayLine()
    {
        if (controllerRayLine != null)
        {
            return;
        }

        // Controller ray visual polish: runtime line renderer for save aiming.
        GameObject controllerRayObject = new GameObject("SaveControllerRay");
        controllerRayLine = controllerRayObject.AddComponent<LineRenderer>();
        controllerRayLine.positionCount = 2;
        controllerRayLine.startWidth = controllerRayWidth;
        controllerRayLine.endWidth = controllerRayWidth;
        controllerRayLine.useWorldSpace = true;
        controllerRayLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        controllerRayLine.receiveShadows = false;
        controllerRayLine.numCapVertices = 4;
        controllerRayLine.startColor = Color.cyan;
        controllerRayLine.endColor = Color.cyan;

        Shader rayShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (rayShader == null)
        {
            rayShader = Shader.Find("Unlit/Color");
        }
        if (rayShader == null)
        {
            rayShader = Shader.Find("Sprites/Default");
        }
        if (rayShader == null)
        {
            rayShader = Shader.Find("Standard");
        }

        if (rayShader != null)
        {
            Material rayMaterial = new Material(rayShader);
            rayMaterial.color = Color.cyan;
            controllerRayLine.sharedMaterial = rayMaterial;
        }

        controllerRayLine.enabled = false;
    }

    private void UpdateControllerRayVisual()
    {
        bool shouldShowRay = isNameSelectionMenuOpen && showAimMarker && rightControllerTransform != null;
        if (!shouldShowRay)
        {
            DisableControllerRayVisual();
            return;
        }

        EnsureControllerRayLine();

        Vector3 rayStart = rightControllerTransform.position;
        Vector3 rayDirection = rightControllerTransform.forward;
        Vector3 rayEnd = rayStart + (rayDirection * controllerSaveDistance);

        // Controller ray visual polish: use a simple Physics raycast for the visible end point only.
        RaycastHit hit;
        if (Physics.Raycast(rayStart, rayDirection, out hit, controllerSaveDistance))
        {
            rayEnd = hit.point;
        }

        controllerRayLine.enabled = true;
        controllerRayLine.SetPosition(0, rayStart);
        controllerRayLine.SetPosition(1, rayEnd);
    }

    private void DisableControllerRayVisual()
    {
        if (controllerRayLine != null)
        {
            controllerRayLine.enabled = false;
        }
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