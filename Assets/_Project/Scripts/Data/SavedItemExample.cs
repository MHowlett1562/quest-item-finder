using UnityEngine;

public class SavedItemExample : MonoBehaviour
{
    private void Start()
    {
        SavedItemData savedItem = new SavedItemData();

        savedItem.itemId = "item-001";
        savedItem.itemName = "Keys";
        savedItem.lastKnownPosition = new Vector3(1.5f, 0.8f, -2.0f);
        savedItem.savedAtUtc = System.DateTime.UtcNow.ToString("o");

        Debug.Log("Saved item: " + savedItem.itemName);
        Debug.Log("Item ID: " + savedItem.itemId);
        Debug.Log("Position: " + savedItem.lastKnownPosition);
        Debug.Log("Saved at: " + savedItem.savedAtUtc);
    }
}