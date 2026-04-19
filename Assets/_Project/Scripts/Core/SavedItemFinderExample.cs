using UnityEngine;
using System.Collections.Generic;

public class SavedItemFinderExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;
	private List<GameObject> spawnedMarkers = new List<GameObject>();

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
		if (!Input.GetKeyDown(KeyCode.F))
		{
			return;
		}

		if (savedItemManager == null)
		{
			Debug.Log("No SavedItemManager found in the scene.");
			return;
		}

		SpawnAllSavedItems();
	}

	private void SpawnAllSavedItems()
	{
		ClearSpawnedMarkers();

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

			GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			marker.transform.position = item.lastKnownPosition;
			marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
			marker.name = item.itemName + " Marker";

			spawnedMarkers.Add(marker);
		}

		Debug.Log("Spawned " + spawnedMarkers.Count + " marker(s).");
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
