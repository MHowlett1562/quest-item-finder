using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SavedItemManager : MonoBehaviour
{
	private List<SavedItemData> savedItems = new List<SavedItemData>();
	private string saveFileName = "saved_items.json";

	public void AddItem(SavedItemData item)
	{
		if (item == null)
		{
			return;
		}

		for (int i = 0; i < savedItems.Count; i++)
		{
			SavedItemData existingItem = savedItems[i];

			if (existingItem != null && existingItem.itemName == item.itemName)
			{
				Debug.LogWarning("Another item with the name '" + item.itemName + "' already exists.");
				break;
			}
		}

		savedItems.Add(item);
		Debug.Log("Added item: Name=" + item.itemName + ", Position=" + item.lastKnownPosition);
		Debug.Log("Total saved items: " + savedItems.Count);
	}

	public List<SavedItemData> GetAllItems()
	{
		return savedItems;
	}

	public SavedItemData GetItemByName(string itemName)
	{
		SavedItemData bestMatch = null;
		System.DateTime bestSavedTime = System.DateTime.MinValue;
		bool hasBestSavedTime = false;
		int matchCount = 0;

		for (int i = 0; i < savedItems.Count; i++)
		{
			SavedItemData item = savedItems[i];

			if (item != null && item.itemName == itemName)
			{
				matchCount++;

				if (bestMatch == null)
				{
					bestMatch = item;

					System.DateTime firstSavedTime;
					if (System.DateTime.TryParse(item.savedAtUtc, out firstSavedTime))
					{
						bestSavedTime = firstSavedTime;
						hasBestSavedTime = true;
					}

					continue;
				}

				System.DateTime currentSavedTime;
				bool hasCurrentSavedTime = System.DateTime.TryParse(item.savedAtUtc, out currentSavedTime);

				if (hasCurrentSavedTime && (!hasBestSavedTime || currentSavedTime > bestSavedTime))
				{
					bestMatch = item;
					bestSavedTime = currentSavedTime;
					hasBestSavedTime = true;
				}
			}
		}

		if (matchCount > 1 && bestMatch != null)
		{
			Debug.Log("Multiple items found with name '" + itemName + "'. Returning item: Id=" + bestMatch.itemId + ", SavedAtUtc=" + bestMatch.savedAtUtc);
		}

		return bestMatch;
	}

	public void LogAllItems()
	{
		if (savedItems == null || savedItems.Count == 0)
		{
			Debug.Log("No saved items.");
			return;
		}

		for (int i = 0; i < savedItems.Count; i++)
		{
			SavedItemData item = savedItems[i];

			if (item == null)
			{
				Debug.Log("Saved item " + i + ": null");
				continue;
			}

			Debug.Log("Saved item " + i + ": Name=" + item.itemName + ", Id=" + item.itemId + ", Position=" + item.lastKnownPosition + ", SavedAtUtc=" + item.savedAtUtc);
		}
	}

	public void ClearAllItems()
	{
		// Temporary MVP testing/debug cleanup feature: clear in-memory items and persist an empty save file.
		savedItems.Clear();
		SaveData();
		Debug.Log("All saved items were cleared.");
	}

	public void SaveData()
	{
		string filePath = Path.Combine(Application.persistentDataPath, saveFileName);
		SaveFileData saveFileData = new SaveFileData();
		saveFileData.items = new List<SavedItemData>(savedItems);

		string json = JsonUtility.ToJson(saveFileData, true);

		try
		{
			File.WriteAllText(filePath, json);
			Debug.Log("Save successful: " + filePath);
		}
		catch (System.Exception ex)
		{
			Debug.Log("Save failed: " + ex.Message);
		}
	}

	public void LoadData()
	{
		string filePath = Path.Combine(Application.persistentDataPath, saveFileName);

		if (!File.Exists(filePath))
		{
			Debug.Log("No save file found.");
			return;
		}

		try
		{
			string json = File.ReadAllText(filePath);
			SaveFileData saveFileData = JsonUtility.FromJson<SaveFileData>(json);

			if (saveFileData != null && saveFileData.items != null)
			{
				savedItems = saveFileData.items;
			}
			else
			{
				savedItems = new List<SavedItemData>();
			}

			Debug.Log("Load successful: " + filePath);
		}
		catch (System.Exception ex)
		{
			Debug.Log("Load failed: " + ex.Message);
		}
	}
}
