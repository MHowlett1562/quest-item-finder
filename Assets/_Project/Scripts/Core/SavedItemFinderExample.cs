using UnityEngine;
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
	private GameObject directionalIndicator;
	[SerializeField] private string testItemName = "Keys";
	// Temporary XR visual debugging toggles (Inspector) to isolate discomfort sources.
	[SerializeField] private bool showDistanceText = true;
	[SerializeField] private bool showDirectionalIndicator = false;
	[SerializeField] private bool showTargetMarker = false;
	private bool wasLeftTriggerPressed;
	private bool wasRightPrimaryButtonPressed;
	// Find Mode toggle UX improvement: true while single-item find mode is actively guiding to one item.
	private bool isSingleItemFindModeActive;
	private static readonly Vector3 distanceTextLocalOffset = new Vector3(0f, -0.15f, 1.5f);
	private static readonly Vector3 hudArrowLocalOffset = new Vector3(0f, -0.06f, 1.5f);
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
		InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
		if (rightHandDevice.isValid)
		{
			rightHandDevice.TryGetFeatureValue(CommonUsages.primaryButton, out rightPrimaryButtonPressed);
		}

		bool leftTriggerPressedThisFrame = leftTriggerPressed && !wasLeftTriggerPressed;
		bool rightPrimaryButtonPressedThisFrame = rightPrimaryButtonPressed && !wasRightPrimaryButtonPressed;

		wasLeftTriggerPressed = leftTriggerPressed;
		wasRightPrimaryButtonPressed = rightPrimaryButtonPressed;

		if (Input.GetKeyDown(KeyCode.F) || rightPrimaryButtonPressedThisFrame)
		{
			currentTargetItem = null;
			SpawnAllSavedItems();
		}
		else if (Input.GetKeyDown(KeyCode.G) || leftTriggerPressedThisFrame)
		{
			// Find Mode toggle UX improvement: G/left trigger now toggles single-item find mode on/off.
			if (!isSingleItemFindModeActive)
			{
				SpawnOneSavedItemByName(testItemName);
				isSingleItemFindModeActive = currentTargetItem != null;
			}
			else
			{
				DisableSingleItemFindMode();
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
