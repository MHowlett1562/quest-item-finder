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
		Debug.Log("Added item: " + item.itemName);
	}

	public List<SavedItemData> GetAllItems()
	{
		return savedItems;
	}

	public SavedItemData GetItemByName(string itemName)
	{
		for (int i = 0; i < savedItems.Count; i++)
		{
			SavedItemData item = savedItems[i];

			if (item != null && item.itemName == itemName)
			{
				return item;
			}
		}

		return null;
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
