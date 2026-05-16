using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using UnityEngine.XR.ARFoundation;

public class SavedItemExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
    [SerializeField] private SceneUnderstandingTest sceneUnderstandingTest;
    [SerializeField] private ARAnchorManager arAnchorManager;
    private bool wasRightTriggerPressed;
    private bool wasRightPrimaryButtonPressed;
    private bool wasRightSecondaryButtonPressed;
    private TextMesh saveFeedbackText;
    private LineRenderer controllerRayLine;

    // Placement-first, naming-second UX: pending save state until user confirms the name.
    private Vector3 pendingSavePosition;
    private bool hasPendingSavePosition;
    private GameObject pendingSaveMarker;
    private bool isNameSelectionMenuOpen;
    private bool wasRightTriggerPressedForPlacement;
    
    // MVP naming feedback: item name set in the Inspector for testing without a keyboard.
    [SerializeField] private string testItemName = "Keys";
    // MVP preset name selection UI: simple preset names for headset-friendly item naming.
    [SerializeField] private string[] presetItemNames = { "Keys", "Wallet", "Remote" };

    // Canvas save name menu prototype: optional toggle to use World Space Canvas panel instead of TextMesh fallback.
    [SerializeField] private bool useWorldSpaceCanvasSaveNamePanelPrototype;

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

        if (sceneUnderstandingTest == null)
        {
            sceneUnderstandingTest = FindFirstObjectByType<SceneUnderstandingTest>();
        }

        if (arAnchorManager == null)
        {
            arAnchorManager = FindFirstObjectByType<ARAnchorManager>();
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

        // Enum app mode state cleanup: settings mode owns trigger input, so save flow is fully blocked.
        if (savedItemFinderExample != null && savedItemFinderExample.IsSettingsMode())
        {
            if (isNameSelectionMenuOpen)
            {
                HideNameSelectionMenu();
            }

            wasRightTriggerPressedForPlacement = false;
            UpdateAimMarker(false);
            DisableControllerRayVisual();
            wasRightTriggerPressed = rightTriggerPressed;
            wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;
            wasRightSecondaryButtonPressed = rightSecondaryButtonPressed;
            return;
        }

        if (isNameSelectionMenuOpen)
        {
            UpdateAimMarker(false);
            DisableControllerRayVisual();

            wasRightTriggerPressed = rightTriggerPressed;
            wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;
            wasRightSecondaryButtonPressed = rightSecondaryButtonPressed;
            return;
        }

        UpdateAimMarker(rightTriggerPressed);
        UpdateControllerRayVisual(rightTriggerPressed);

        // Placement-first, naming-second UX: capture placement on trigger release, not press.
        if (!isNameSelectionMenuOpen)
        {
            if (rightTriggerPressed)
            {
                // Trigger held: mark that we've detected a press so we can detect the release.
                wasRightTriggerPressedForPlacement = true;
            }
            else if (wasRightTriggerPressedForPlacement)
            {
                // Trigger released after being pressed: capture placement and open naming menu.
                wasRightTriggerPressedForPlacement = false;

                // Symmetric UI state conflict cleanup: close find UI before opening save UI.
                if (savedItemFinderExample != null)
                {
                    if (savedItemFinderExample.IsSettingsMode() || savedItemFinderExample.IsFindMode())
                    {
                        savedItemFinderExample.CancelFindModeAndMenu();
                    }
                }

                BeginNamingForPendingPlacement();
            }
        }

        // Handle keyboard shortcut for save (K key).
        if (Input.GetKeyDown(KeyCode.K) && !isNameSelectionMenuOpen)
        {
            if (savedItemManager != null)
            {
                // Symmetric UI state conflict cleanup: close find UI before opening save UI.
                if (savedItemFinderExample != null)
                {
                    if (savedItemFinderExample.IsSettingsMode() || savedItemFinderExample.IsFindMode())
                    {
                        savedItemFinderExample.CancelFindModeAndMenu();
                    }
                }

                BeginNamingForPendingPlacement();
            }
            else
            {
                Debug.Log("No SavedItemManager found in the scene.");
            }
        }

        wasRightTriggerPressed = rightTriggerPressed;
        wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;
        wasRightSecondaryButtonPressed = rightSecondaryButtonPressed;
    }



    private void ShowNameSelectionMenu()
    {
        // MVP preset name selection UI: open simple headset menu before save instead of typing a name.
        isNameSelectionMenuOpen = true;

        // Keep only the normal XR UI ray during naming menu interaction.
        UpdateAimMarker(false);
        DisableControllerRayVisual();

        if (sceneUnderstandingTest != null)
        {
            sceneUnderstandingTest.SetDebugHitSphereVisible(false);
        }

		if (savedItemFinderExample != null)
		{
			// Enum app mode state cleanup: save naming is now the active primary mode.
			savedItemFinderExample.SetAppMode(AppMode.SaveNaming);
		}

        // Canvas save name menu prototype: Canvas panel is shown via WorldSpaceSaveNamePanelPrototype.
        RefreshSaveNameMenuVisualState();
    }

    private void HideNameSelectionMenu()
    {
        isNameSelectionMenuOpen = false;
        bool shouldRestoreSavePlacementVisuals = savedItemFinderExample != null && savedItemFinderExample.IsSaveMode();

        if (sceneUnderstandingTest != null)
        {
            sceneUnderstandingTest.SetDebugHitSphereVisible(true);
        }

		if (savedItemFinderExample != null && savedItemFinderExample.IsSaveMode())
		{
			// Enum app mode state cleanup: leaving save naming returns to neutral unless another mode takes over.
			savedItemFinderExample.SetAppMode(AppMode.Neutral);
		}

        // Canvas save name menu prototype: Canvas panel is hidden via WorldSpaceSaveNamePanelPrototype.
        RefreshSaveNameMenuVisualState();

        if (aimMarker != null)
        {
            aimMarker.SetActive(false);
        }

        DisableControllerRayVisual();

        if (shouldRestoreSavePlacementVisuals)
        {
            UpdateAimMarker(false);
        }
    }

    public void CancelNameSelectionMenu()
    {
        // UI state conflict cleanup: close save UI visuals without saving anything.
        HideNameSelectionMenu();
        
        // Placement-first, naming-second UX: discard pending save position and marker.
        hasPendingSavePosition = false;
        if (pendingSaveMarker != null)
        {
            Destroy(pendingSaveMarker);
            pendingSaveMarker = null;
        }
    }



    public bool IsNameSelectionMenuOpen()
    {
        return isNameSelectionMenuOpen;
    }

    private Vector3 GetSavePlacementPosition()
    {
        if (rightControllerTransform != null && sceneUnderstandingTest != null)
        {
            Ray controllerRay = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
            Pose sceneHitPose;
            if (sceneUnderstandingTest.TryGetSceneHitFromRay(controllerRay, out sceneHitPose))
            {
                return sceneHitPose.position;
            }
        }

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

		if (isNameSelectionMenuOpen)
		{
			if (aimMarker != null)
			{
				aimMarker.SetActive(false);
			}
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

        bool shouldShowAimMarker = rightTriggerPressed;
        if (!shouldShowAimMarker)
        {
            aimMarker.SetActive(false);
            return;
        }

        // Save Placement UX MVP refinement: preview controller-based scene-understanding hit when available.
        if (rightControllerTransform != null && sceneUnderstandingTest != null)
        {
            Ray controllerRay = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
            Pose sceneHitPose;
            if (sceneUnderstandingTest.TryGetSceneHitFromRay(controllerRay, out sceneHitPose))
            {
                aimMarker.transform.position = sceneHitPose.position;
                aimMarker.SetActive(true);
                return;
            }
        }

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

    private void UpdateControllerRayVisual(bool rightTriggerPressed)
    {
        bool shouldShowRay = !isNameSelectionMenuOpen && showAimMarker && rightControllerTransform != null && rightTriggerPressed;
        if (!shouldShowRay)
        {
            DisableControllerRayVisual();
            return;
        }

        EnsureControllerRayLine();

        Vector3 rayStart = rightControllerTransform.position;
        Vector3 rayDirection = rightControllerTransform.forward;
        Vector3 rayEnd = rayStart + (rayDirection * controllerSaveDistance);

        if (sceneUnderstandingTest != null)
        {
            Ray controllerRay = new Ray(rayStart, rayDirection);
            Pose sceneHitPose;
            if (sceneUnderstandingTest.TryGetSceneHitFromRay(controllerRay, out sceneHitPose))
            {
                rayEnd = sceneHitPose.position;
                controllerRayLine.enabled = true;
                controllerRayLine.SetPosition(0, rayStart);
                controllerRayLine.SetPosition(1, rayEnd);
                return;
            }
        }

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

    // Canvas save name menu prototype: show/hide the Canvas panel based on app mode.
    private void RefreshSaveNameMenuVisualState()
    {
        // Canvas panel visibility is managed by WorldSpaceSaveNamePanelPrototype based on app mode.
    }

    // Placement-first, naming-second UX: store current placement and open naming menu.
    private void BeginNamingForPendingPlacement()
    {
        pendingSavePosition = GetSavePlacementPosition();
        hasPendingSavePosition = true;
        
        // Create/show a temporary marker at the pending position.
        if (pendingSaveMarker == null && showAimMarker)
        {
            pendingSaveMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pendingSaveMarker.name = "PendingSaveMarker";
            pendingSaveMarker.transform.localScale = Vector3.one * 0.06f;
            
            if (aimMarkerMaterial != null)
            {
                Renderer markerRenderer = pendingSaveMarker.GetComponent<Renderer>();
                if (markerRenderer != null)
                {
                    markerRenderer.sharedMaterial = aimMarkerMaterial;
                }
            }
            
            Collider markerCollider = pendingSaveMarker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                markerCollider.enabled = false;
            }
        }
        
        if (pendingSaveMarker != null)
        {
            pendingSaveMarker.transform.position = pendingSavePosition;
            pendingSaveMarker.SetActive(true);
        }
        
        ShowNameSelectionMenu();
    }

    // Placement-first, naming-second UX: save using the stored pending position, not the current controller position.
    public void SavePendingPlacementWithName(string name)
    {
        if (savedItemManager == null)
        {
            Debug.Log("No SavedItemManager found in the scene.");
            return;
        }
        
        if (!hasPendingSavePosition)
        {
            Debug.LogWarning("SavePendingPlacementWithName called but no pending placement exists. Falling back to current position.");
            SaveItemWithName(name);
            return;
        }
        
        string resolvedName = string.IsNullOrWhiteSpace(name) ? "Unnamed Item" : name;
        SaveItemWithNameAtPosition(resolvedName, pendingSavePosition);
        
        // Clear pending state and cleanup marker.
        hasPendingSavePosition = false;
        if (pendingSaveMarker != null)
        {
            Destroy(pendingSaveMarker);
            pendingSaveMarker = null;
        }
        
        HideNameSelectionMenu();
    }

    // Placement-first, naming-second UX: helper that saves an item at a given position.
    private async void SaveItemWithNameAtPosition(string name, Vector3 position)
    {
        if (savedItemManager == null)
        {
            Debug.Log("No SavedItemManager found in the scene.");
            return;
        }

        string resolvedName = string.IsNullOrWhiteSpace(name) ? "Unnamed Item" : name;

        SavedItemData savedItem = new SavedItemData();
        savedItem.itemId = System.Guid.NewGuid().ToString();
        savedItem.itemName = resolvedName;
        savedItem.lastKnownPosition = position;
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");
        savedItem.persistentAnchorId = null;

        if (arAnchorManager == null)
        {
            arAnchorManager = FindFirstObjectByType<ARAnchorManager>();
        }

        if (arAnchorManager == null)
        {
            Debug.LogWarning("[SavedItemExample] ARAnchorManager not found. Saving item with fallback position only.");
        }
        else if (arAnchorManager.descriptor == null)
        {
            Debug.LogWarning("[SavedItemExample] ARAnchorManager descriptor missing. Saving item with fallback position only.");
        }
        else
        {
            Pose anchorPose = new Pose(position, Quaternion.identity);
            var addAnchorResult = await arAnchorManager.TryAddAnchorAsync(anchorPose);
            if (!addAnchorResult.status.IsSuccess())
            {
                Debug.LogWarning("[SavedItemExample] Anchor creation failed. Saving item with fallback position. Status: " + addAnchorResult.status);
            }
            else if (addAnchorResult.value == null)
            {
                Debug.LogWarning("[SavedItemExample] Anchor creation returned null anchor. Saving item with fallback position.");
            }
            else if (!arAnchorManager.descriptor.supportsSaveAnchor)
            {
                Debug.LogWarning("[SavedItemExample] Anchor persistence not supported. Saving item with fallback position only.");
            }
            else
            {
                var saveAnchorResult = await arAnchorManager.TrySaveAnchorAsync(addAnchorResult.value);
                if (!saveAnchorResult.status.IsSuccess())
                {
                    Debug.LogWarning("[SavedItemExample] Anchor persistence failed. Saving item with fallback position. Status: " + saveAnchorResult.status);
                }
                else
                {
                    savedItem.persistentAnchorId = saveAnchorResult.value.ToString();
                    Debug.Log("[SavedItemExample] Anchor persisted for item '" + resolvedName + "'. AnchorId=" + savedItem.persistentAnchorId);
                }
            }
        }

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

        string feedbackMessage = isDuplicate
            ? "Saved duplicate: " + resolvedName
            : "Saved: " + resolvedName;
        ShowTemporarySaveFeedback(feedbackMessage);

        Debug.Log("Saved item: Name=" + savedItem.itemName + ", Id=" + savedItem.itemId + ", Position=" + savedItem.lastKnownPosition + ", SavedAtUtc=" + savedItem.savedAtUtc);
    }

    // Canvas save name menu prototype: perform a full save using the given name and current placement position.
    public void SaveItemWithName(string name)
    {
        SaveItemWithNameAtPosition(name, GetSavePlacementPosition());
    }

    // Canvas save name menu prototype: called by WorldSpaceSaveNamePanelPrototype on enable/disable.
    public void SetUseWorldSpaceCanvasSaveNamePanelPrototypeEnabled(bool isEnabled)
    {
        useWorldSpaceCanvasSaveNamePanelPrototype = isEnabled;
        RefreshSaveNameMenuVisualState();
    }

    // Canvas save name menu prototype: queried by WorldSpaceSaveNamePanelPrototype to decide show/hide.
    public bool IsWorldSpaceCanvasSaveNamePanelPrototypeEnabled()
    {
        return useWorldSpaceCanvasSaveNamePanelPrototype;
    }
}