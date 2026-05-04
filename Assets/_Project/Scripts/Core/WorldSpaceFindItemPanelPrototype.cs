using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

public class WorldSpaceFindItemPanelPrototype : MonoBehaviour
{
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
	[SerializeField] private Canvas worldSpaceCanvas;
	[SerializeField] private bool autoCreateCanvasIfMissing = true;
	[SerializeField] private Vector3 canvasLocalPosition = new Vector3(0f, 0.04f, 1.15f);
	[SerializeField] private Vector3 canvasLocalEulerAngles = Vector3.zero;
	[SerializeField] private Vector3 canvasLocalScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

	private RectTransform contentRoot;
	private Text statusText;
	private readonly List<GameObject> dynamicItemButtons = new List<GameObject>();
	private static readonly Color normalButtonColor = new Color(0.18f, 0.22f, 0.3f, 1f);
	private static readonly Color hoverButtonColor = new Color(0.28f, 0.35f, 0.52f, 1f);
	private static readonly Color pressedButtonColor = new Color(0.12f, 0.16f, 0.24f, 1f);

	private void Awake()
	{
		if (savedItemFinderExample == null)
		{
			savedItemFinderExample = FindFirstObjectByType<SavedItemFinderExample>();
		}

		EnsureEventSystemWithXrUiInputModule();

		if (worldSpaceCanvas == null && autoCreateCanvasIfMissing)
		{
			CreatePrototypeCanvas();
		}

		if (worldSpaceCanvas != null)
		{
			EnsureCanvasIsWorldSpace(worldSpaceCanvas);
			EnsureTrackedDeviceGraphicRaycaster(worldSpaceCanvas);
			if (contentRoot == null)
			{
				BuildPanel(worldSpaceCanvas.transform);
			}

			worldSpaceCanvas.gameObject.SetActive(false);
		}
	}

