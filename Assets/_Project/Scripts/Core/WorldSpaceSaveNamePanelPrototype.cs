using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

// Canvas save name menu prototype: World Space Canvas preset-name picker shown during AppMode.SaveNaming.
public class WorldSpaceSaveNamePanelPrototype : MonoBehaviour
{
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
	[SerializeField] private SavedItemExample savedItemExample;
	[SerializeField] private Canvas worldSpaceCanvas;
	[SerializeField] private bool autoCreateCanvasIfMissing = true;
	[SerializeField] private Vector3 canvasLocalPosition = new Vector3(0f, 0.04f, 1.15f);
	[SerializeField] private Vector3 canvasLocalEulerAngles = Vector3.zero;
	[SerializeField] private Vector3 canvasLocalScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

	// Canvas save name menu prototype: names shown as buttons in the panel.
	private static readonly string[] presetNames = { "Keys", "Wallet", "Remote", "Phone", "Glasses", "Backpack" };

	private readonly List<GameObject> presetButtons = new List<GameObject>();

	private static readonly Color normalButtonColor  = new Color(0.18f, 0.22f, 0.3f,  1f);
	private static readonly Color hoverButtonColor   = new Color(0.28f, 0.35f, 0.52f, 1f);
	private static readonly Color pressedButtonColor = new Color(0.12f, 0.16f, 0.24f, 1f);

	private void Awake()
	{
		if (savedItemFinderExample == null)
		{
			savedItemFinderExample = FindFirstObjectByType<SavedItemFinderExample>();
		}

		if (savedItemExample == null)
		{
			savedItemExample = FindFirstObjectByType<SavedItemExample>();
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
			BuildPanel(worldSpaceCanvas.transform);
			worldSpaceCanvas.gameObject.SetActive(false);
		}
	}

	private void OnEnable()
	{
		// Canvas save name menu prototype: register so SavedItemExample knows the canvas panel is active.
		if (savedItemExample != null)
		{
			savedItemExample.SetUseWorldSpaceCanvasSaveNamePanelPrototypeEnabled(true);
		}
	}

	private void OnDisable()
	{
		// Canvas save name menu prototype: deregister when this component is disabled.
		if (savedItemExample != null)
		{
			savedItemExample.SetUseWorldSpaceCanvasSaveNamePanelPrototypeEnabled(false);
		}

		if (worldSpaceCanvas != null)
		{
			worldSpaceCanvas.gameObject.SetActive(false);
		}
	}

