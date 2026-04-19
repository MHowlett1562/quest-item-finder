using UnityEngine;

public class SavedItemFinderExample : MonoBehaviour
{
	private SavedItemManager savedItemManager;

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

		SavedItemData item = savedItemManager.GetItemByName("Keys");

		if (item != null)
		{
			GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			marker.transform.position = item.lastKnownPosition;
			marker.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
			Debug.Log("Marker created for Keys.");
		}
		else
		{
			Debug.Log("No item named Keys was found.");
		}
	}
}