	private void OnEnable()
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetUseWorldSpaceFindItemPanelPrototypeEnabled(true);
		}
	}

	private void OnDisable()
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetUseWorldSpaceFindItemPanelPrototypeEnabled(false);
		}

		if (worldSpaceCanvas != null)
		{
			worldSpaceCanvas.gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		if (savedItemFinderExample == null || worldSpaceCanvas == null)
		{
			return;
		}

		bool shouldShow = savedItemFinderExample.IsFindSelectingMode() && savedItemFinderExample.IsWorldSpaceFindItemPanelPrototypeEnabled();
		if (worldSpaceCanvas.gameObject.activeSelf != shouldShow)
		{
			worldSpaceCanvas.gameObject.SetActive(shouldShow);
			if (shouldShow)
			{
				RefreshItemButtons();
			}
		}

		if (shouldShow)
		{
			UpdateCanvasPose();
		}
	}

	private void EnsureEventSystemWithXrUiInputModule()
	{
		EventSystem eventSystem = FindFirstObjectByType<EventSystem>();
		if (eventSystem == null)
		{
			GameObject eventSystemObject = new GameObject("EventSystem");
			eventSystem = eventSystemObject.AddComponent<EventSystem>();
		}

		if (eventSystem.GetComponent<XRUIInputModule>() == null)
		{
			eventSystem.gameObject.AddComponent<XRUIInputModule>();
		}
	}

	private void CreatePrototypeCanvas()
	{
		GameObject canvasObject = new GameObject("WorldSpaceFindItemCanvasPrototype");
		worldSpaceCanvas = canvasObject.AddComponent<Canvas>();
		canvasObject.AddComponent<CanvasScaler>();
		canvasObject.AddComponent<GraphicRaycaster>();
		EnsureCanvasIsWorldSpace(worldSpaceCanvas);
		EnsureTrackedDeviceGraphicRaycaster(worldSpaceCanvas);
		UpdateCanvasPose();

		BuildPanel(canvasObject.transform);
	}

	private void EnsureCanvasIsWorldSpace(Canvas canvas)
	{
		canvas.renderMode = RenderMode.WorldSpace;
		RectTransform canvasRect = canvas.GetComponent<RectTransform>();
		canvasRect.sizeDelta = new Vector2(900f, 620f);
		UpdateCanvasPose();
	}

	private void EnsureTrackedDeviceGraphicRaycaster(Canvas canvas)
	{
		if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
		{
			canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
		}
	}

	private void UpdateCanvasPose()
	{
		if (worldSpaceCanvas == null || Camera.main == null)
		{
			return;
		}

		Transform canvasTransform = worldSpaceCanvas.transform;
		if (canvasTransform.parent != Camera.main.transform)
		{
			canvasTransform.SetParent(Camera.main.transform, false);
		}

		canvasTransform.localPosition = canvasLocalPosition;
		canvasTransform.localRotation = Quaternion.Euler(canvasLocalEulerAngles);
		canvasTransform.localScale = canvasLocalScale;
	}

	private void BuildPanel(Transform canvasTransform)
	{
		GameObject panelObject = CreateUiObject("Panel", canvasTransform);
		Image panelImage = panelObject.AddComponent<Image>();
		panelImage.color = new Color(0.05f, 0.05f, 0.05f, 0.82f);

		RectTransform panelRect = panelObject.GetComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.5f, 0.5f);
		panelRect.anchorMax = new Vector2(0.5f, 0.5f);
		panelRect.pivot = new Vector2(0.5f, 0.5f);
		panelRect.sizeDelta = new Vector2(860f, 580f);
		panelRect.anchoredPosition = Vector2.zero;

		VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.spacing = 10f;
		layout.padding = new RectOffset(24, 24, 20, 20);
		layout.childControlHeight = true;
		layout.childControlWidth = true;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;

		CreateHeader(panelRect, "Find Item");
		CreateStatusText(panelRect);
		CreateContentRoot(panelRect);
		CreateCloseButton(panelRect);
	}

	private void CreateHeader(Transform parent, string text)
	{
		GameObject headerObject = CreateUiObject("Header", parent);
		LayoutElement headerLayout = headerObject.AddComponent<LayoutElement>();
		headerLayout.preferredHeight = 64f;

		Text headerText = headerObject.AddComponent<Text>();
		headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		headerText.text = text;
		headerText.color = Color.white;
		headerText.alignment = TextAnchor.MiddleCenter;
		headerText.fontSize = 30;
	}

	private void CreateStatusText(Transform parent)
	{
		GameObject statusObject = CreateUiObject("StatusText", parent);
		LayoutElement statusLayout = statusObject.AddComponent<LayoutElement>();
		statusLayout.preferredHeight = 44f;

		statusText = statusObject.AddComponent<Text>();
		statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		statusText.text = "Select an item to find";
		statusText.color = Color.white;
		statusText.alignment = TextAnchor.MiddleCenter;
		statusText.fontSize = 22;
	}

	private void CreateContentRoot(Transform parent)
	{
		GameObject contentObject = CreateUiObject("ContentRoot", parent);
		LayoutElement contentLayout = contentObject.AddComponent<LayoutElement>();
		contentLayout.preferredHeight = 360f;
		contentLayout.flexibleHeight = 1f;

		VerticalLayoutGroup contentLayoutGroup = contentObject.AddComponent<VerticalLayoutGroup>();
		contentLayoutGroup.spacing = 8f;
		contentLayoutGroup.childControlHeight = true;
		contentLayoutGroup.childControlWidth = true;
		contentLayoutGroup.childForceExpandHeight = false;
		contentLayoutGroup.childForceExpandWidth = true;

		contentRoot = contentObject.GetComponent<RectTransform>();
	}

	private void CreateCloseButton(Transform parent)
	{
		// Canvas find item menu prototype: give the close row a fixed height and expand width so "Back" renders horizontally.
		GameObject closeRowObject = CreateUiObject("CloseRow", parent);
		LayoutElement closeLayout = closeRowObject.AddComponent<LayoutElement>();
		closeLayout.preferredHeight = 84f;
		closeLayout.minHeight = 84f;

		HorizontalLayoutGroup closeRowGroup = closeRowObject.AddComponent<HorizontalLayoutGroup>();
		closeRowGroup.childAlignment = TextAnchor.MiddleCenter;
		closeRowGroup.childControlHeight = true;
		closeRowGroup.childControlWidth = true;
		closeRowGroup.childForceExpandHeight = true;
		closeRowGroup.childForceExpandWidth = true;
		closeRowGroup.padding = new RectOffset(120, 120, 0, 0);

		CreateButton(closeRowObject.transform, "Back", () =>
		{
			if (savedItemFinderExample != null)
			{
				// Canvas find item menu prototype
				savedItemFinderExample.CancelFindSelectionMenu();
			}
		});
	}

	private void RefreshItemButtons()
	{
		ClearDynamicButtons();

		if (savedItemFinderExample == null || contentRoot == null)
		{
			return;
		}

		List<string> itemNames = savedItemFinderExample.GetSelectableItemNamesForFindMenu();
		if (itemNames.Count == 0)
		{
			statusText.text = "No saved items";
			return;
		}

		statusText.text = "Select an item to find";
		for (int i = 0; i < itemNames.Count; i++)
		{
			string itemName = itemNames[i];
			CreateItemButton(itemName);
		}
	}

	private void CreateItemButton(string itemName)
	{
		// Canvas find item button action fix: copy into a local so each button closure captures its own name.
		string capturedName = itemName;
		GameObject buttonObject = CreateButton(contentRoot, itemName, () =>
		{
			if (savedItemFinderExample != null)
			{
				// Canvas find item button action fix
				savedItemFinderExample.StartFindModeForItemName(capturedName);
			}
		});
		dynamicItemButtons.Add(buttonObject);
	}

	private GameObject CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
	{
		GameObject buttonObject = CreateUiObject("Button_" + label.Replace(" ", string.Empty), parent);
		Image buttonImage = buttonObject.AddComponent<Image>();
		buttonImage.color = normalButtonColor;

		Button button = buttonObject.AddComponent<Button>();
		ColorBlock colors = button.colors;
		colors.normalColor = normalButtonColor;
		colors.highlightedColor = hoverButtonColor;
		colors.pressedColor = pressedButtonColor;
		colors.selectedColor = hoverButtonColor;
		button.colors = colors;
		button.onClick.AddListener(action);

		LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
		buttonLayout.minHeight = 74f;
		buttonLayout.preferredHeight = 74f;

		GameObject labelObject = CreateUiObject("Label", buttonObject.transform);
		RectTransform labelRect = labelObject.GetComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = Vector2.zero;
		labelRect.offsetMax = Vector2.zero;

		Text labelText = labelObject.AddComponent<Text>();
		labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		labelText.text = label;
		labelText.alignment = TextAnchor.MiddleCenter;
		labelText.color = Color.white;
		labelText.fontSize = 24;
		// Canvas find item menu prototype: prevent any button label from wrapping to vertical text.
		labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
		labelText.verticalOverflow = VerticalWrapMode.Overflow;

		return buttonObject;
	}

	private void ClearDynamicButtons()
	{
		for (int i = 0; i < dynamicItemButtons.Count; i++)
		{
			if (dynamicItemButtons[i] != null)
			{
				Destroy(dynamicItemButtons[i]);
			}
		}

		dynamicItemButtons.Clear();
	}

	private GameObject CreateUiObject(string name, Transform parent)
	{
		GameObject uiObject = new GameObject(name, typeof(RectTransform));
		uiObject.transform.SetParent(parent, false);
		return uiObject;
	}
}