	private void Update()
	{
		if (savedItemFinderExample == null || savedItemExample == null || worldSpaceCanvas == null)
		{
			return;
		}

		// Canvas save name menu prototype: show only while save naming mode is active and this panel is enabled.
		bool shouldShow = savedItemFinderExample.IsSaveMode()
		               && savedItemExample.IsWorldSpaceCanvasSaveNamePanelPrototypeEnabled();

		if (worldSpaceCanvas.gameObject.activeSelf != shouldShow)
		{
			worldSpaceCanvas.gameObject.SetActive(shouldShow);
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
		GameObject canvasObject = new GameObject("WorldSpaceSaveNameCanvasPrototype");
		worldSpaceCanvas = canvasObject.AddComponent<Canvas>();
		canvasObject.AddComponent<CanvasScaler>();
		canvasObject.AddComponent<GraphicRaycaster>();
		EnsureCanvasIsWorldSpace(worldSpaceCanvas);
		EnsureTrackedDeviceGraphicRaycaster(worldSpaceCanvas);
		UpdateCanvasPose();
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
		panelRect.pivot    = new Vector2(0.5f, 0.5f);
		panelRect.sizeDelta = new Vector2(860f, 580f);
		panelRect.anchoredPosition = Vector2.zero;

		VerticalLayoutGroup layout = panelObject.AddComponent<VerticalLayoutGroup>();
		layout.childAlignment = TextAnchor.UpperCenter;
		layout.spacing = 8f;
		layout.padding = new RectOffset(24, 24, 20, 20);
		layout.childControlHeight = true;
		layout.childControlWidth = true;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = true;

		CreateHeader(panelRect, "Save Item");
		CreateSubtitle(panelRect, "Choose a name");
		CreatePresetButtons(panelRect);
		CreateBackButton(panelRect);
	}

	private void CreateHeader(Transform parent, string text)
	{
		GameObject headerObject = CreateUiObject("Header", parent);
		LayoutElement headerLayout = headerObject.AddComponent<LayoutElement>();
		headerLayout.preferredHeight = 60f;

		Text headerText = headerObject.AddComponent<Text>();
		headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		headerText.text = text;
		headerText.color = Color.white;
		headerText.alignment = TextAnchor.MiddleCenter;
		headerText.fontSize = 30;
		headerText.horizontalOverflow = HorizontalWrapMode.Overflow;
		headerText.verticalOverflow = VerticalWrapMode.Overflow;
	}

	private void CreateSubtitle(Transform parent, string text)
	{
		GameObject subtitleObject = CreateUiObject("Subtitle", parent);
		LayoutElement subtitleLayout = subtitleObject.AddComponent<LayoutElement>();
		subtitleLayout.preferredHeight = 36f;

		Text subtitleText = subtitleObject.AddComponent<Text>();
		subtitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
		subtitleText.text = text;
		subtitleText.color = new Color(0.75f, 0.75f, 0.75f, 1f);
		subtitleText.alignment = TextAnchor.MiddleCenter;
		subtitleText.fontSize = 22;
		subtitleText.horizontalOverflow = HorizontalWrapMode.Overflow;
		subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
	}

	private void CreatePresetButtons(Transform parent)
	{
		// Canvas save name menu prototype: one button per preset name; closure captures its own name.
		for (int i = 0; i < presetNames.Length; i++)
		{
			string capturedName = presetNames[i];
			GameObject buttonObject = CreateButton(parent, capturedName, () =>
			{
				// Canvas save name menu prototype: save the item with the clicked preset name.
				if (savedItemExample != null)
				{
					savedItemExample.SaveItemWithName(capturedName);
				}
			});
			presetButtons.Add(buttonObject);
		}
	}

	private void CreateBackButton(Transform parent)
	{
		// Canvas save name menu prototype: Back button cancels save naming without saving.
		GameObject backRowObject = CreateUiObject("BackRow", parent);
		LayoutElement backLayout = backRowObject.AddComponent<LayoutElement>();
		backLayout.preferredHeight = 72f;
		backLayout.minHeight = 72f;

		HorizontalLayoutGroup backRowGroup = backRowObject.AddComponent<HorizontalLayoutGroup>();
		backRowGroup.childAlignment = TextAnchor.MiddleCenter;
		backRowGroup.childControlHeight = true;
		backRowGroup.childControlWidth = true;
		backRowGroup.childForceExpandHeight = true;
		backRowGroup.childForceExpandWidth = true;
		backRowGroup.padding = new RectOffset(120, 120, 0, 0);

		CreateButton(backRowObject.transform, "Back", () =>
		{
			// Canvas save name menu prototype: cancel without saving; hide aim marker and return to Neutral.
			if (savedItemExample != null)
			{
				savedItemExample.CancelNameSelectionMenu();
			}
		});
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
		buttonLayout.minHeight = 60f;
		buttonLayout.preferredHeight = 60f;

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
		// Canvas save name menu prototype: prevent button labels from wrapping to vertical text.
		labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
		labelText.verticalOverflow = VerticalWrapMode.Overflow;

		return buttonObject;
	}

	private GameObject CreateUiObject(string objectName, Transform parent)
	{
		GameObject uiObject = new GameObject(objectName);
		uiObject.transform.SetParent(parent, false);
		uiObject.AddComponent<RectTransform>();
		return uiObject;
	}
}
