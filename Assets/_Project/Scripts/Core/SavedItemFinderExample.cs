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
	private TextMesh distanceText;
	private TextMesh hudArrowText;
	private TextMesh itemSelectionMenuText;
	private GameObject directionalIndicator;
	[SerializeField] private string testItemName = "Keys";
	// Temporary XR visual debugging toggles (Inspector) to isolate discomfort sources.
	[SerializeField] private bool showDistanceText = true;
	[SerializeField] private bool showDirectionalIndicator = false;
	[SerializeField] private bool showTargetMarker = false;
	private bool wasLeftTriggerPressed;
	private bool wasRightPrimaryButtonPressed;
	private bool wasRightSecondaryButtonPressed;
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
	private static readonly Vector3 itemSelectionMenuLocalOffset = new Vector3(0f, 0.06f, 1.3f);
	private static readonly Vector3 directionalIndicatorLocalOffset = new Vector3(0f, -0.08f, 1.2f);
	// Temporary XR runtime material fix for primitives created at runtime.
	[SerializeField] private Material targetMarkerMaterial;
	[SerializeField] private Material directionalIndicatorMaterial;

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

		if (Input.GetKeyDown(KeyCode.F) || (!isItemSelectionMenuActive && rightPrimaryButtonPressedThisFrame))
		{
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
			UpdateHudArrowTextTransform();

			float distanceToItem = Vector3.Distance(Camera.main.transform.position, currentTargetItem.lastKnownPosition);
			// Direction UX improvement: replace rotating 3D indicator with simple text guidance.
			string directionText = GetDirectionGuidanceText();

			// Draw every frame so the guidance line stays visible while in find mode.
			Debug.DrawLine(Camera.main.transform.position, currentTargetItem.lastKnownPosition, Color.red);
			if (showDistanceText && distanceText != null)
			{
				distanceText.text = "Distance: " + distanceToItem.ToString("F1") + "m\n" + directionText;
			}

			if (hudArrowText != null)
			{
				hudArrowText.text = GetDirectionArrowSymbol(directionText);
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
			hudArrowText.transform.localPosition = hudArrowLocalOffset;
			hudArrowText.transform.localRotation = Quaternion.identity;
		}
		hudArrowText.fontSize = 72;
		hudArrowText.characterSize = 0.012f;
		hudArrowText.anchor = TextAnchor.MiddleCenter;
		hudArrowText.alignment = TextAlignment.Center;
		hudArrowText.color = Color.white;
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

	private void UpdateHudArrowTextTransform()
	{
		if (hudArrowText == null || Camera.main == null)
		{
			return;
		}

		if (hudArrowText.transform.parent != Camera.main.transform)
		{
			hudArrowText.transform.SetParent(Camera.main.transform, false);
		}

		hudArrowText.transform.localPosition = hudArrowLocalOffset;
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
		directionToTarget.y = 0f;

		if (directionToTarget.sqrMagnitude < 0.0001f)
		{
			return "Ahead";
		}

		directionToTarget.Normalize();

		Vector3 cameraForward = Camera.main.transform.forward;
		cameraForward.y = 0f;
		if (cameraForward.sqrMagnitude < 0.0001f)
		{
			return "Ahead";
		}
		cameraForward.Normalize();

		Vector3 cameraRight = Camera.main.transform.right;
		cameraRight.y = 0f;
		if (cameraRight.sqrMagnitude < 0.0001f)
		{
			return "Ahead";
		}
		cameraRight.Normalize();

		float forwardDot = Vector3.Dot(cameraForward, directionToTarget);
		float rightDot = Vector3.Dot(cameraRight, directionToTarget);

		// Direction UX improvement: dead zone keeps guidance stable and easy to read.
		if (forwardDot > 0.35f || Mathf.Abs(rightDot) < 0.2f)
		{
			return "Ahead";
		}

		if (rightDot < 0f)
		{
			return "Turn Left";
		}

		return "Turn Right";
	}

	private string GetDirectionArrowSymbol(string directionText)
	{
		if (directionText == "Turn Left")
		{
			return "\u2190";
		}

		if (directionText == "Turn Right")
		{
			return "\u2192";
		}

		return "\u2191";
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

		ClearSpawnedMarkers();
	}

	private void SpawnMarkerForItem(SavedItemData item)
	{
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		// Temporary XR runtime material fix for target marker primitive.
		if (targetMarkerMaterial != null)
		{
			Renderer renderer = marker.GetComponent<Renderer>();
			if (renderer != null)
			{
				renderer.sharedMaterial = targetMarkerMaterial;
			}
		}
		marker.transform.SetParent(spawnedMarkersParent, true);
		marker.transform.position = item.lastKnownPosition;
		marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
		marker.name = item.itemName + " Marker";

		spawnedMarkers.Add(marker);
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
