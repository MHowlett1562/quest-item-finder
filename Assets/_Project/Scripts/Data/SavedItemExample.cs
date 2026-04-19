using UnityEngine;

public class SavedItemExample : MonoBehaviour
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
        if (!Input.GetKeyDown(KeyCode.K))
        {
            return;
        }

        if (savedItemManager == null)
        {
            Debug.Log("No SavedItemManager found in the scene.");
            return;
        }

        SavedItemData savedItem = new SavedItemData();
        savedItem.itemId = System.Guid.NewGuid().ToString();
        savedItem.itemName = "Keys";
        savedItem.lastKnownPosition = transform.position;
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");

        savedItemManager.AddItem(savedItem);
        savedItemManager.SaveData();

        Debug.Log("Saved item: " + savedItem.itemName);
    }
}