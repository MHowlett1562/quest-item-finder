using UnityEngine;
using System.Collections.Generic;

public class SavedItemManager : MonoBehaviour
{
	private List<SavedItemData> savedItems = new List<SavedItemData>();

	public void AddItem(SavedItemData item)
	{
		if (item == null)
		{
			return;
		}

		savedItems.Add(item);
		Debug.Log("Added item: " + item.itemName);
	}

	public List<SavedItemData> GetAllItems()
	{
		return savedItems;
	}
}
