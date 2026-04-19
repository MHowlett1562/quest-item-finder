using UnityEngine;

public class SavedItemExample : MonoBehaviour
{
    private void Start()
    {
        SavedItemManager savedItemManager = FindFirstObjectByType<SavedItemManager>();

        if (savedItemManager != null)
        {
            savedItemManager.LoadData();
            savedItemManager.LogAllItems();
        }

        SavedItemData savedItem = new SavedItemData();

        savedItem.itemId = "item-001";
        savedItem.itemName = "Keys";
        savedItem.lastKnownPosition = new Vector3(1.5f, 0.8f, -2.0f);
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");

        if (savedItemManager != null)
        {
            savedItemManager.AddItem(savedItem);
            savedItemManager.SaveData();
        }
        else
        {
            Debug.Log("No SavedItemManager found in the scene.");
        }

        Debug.Log("Saved item: " + savedItem.itemName);
        Debug.Log("Item ID: " + savedItem.itemId);
        Debug.Log("Position: " + savedItem.lastKnownPosition);
        Debug.Log("Saved at: " + savedItem.savedAtUtc);
    }
}