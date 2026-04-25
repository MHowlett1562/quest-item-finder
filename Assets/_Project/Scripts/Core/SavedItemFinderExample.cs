using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR;

public class SavedItemFinderExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
	private List<GameObject> spawnedMarkers = new List<GameObject>();
	private Transform spawnedMarkersParent;
	private SavedItemData currentTargetItem;
	private float nextDistanceLogTime;
	private float lastHotColdDistance = -1f;
	private float hotColdCheckTimer = 0f;
	private float hotColdCheckInterval = 0.35f;
	private TextMesh distanceText;
	private TextMesh hudArrowText;
	private TextMesh hotColdText;
	private TextMesh itemSelectionMenuText;
	private GameObject directionalIndicator;
	[SerializeField] private SavedItemExample savedItemExample;

	// Symmetric UI state conflict cleanup: expose read-only find UI state for other scripts.
	public bool IsFindItemSelectionMenuOpen => isItemSelectionMenuActive;
	public bool IsFindModeActive => isSingleItemFindModeActive;

	[SerializeField] private string testItemName = "Keys";
	// Temporary XR visual debugging toggles (Inspector) to isolate discomfort sources.
	[SerializeField] private bool showDistanceText = true;
	[SerializeField] private bool showDirectionalIndicator = false;
	[SerializeField] private bool showTargetMarker = false;
	private bool wasLeftTriggerPressed;
	private bool wasRightPrimaryButtonPressed;
	private bool wasRightSecondaryButtonPressed;
	// Temporary MVP controller debug cleanup: track deliberate A+B hold before clearing saved data.
	private float clearAllButtonsHoldTimer;
	private bool wasClearAllHoldActive;
	private const float clearAllHoldDurationSeconds = 2f;
	// Find Mode toggle UX improvement: true while single-item find mode is actively guiding to one item.
	private bool isSingleItemFindModeActive;
	// MVP item selection UI: active while user is choosing which item to find.
	private bool isItemSelectionMenuActive;
	// MVP item selection UI: unique item names shown in the simple camera-parented menu.
	private List<string> selectableItemNames = new List<string>();
	private int selectedItemNameIndex;
	private Coroutine hideItemSelectionMenuTextCoroutine;
	private static readonly Vector3 distanceTextLocalOffset = new Vector3(0f, -0.15f, 1.5f);
	private static readonly Vector3 hudArrowLocalOffset = new Vector3(0f, -0.06f, 1.5f);
	private Vector3 currentHudArrowLocalPosition = new Vector3(0f, -0.08f, 1.5f);
	private int lastBehindArrowSide = 1;
	private static readonly Vector3 hotColdTextLocalOffset = new Vector3(0f, -0.24f, 1.5f);
	private static readonly Vector3 itemSelectionMenuLocalOffset = new Vector3(0f, 0.06f, 1.3f);
	private static readonly Vector3 directionalIndicatorLocalOffset = new Vector3(0f, -0.08f, 1.2f);
	private float hotColdDeadZoneMeters = 0.15f;
	private const float minWaypointScaleMultiplier = 0.7f;
	private const float maxWaypointScaleMultiplier = 1.4f;
	// Temporary XR runtime material fix for primitives created at runtime.
	[SerializeField] private Material targetMarkerMaterial;
	[SerializeField] private Material directionalIndicatorMaterial;

	private void Start()
	{
		savedItemManager = FindFirstObjectByType<SavedItemManager>();
		if (savedItemExample == null)
		{
			savedItemExample = FindFirstObjectByType<SavedItemExample>();
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
		if (savedItemManager == null)
		{
			Debug.Log("No SavedItemManager found in the scene.");
			return;
		}

		bool leftTriggerPressed = false;
		InputDevice leftHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
		if (leftHandDevice.isValid)
		{
			leftHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out leftTriggerPressed);
		}

		bool rightPrimaryButtonPressed = false;
		bool rightSecondaryButtonPressed = false;
		InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
		if (rightHandDevice.isValid)
		{
			rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimaryButtonPressed);
			rightHandDevice.TryGetFeatureValue(CommonUsages.secondaryButton, out rightSecondaryButtonPressed);
		}

		bool leftTriggerPressedThisFrame = leftTriggerPressed && !wasLeftTriggerPressed;
		bool rightPrimaryButtonPressedThisFrame = rightPrimaryButtonPressed && !wasRightPrimaryButtonPressed;
		bool rightSecondaryButtonPressedThisFrame = rightSecondaryButtonPressed && !wasRightSecondaryButtonPressed;

		wasLeftTriggerPressed = leftTriggerPressed;
		wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;
		wasRightSecondaryButtonPressed = rightSecondaryButtonPressed;

		bool findInputPressedThisFrame = Input.GetKeyDown(KeyCode.G) || leftTriggerPressedThisFrame;

		if (Input.GetKeyDown(KeyCode.C))
		{
			HandleDebugClearAllSavedItems();
			return;
		}

		UpdateControllerClearAllHold(rightPrimaryButtonPressed, rightSecondaryButtonPressed);

		// MVP input conflict cleanup: reserve A/B for active menus by gating show-all while save-name menu is open.
		bool isSaveNameMenuOpen = savedItemExample != null && savedItemExample.IsNameSelectionMenuOpen;
		if (Input.GetKeyDown(KeyCode.F) || (!isItemSelectionMenuActive && !isSaveNameMenuOpen && rightPrimaryButtonPressedThisFrame))
		{
			if (isSaveNameMenuOpen)
			{
				savedItemExample.CancelNameSelectionMenu();
			}

			HideItemSelectionMenu();
			currentTargetItem = null;
			SpawnAllSavedItems();
		}

		if (isItemSelectionMenuActive)
		{
			UpdateItemSelectionMenuTransform();
			HandleItemSelectionMenuCyclingInput(rightPrimaryButtonPressedThisFrame, rightSecondaryButtonPressedThisFrame);
		}

		if (findInputPressedThisFrame)
		{
			if (isSaveNameMenuOpen)
			{
				// UI state conflict cleanup: close save UI before opening find UI.
				savedItemExample.CancelNameSelectionMenu();
			}

			// Find Mode toggle UX improvement: G/left trigger now toggles single-item find mode on/off.
			if (isSingleItemFindModeActive)
			{
				DisableSingleItemFindMode();
			}
			else if (isItemSelectionMenuActive)
			{
				ConfirmItemSelectionAndStartFindMode();
			}
			else
			{
				ShowItemSelectionMenu();
			}
		}

		if (!showDistanceText && distanceText != null)
		{
			distanceText.gameObject.SetActive(false);
		}

		if (!showDirectionalIndicator && directionalIndicator != null)
		{
			directionalIndicator.SetActive(false);
		}

		if (!showTargetMarker && spawnedMarkers.Count > 0)
		{
			ClearSpawnedMarkers();
		}

		if (showTargetMarker && spawnedMarkers.Count > 0 && Camera.main != null)
		{
			UpdateWaypointMarkerScale();
		}

		if (currentTargetItem != null && Camera.main != null)
		{
			if (showDistanceText)
			{
				EnsureDistanceText();
				distanceText.gameObject.SetActive(true);
				UpdateDistanceTextTransform();
			}

			// HUD arrow direction UX improvement: flat TextMesh arrow for quick passthrough readability.
			EnsureHudArrowText();
			hudArrowText.gameObject.SetActive(true);
			EnsureHotColdText();
			hotColdText.gameObject.SetActive(true);
			UpdateHotColdTextTransform();

			float distanceToItem = Vector3.Distance(Camera.main.transform.position, currentTargetItem.lastKnownPosition);
			// Direction UX improvement: replace rotating 3D indicator with simple text guidance.
			string directionText = GetDirectionGuidanceText();
			UpdateHotColdFeedback(distanceToItem);

			// Draw every frame so the guidance line stays visible while in find mode.
			Debug.DrawLine(Camera.main.transform.position, currentTargetItem.lastKnownPosition, Color.red);
			if (showDistanceText && distanceText != null)
			{
				distanceText.text = "Distance: " + distanceToItem.ToString("F1") + "m\n" + directionText;
			}

			if (hudArrowText != null)
			{
				hudArrowText.text = GetDirectionArrowSymbol(directionText);
				UpdateHudArrowTextTransform(directionText);
			}

			if (Time.time >= nextDistanceLogTime)
			{
				Debug.Log("Distance to selected item: " + distanceToItem.ToString("F2") + " meters");
				nextDistanceLogTime = Time.time + 1f;
			}
		}
		else
		{
			if (distanceText != null)
			{
				distanceText.gameObject.SetActive(false);
			}

			if (directionalIndicator != null)
			{
				directionalIndicator.SetActive(false);
			}

			if (hudArrowText != null)
			{
				hudArrowText.gameObject.SetActive(false);
			}

			if (hotColdText != null)
			{
				hotColdText.gameObject.SetActive(false);
			}
		}
	}

	private void EnsureDistanceText()
	{
		if (distanceText != null)
		{
			return;
		}

		GameObject distanceTextObject = new GameObject("DistanceText");
		distanceText = distanceTextObject.AddComponent<TextMesh>();
		if (Camera.main != null)
		{
			distanceText.transform.SetParent(Camera.main.transform, false);
			distanceText.transform.localPosition = distanceTextLocalOffset;
			distanceText.transform.localRotation = Quaternion.identity;
		}
		distanceText.fontSize = 48;
		distanceText.characterSize = 0.01f;
		distanceText.anchor = TextAnchor.MiddleCenter;
		distanceText.alignment = TextAlignment.Center;
		distanceText.color = Color.white;
	}

	private void UpdateDistanceTextTransform()
	{
		if (distanceText == null || Camera.main == null)
		{
			return;
		}

		if (distanceText.transform.parent != Camera.main.transform)
		{
			distanceText.transform.SetParent(Camera.main.transform, false);
		}

		distanceText.transform.localPosition = distanceTextLocalOffset;
		distanceText.transform.localRotation = Quaternion.identity;
	}

	private void EnsureHudArrowText()
	{
		if (hudArrowText != null)
		{
			return;
		}

		GameObject hudArrowTextObject = new GameObject("HudDirectionArrowText");
		hudArrowText = hudArrowTextObject.AddComponent<TextMesh>();
		if (Camera.main != null)
		{
			hudArrowText.transform.SetParent(Camera.main.transform, false);
			hudArrowText.transform.localPosition = currentHudArrowLocalPosition;
			hudArrowText.transform.localRotation = Quaternion.identity;
		}
		// Edge-of-screen waypoint indicator polish: larger minimalist chevron-style HUD arrow.
		hudArrowText.fontSize = 96;
		hudArrowText.characterSize = 0.014f;
		hudArrowText.anchor = TextAnchor.MiddleCenter;
		hudArrowText.alignment = TextAlignment.Center;
		hudArrowText.color = Color.white;
	}

	private void EnsureHotColdText()
	{
		if (hotColdText != null)
		{
			return;
		}

		GameObject hotColdTextObject = new GameObject("HotColdText");
		hotColdText = hotColdTextObject.AddComponent<TextMesh>();
		if (Camera.main != null)
		{
			hotColdText.transform.SetParent(Camera.main.transform, false);
			hotColdText.transform.localPosition = hotColdTextLocalOffset;
			hotColdText.transform.localRotation = Quaternion.identity;
		}
		hotColdText.fontSize = 48;
		hotColdText.characterSize = 0.01f;
		hotColdText.anchor = TextAnchor.MiddleCenter;
		hotColdText.alignment = TextAlignment.Center;
		hotColdText.color = Color.white;
	}

	private void UpdateHotColdTextTransform()
	{
		if (hotColdText == null || Camera.main == null)
		{
			return;
		}

		if (hotColdText.transform.parent != Camera.main.transform)
		{
			hotColdText.transform.SetParent(Camera.main.transform, false);
		}

		hotColdText.transform.localPosition = hotColdTextLocalOffset;
		hotColdText.transform.localRotation = Quaternion.identity;
	}

	private void UpdateHotColdFeedback(float currentDistance)
	{
		// Hot/cold proximity feedback polish: compare at a fixed interval to reduce tracking noise.
		if (hotColdText == null)
		{
			return;
		}

		hotColdCheckTimer += Time.deltaTime;
		if (hotColdCheckTimer < hotColdCheckInterval)
		{
			return;
		}

		hotColdCheckTimer = 0f;
		float previousDistanceBeforeUpdate = lastHotColdDistance;
		float delta = 0f;
		Color arrowHotColdColor = Color.white;

		if (lastHotColdDistance < 0f)
		{
			lastHotColdDistance = currentDistance;
			hotColdText.text = "STEADY";
			hotColdText.color = Color.white;
			arrowHotColdColor = Color.white;
			delta = 0f;
		}
		else
		{
			delta = currentDistance - lastHotColdDistance;
			if (delta < -hotColdDeadZoneMeters)
			{
				hotColdText.text = "WARMER";
				hotColdText.color = Color.green;
				arrowHotColdColor = Color.green;
			}
			else if (delta > hotColdDeadZoneMeters)
			{
				hotColdText.text = "COLDER";
				hotColdText.color = Color.red;
				arrowHotColdColor = Color.red;
			}
			else
			{
				hotColdText.text = "STEADY";
				hotColdText.color = Color.white;
				arrowHotColdColor = Color.white;
			}

			lastHotColdDistance = currentDistance;
		}

		// Arrow color hot/cold feedback polish: reuse the same hot/cold result to color the direction arrow.
		if (hudArrowText != null)
		{
			hudArrowText.color = arrowHotColdColor;
		}

		Debug.Log($"HOTCOLD current={currentDistance:F2} last={previousDistanceBeforeUpdate:F2} delta={delta:F2}");
	}

	private void EnsureItemSelectionMenuText()
	{
		if (itemSelectionMenuText != null)
		{
			return;
		}

		GameObject itemSelectionMenuObject = new GameObject("ItemSelectionMenuText");
		itemSelectionMenuText = itemSelectionMenuObject.AddComponent<TextMesh>();
		itemSelectionMenuText.fontSize = 48;
		itemSelectionMenuText.characterSize = 0.01f;
		itemSelectionMenuText.anchor = TextAnchor.MiddleCenter;
		itemSelectionMenuText.alignment = TextAlignment.Center;
		itemSelectionMenuText.color = Color.white;
		itemSelectionMenuText.gameObject.SetActive(false);
		UpdateItemSelectionMenuTransform();
	}

	private void UpdateItemSelectionMenuTransform()
	{
		if (itemSelectionMenuText == null || Camera.main == null)
		{
			return;
		}

		if (itemSelectionMenuText.transform.parent != Camera.main.transform)
		{
			itemSelectionMenuText.transform.SetParent(Camera.main.transform, false);
		}

		itemSelectionMenuText.transform.localPosition = itemSelectionMenuLocalOffset;
		itemSelectionMenuText.transform.localRotation = Quaternion.identity;
	}

	private void ShowItemSelectionMenu()
	{
		// MVP item selection UI: build a unique list of saved item names and show a tiny menu.
		savedItemManager.LoadData();
		List<SavedItemData> items = savedItemManager.GetAllItems();
		if (items == null)
		{
			items = new List<SavedItemData>();
		}

		selectableItemNames.Clear();
		HashSet<string> seenNames = new HashSet<string>();

		for (int i = 0; i < items.Count; i++)
		{
			SavedItemData item = items[i];
			if (item == null)
			{
				continue;
			}

			string name = string.IsNullOrWhiteSpace(item.itemName) ? "Unnamed Item" : item.itemName;
			if (seenNames.Add(name))
			{
				selectableItemNames.Add(name);
			}
		}

		// MVP XR menu navigation: log source item counts and the unique menu options.
		Debug.Log("MVP menu build: total saved items=" + items.Count + ", unique names=" + selectableItemNames.Count);
		for (int i = 0; i < selectableItemNames.Count; i++)
		{
			Debug.Log("MVP menu option " + i + ": " + selectableItemNames[i]);
		}

		if (selectableItemNames.Count == 0)
		{
			ShowTemporaryItemSelectionMessage("No saved items");
			isItemSelectionMenuActive = false;
			return;
		}

		if (hideItemSelectionMenuTextCoroutine != null)
		{
			StopCoroutine(hideItemSelectionMenuTextCoroutine);
			hideItemSelectionMenuTextCoroutine = null;
		}

		selectedItemNameIndex = 0;
		isItemSelectionMenuActive = true;

		EnsureItemSelectionMenuText();
		UpdateItemSelectionMenuText();
		itemSelectionMenuText.gameObject.SetActive(true);
	}

	private void HandleItemSelectionMenuCyclingInput(bool rightPrimaryButtonPressedThisFrame, bool rightSecondaryButtonPressedThisFrame)
	{
		if (!isItemSelectionMenuActive || selectableItemNames.Count == 0)
		{
			return;
		}

		// MVP XR menu navigation: support both keyboard fallback and headset-friendly A/B cycling.
		if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || rightPrimaryButtonPressedThisFrame)
		{
			selectedItemNameIndex++;
			if (selectedItemNameIndex >= selectableItemNames.Count)
			{
				selectedItemNameIndex = 0;
			}

			UpdateItemSelectionMenuText();
		}

		if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || rightSecondaryButtonPressedThisFrame)
		{
			selectedItemNameIndex--;
			if (selectedItemNameIndex < 0)
			{
				selectedItemNameIndex = selectableItemNames.Count - 1;
			}

			UpdateItemSelectionMenuText();
		}
	}

	private void ConfirmItemSelectionAndStartFindMode()
	{
		if (!isItemSelectionMenuActive || selectableItemNames.Count == 0)
		{
			HideItemSelectionMenu();
			return;
		}

		string selectedItemName = selectableItemNames[selectedItemNameIndex];
		if (string.IsNullOrWhiteSpace(selectedItemName))
		{
			selectedItemName = string.IsNullOrWhiteSpace(testItemName) ? "Unnamed Item" : testItemName;
		}

		HideItemSelectionMenu();
		SpawnOneSavedItemByName(selectedItemName);

		// Keep old keyboard/debug fallback available if selected lookup fails unexpectedly.
		if (currentTargetItem == null && !string.IsNullOrWhiteSpace(testItemName) && selectedItemName != testItemName)
		{
			SpawnOneSavedItemByName(testItemName);
		}

		isSingleItemFindModeActive = currentTargetItem != null;
		if (isSingleItemFindModeActive)
		{
			lastHotColdDistance = -1f;
			hotColdCheckTimer = 0f;
		}
	}

	private void UpdateItemSelectionMenuText()
	{
		if (itemSelectionMenuText == null)
		{
			return;
		}

		if (selectableItemNames.Count == 0)
		{
			itemSelectionMenuText.text = "No saved items";
			return;
		}

		string selectedName = selectableItemNames[selectedItemNameIndex];
		itemSelectionMenuText.text = "Select item\n" + selectedName + "\nA/B to cycle\nLeft Trigger to confirm";
	}

	private void HideItemSelectionMenu()
	{
		isItemSelectionMenuActive = false;

		if (itemSelectionMenuText != null)
		{
			itemSelectionMenuText.gameObject.SetActive(false);
		}
	}

	private void ShowTemporaryItemSelectionMessage(string message)
	{
		EnsureItemSelectionMenuText();
		UpdateItemSelectionMenuTransform();
		itemSelectionMenuText.text = message;
		itemSelectionMenuText.gameObject.SetActive(true);

		if (hideItemSelectionMenuTextCoroutine != null)
		{
			StopCoroutine(hideItemSelectionMenuTextCoroutine);
		}

		hideItemSelectionMenuTextCoroutine = StartCoroutine(HideItemSelectionMenuTextAfterDelay());
	}

	private IEnumerator HideItemSelectionMenuTextAfterDelay()
	{
		yield return new WaitForSeconds(2f);

		if (!isItemSelectionMenuActive && itemSelectionMenuText != null)
		{
			itemSelectionMenuText.gameObject.SetActive(false);
		}

		hideItemSelectionMenuTextCoroutine = null;
	}

	private void UpdateHudArrowTextTransform(string directionText)
	{
		if (hudArrowText == null || Camera.main == null || currentTargetItem == null)
		{
			return;
		}

		if (hudArrowText.transform.parent != Camera.main.transform)
		{
			hudArrowText.transform.SetParent(Camera.main.transform, false);
		}

		Vector3 viewportPosition = Camera.main.WorldToViewportPoint(currentTargetItem.lastKnownPosition);
		bool isTargetVisible = viewportPosition.z > 0f
			&& viewportPosition.x >= 0f && viewportPosition.x <= 1f
			&& viewportPosition.y >= 0f && viewportPosition.y <= 1f;

		// Dynamic edge-of-screen waypoint indicator polish: hide HUD arrow when target is already visible.
		if (isTargetVisible)
		{
			hudArrowText.gameObject.SetActive(false);
			return;
		}

		if (!hudArrowText.gameObject.activeSelf)
		{
			hudArrowText.gameObject.SetActive(true);
		}

		Vector3 targetHudArrowLocalPosition;
		const float hudXMin = -0.65f;
		const float hudXMax = 0.65f;
		const float hudYMin = -0.28f;
		const float hudYMax = 0.28f;

		Vector3 directionToTarget = currentTargetItem.lastKnownPosition - Camera.main.transform.position;
		if (directionToTarget.sqrMagnitude > 0.0001f)
		{
			Vector3 localDirection = Camera.main.transform.InverseTransformDirection(directionToTarget.normalized);

			// Behind-target side-arrow polish: keep behind targets pinned to left/right edge with stronger vertical offset.
			if (localDirection.z < -0.25f)
			{
				int behindArrowSide = lastBehindArrowSide;
				if (localDirection.x < -0.05f)
				{
					behindArrowSide = -1;
				}
				else if (localDirection.x > 0.05f)
				{
					behindArrowSide = 1;
				}

				lastBehindArrowSide = behindArrowSide;
				float behindLocalY = Mathf.Clamp(localDirection.y * 0.35f, -0.28f, 0.28f);
				targetHudArrowLocalPosition = new Vector3(behindArrowSide < 0 ? -0.65f : 0.65f, behindLocalY, hudArrowLocalOffset.z);
				currentHudArrowLocalPosition = Vector3.Lerp(currentHudArrowLocalPosition, targetHudArrowLocalPosition, 0.2f);
				hudArrowText.transform.localPosition = currentHudArrowLocalPosition;
				hudArrowText.transform.localRotation = Quaternion.identity;
				return;
			}
		}

		if (viewportPosition.z <= 0f)
		{
			viewportPosition.x = 1f - viewportPosition.x;
			viewportPosition.y = 1f - viewportPosition.y;
		}

		viewportPosition.x = Mathf.Clamp(viewportPosition.x, 0.12f, 0.88f);
		viewportPosition.y = Mathf.Clamp(viewportPosition.y, 0.16f, 0.84f);

		float edgeBiasedViewportX = viewportPosition.x < 0.5f ? 0.12f : 0.88f;
		if (Mathf.Abs(viewportPosition.x - 0.5f) < 0.12f)
		{
			edgeBiasedViewportX = viewportPosition.x;
		}

		float edgeBiasedViewportY = Mathf.Clamp(viewportPosition.y, 0.16f, 0.84f);
		float localX = Mathf.Lerp(hudXMin, hudXMax, edgeBiasedViewportX);
		float localY = Mathf.Lerp(hudYMin, hudYMax, edgeBiasedViewportY);
		localX = Mathf.Clamp(localX, hudXMin, hudXMax);
		localY = Mathf.Clamp(localY, hudYMin, hudYMax);
		targetHudArrowLocalPosition = new Vector3(localX, localY, hudArrowLocalOffset.z);
		currentHudArrowLocalPosition = Vector3.Lerp(currentHudArrowLocalPosition, targetHudArrowLocalPosition, 0.2f);

		// 2D peripheral HUD arrow movement polish: slide arrow around a safe HUD perimeter instead of a flat line.
		hudArrowText.transform.localPosition = currentHudArrowLocalPosition;

		hudArrowText.transform.localRotation = Quaternion.identity;
	}

	private void EnsureDirectionalIndicator()
	{
		// Direction UX improvement: 3D directional indicator is disabled in favor of text guidance.
		if (directionalIndicator != null)
		{
			directionalIndicator.SetActive(false);
		}
	}

	private void UpdateDirectionalIndicatorTransform()
	{
		// Direction UX improvement: 3D directional indicator is disabled in favor of text guidance.
		if (directionalIndicator != null)
		{
			directionalIndicator.SetActive(false);
		}
	}

	private void SpawnAllSavedItems()
	{
		ClearSpawnedMarkers();
		EnsureSpawnedMarkersParent();

		savedItemManager.LoadData();

		List<SavedItemData> items = savedItemManager.GetAllItems();

		if (items == null || items.Count == 0)
		{
			Debug.Log("No saved items to spawn markers for.");
			return;
		}

		if (!showTargetMarker)
		{
			Debug.Log("Target marker visuals are disabled for XR debugging.");
			return;
		}

		for (int i = 0; i < items.Count; i++)
		{
			SavedItemData item = items[i];
			SpawnMarkerForItem(item);
		}

		Debug.Log("Spawned " + spawnedMarkers.Count + " marker(s).");
	}

	private string GetDirectionGuidanceText()
	{
		if (currentTargetItem == null || Camera.main == null)
		{
			return "Ahead";
		}

		Vector3 directionToTarget = currentTargetItem.lastKnownPosition - Camera.main.transform.position;

		if (directionToTarget.sqrMagnitude < 0.0001f)
		{
			return "Ahead";
		}

		Vector3 localDirection = Camera.main.transform.InverseTransformDirection(directionToTarget.normalized);

		if (localDirection.z > 0.35f && Mathf.Abs(localDirection.x) < 0.2f)
		{
			return "Ahead";
		}

		if (localDirection.z < -0.25f)
		{
			if (localDirection.x < 0f)
			{
				return "Turn Around Left";
			}

			return "Turn Around Right";
		}

		if (localDirection.x < 0f)
		{
			return "Turn Left";
		}

		return "Turn Right";
	}

	private string GetDirectionArrowSymbol(string directionText)
	{
		if (currentTargetItem != null && Camera.main != null)
		{
			Vector3 directionToTarget = currentTargetItem.lastKnownPosition - Camera.main.transform.position;
			if (directionToTarget.sqrMagnitude > 0.0001f)
			{
				Vector3 localDirection = Camera.main.transform.InverseTransformDirection(directionToTarget.normalized);
				if (localDirection.z < -0.25f)
				{
					int behindArrowSide = lastBehindArrowSide;
					if (localDirection.x < -0.05f)
					{
						behindArrowSide = -1;
					}
					else if (localDirection.x > 0.05f)
					{
						behindArrowSide = 1;
					}

					return behindArrowSide < 0 ? "\u2039" : "\u203A";
				}
			}
		}

		if (currentTargetItem != null && Camera.main != null)
		{
			Vector3 viewportPosition = Camera.main.WorldToViewportPoint(currentTargetItem.lastKnownPosition);
			if (viewportPosition.z > 0f && viewportPosition.y > 0.84f)
			{
				return "\u2227";
			}
		}

		if (directionText == "Turn Left")
		{
			return "\u2039";
		}

		if (directionText == "Turn Right")
		{
			return "\u203A";
		}

		return "\u2228";
	}

	private void SpawnOneSavedItemByName(string itemName)
	{
		ClearSpawnedMarkers();
		EnsureSpawnedMarkersParent();

		savedItemManager.LoadData();
		SavedItemData item = savedItemManager.GetItemByName(itemName);

		if (item == null)
		{
			Debug.Log("No saved item named '" + itemName + "' was found.");
			return;
		}

		currentTargetItem = item;
		lastHotColdDistance = -1f;
		hotColdCheckTimer = 0f;

		Debug.Log("Selected item: Name=" + item.itemName + ", Id=" + item.itemId + ", Position=" + item.lastKnownPosition + ", SavedAtUtc=" + item.savedAtUtc);
		if (Camera.main != null)
		{
			float distanceToItem = Vector3.Distance(Camera.main.transform.position, item.lastKnownPosition);
			Debug.Log("Distance to selected item: " + distanceToItem.ToString("F2") + " meters");
			Debug.DrawLine(Camera.main.transform.position, item.lastKnownPosition, Color.red, 5f);
		}

		if (showTargetMarker)
		{
			SpawnMarkerForItem(item);
			Debug.Log("Spawned marker for item: " + item.itemName);
		}
	}

	private void EnsureSpawnedMarkersParent()
	{
		if (spawnedMarkersParent == null)
		{
			GameObject parentObject = GameObject.Find("SpawnedMarkers");

			if (parentObject == null)
			{
				parentObject = new GameObject("SpawnedMarkers");
			}

			spawnedMarkersParent = parentObject.transform;
		}
	}

	private void DisableSingleItemFindMode()
	{
		isSingleItemFindModeActive = false;
		currentTargetItem = null;
		lastHotColdDistance = -1f;
		hotColdCheckTimer = 0f;
		HideItemSelectionMenu();

		if (distanceText != null)
		{
			distanceText.gameObject.SetActive(false);
		}

		if (directionalIndicator != null)
		{
			directionalIndicator.SetActive(false);
		}

		if (hudArrowText != null)
		{
			hudArrowText.gameObject.SetActive(false);
		}

		if (hotColdText != null)
		{
			hotColdText.gameObject.SetActive(false);
		}

		ClearSpawnedMarkers();
	}

	public void CancelFindModeAndMenu()
	{
		// Symmetric UI state conflict cleanup: close find menu/find mode UI without changing saved data.
		DisableSingleItemFindMode();
	}

	private void UpdateControllerClearAllHold(bool rightPrimaryButtonPressed, bool rightSecondaryButtonPressed)
	{
		// Temporary MVP controller debug cleanup: require a deliberate in-headset A+B hold to clear all data.
		bool canUseControllerClearShortcut = !isItemSelectionMenuActive && !isSingleItemFindModeActive;
		bool isClearHoldActive = canUseControllerClearShortcut && rightPrimaryButtonPressed && rightSecondaryButtonPressed;

		if (isClearHoldActive)
		{
			if (!wasClearAllHoldActive)
			{
				Debug.Log("Controller clear-all hold started. Keep A+B pressed for 2 seconds.");
			}

			clearAllButtonsHoldTimer += Time.deltaTime;
			if (clearAllButtonsHoldTimer >= clearAllHoldDurationSeconds)
			{
				Debug.Log("Controller clear-all hold complete. Clearing saved items.");
				HandleDebugClearAllSavedItems();
				clearAllButtonsHoldTimer = 0f;
				wasClearAllHoldActive = false;
				return;
			}
		}
		else
		{
			clearAllButtonsHoldTimer = 0f;
		}

		wasClearAllHoldActive = isClearHoldActive;
	}

	private void HandleDebugClearAllSavedItems()
	{
		// Temporary MVP testing/debug cleanup feature: clear saved data and reset active find/menu visuals.
		DisableSingleItemFindMode();
		savedItemManager.ClearAllItems();
		ShowTemporaryItemSelectionMessage("Saved items cleared");
	}

	private Material CreateUnlitRedMaterial()
	{
		// Waypoint marker visual polish: create runtime unlit red material with URP transparency and fallback chain.
		Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Color");
		}
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		Material material = new Material(shader);
		material.color = new Color(1f, 0.2f, 0.2f, 0.8f);

		// URP transparency settings if using URP shader.
		if (shader.name.Contains("URP") || shader.name.Contains("Universal"))
		{
			material.SetFloat("_Surface", 1);
			material.SetFloat("_Blend", 0);
			material.SetOverrideTag("RenderType", "Transparent");
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}

		return material;
	}

	private Material CreateUnlitWhiteMaterial()
	{
		// Waypoint marker visual polish: create runtime unlit white material with fallback chain.
		Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Color");
		}
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		Material material = new Material(shader);
		material.color = Color.white;
		return material;
	}

	private void SpawnMarkerForItem(SavedItemData item)
	{
		// Waypoint marker visual polish: clean 3D map pin with tapered drop.
		GameObject waypointMarker = new GameObject(item.itemName + " Waypoint Marker");
		waypointMarker.transform.SetParent(spawnedMarkersParent, true);
		waypointMarker.transform.position = item.lastKnownPosition;

		Material redMaterial = CreateUnlitRedMaterial();
		Material whiteMaterial = CreateUnlitWhiteMaterial();

		// Red sphere head (0.12m).
		GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		sphere.name = "WaypointSphere";
		sphere.transform.SetParent(waypointMarker.transform, false);
		sphere.transform.localPosition = Vector3.zero;
		sphere.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
		Renderer sphereRenderer = sphere.GetComponent<Renderer>();
		if (sphereRenderer != null)
		{
			sphereRenderer.sharedMaterial = redMaterial;
		}
		Collider sphereCollider = sphere.GetComponent<Collider>();
		if (sphereCollider != null)
		{
			sphereCollider.enabled = false;
		}

		// Small white center dot for clarity (0.035m).
		GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		dot.name = "WaypointDot";
		dot.transform.SetParent(waypointMarker.transform, false);
		dot.transform.localPosition = Vector3.zero;
		dot.transform.localScale = new Vector3(0.035f, 0.035f, 0.035f);
		Renderer dotRenderer = dot.GetComponent<Renderer>();
		if (dotRenderer != null)
		{
			dotRenderer.sharedMaterial = whiteMaterial;
		}
		Collider dotCollider = dot.GetComponent<Collider>();
		if (dotCollider != null)
		{
			dotCollider.enabled = false;
		}

		// Tapered red drop point below (narrow capsule).
		GameObject drop = GameObject.CreatePrimitive(PrimitiveType.Capsule);
		drop.name = "WaypointDrop";
		drop.transform.SetParent(waypointMarker.transform, false);
		drop.transform.localPosition = new Vector3(0f, -0.11f, 0f);
		drop.transform.localScale = new Vector3(0.025f, 0.12f, 0.025f);
		Renderer dropRenderer = drop.GetComponent<Renderer>();
		if (dropRenderer != null)
		{
			dropRenderer.sharedMaterial = redMaterial;
		}
		Collider dropCollider = drop.GetComponent<Collider>();
		if (dropCollider != null)
		{
			dropCollider.enabled = false;
		}

		spawnedMarkers.Add(waypointMarker);
	}

	private void UpdateWaypointMarkerScale()
	{
		// Distance-based waypoint scaling polish: scale marker roots subtly by camera distance.
		for (int i = 0; i < spawnedMarkers.Count; i++)
		{
			GameObject marker = spawnedMarkers[i];
			if (marker == null)
			{
				continue;
			}

			float distanceToMarker = Vector3.Distance(Camera.main.transform.position, marker.transform.position);
			float scaleMultiplier = Mathf.Lerp(minWaypointScaleMultiplier, maxWaypointScaleMultiplier, Mathf.InverseLerp(1f, 6f, distanceToMarker));
			scaleMultiplier = Mathf.Clamp(scaleMultiplier, minWaypointScaleMultiplier, maxWaypointScaleMultiplier);
			marker.transform.localScale = Vector3.one * scaleMultiplier;
		}
	}

	private void ClearSpawnedMarkers()
	{
		for (int i = 0; i < spawnedMarkers.Count; i++)
		{
			if (spawnedMarkers[i] != null)
			{
				Destroy(spawnedMarkers[i]);
			}
		}

		spawnedMarkers.Clear();
	}
}

