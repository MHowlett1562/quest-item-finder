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
	private GameObject directionalIndicator;
	[SerializeField] private string testItemName = "Keys";
	private bool wasLeftTriggerPressed;
	private bool wasRightPrimaryButtonPressed;
	private static readonly Vector3 distanceTextLocalOffset = new Vector3(0f, -0.15f, 1.5f);
	private static readonly Vector3 directionalIndicatorLocalOffset = new Vector3(0f, -0.08f, 1.2f);

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
			SpawnOneSavedItemByName(testItemName);
		}

		if (currentTargetItem != null && Camera.main != null)
		{
			EnsureDistanceText();
			distanceText.gameObject.SetActive(true);
			UpdateDistanceTextTransform();
			EnsureDirectionalIndicator();
			directionalIndicator.SetActive(true);
			UpdateDirectionalIndicatorTransform();

			float distanceToItem = Vector3.Distance(Camera.main.transform.position, currentTargetItem.lastKnownPosition);

			// Draw every frame so the guidance line stays visible while in find mode.
			Debug.DrawLine(Camera.main.transform.position, currentTargetItem.lastKnownPosition, Color.red);
			distanceText.text = "Distance: " + distanceToItem.ToString("F2") + " m";

			if (Time.time >= nextDistanceLogTime)
			{
				Debug.Log("Distance to selected item: " + distanceToItem.ToString("F2") + " meters");
				nextDistanceLogTime = Time.time + 1f;
			}
		}
		else if (distanceText != null)
		{
			distanceText.gameObject.SetActive(false);

			if (directionalIndicator != null)
			{
				directionalIndicator.SetActive(false);
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

	private void EnsureDirectionalIndicator()
	{
		if (directionalIndicator != null)
		{
			return;
		}

		directionalIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
		directionalIndicator.name = "DirectionalIndicator";
		if (Camera.main != null)
		{
			directionalIndicator.transform.SetParent(Camera.main.transform, false);
			directionalIndicator.transform.localPosition = directionalIndicatorLocalOffset;
		}
		directionalIndicator.transform.localScale = new Vector3(0.05f, 0.05f, 0.15f);
	}

	private void UpdateDirectionalIndicatorTransform()
	{
		if (directionalIndicator == null || currentTargetItem == null || Camera.main == null)
		{
			return;
		}

		if (directionalIndicator.transform.parent != Camera.main.transform)
		{
			directionalIndicator.transform.SetParent(Camera.main.transform, false);
		}

		directionalIndicator.transform.localPosition = directionalIndicatorLocalOffset;

		Vector3 directionToTarget = currentTargetItem.lastKnownPosition - directionalIndicator.transform.position;
		if (directionToTarget.sqrMagnitude > 0.0001f)
		{
			directionalIndicator.transform.rotation = Quaternion.LookRotation(directionToTarget.normalized);
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

		for (int i = 0; i < items.Count; i++)
		{
			SavedItemData item = items[i];
			SpawnMarkerForItem(item);
		}

		Debug.Log("Spawned " + spawnedMarkers.Count + " marker(s).");
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

		SpawnMarkerForItem(item);
		Debug.Log("Spawned marker for item: " + item.itemName);
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

	private void SpawnMarkerForItem(SavedItemData item)
	{
		GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
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
