using UnityEngine;

[System.Serializable]
public class SavedItemData
{
	public string itemId;
	public string itemName;
	public string persistentAnchorId;
	public Vector3 lastKnownPosition;
	public string savedAtUtc;
}
