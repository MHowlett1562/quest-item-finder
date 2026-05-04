using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Collections.Generic;

public class WorldSpaceSettingsPanelPrototype : MonoBehaviour
{
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
	[SerializeField] private Canvas worldSpaceCanvas;
	[SerializeField] private bool autoCreateCanvasIfMissing = true;
	[SerializeField] private Vector3 canvasLocalPosition = new Vector3(0f, -0.14f, 1.15f);
	[SerializeField] private Vector3 canvasLocalEulerAngles = Vector3.zero;
	[SerializeField] private Vector3 canvasLocalScale = new Vector3(0.0012f, 0.0012f, 0.0012f);

	private RectTransform buttonPanelRoot;
	// Canvas settings selected-state polish: keep buttons mapped so active values can repaint blue.
	private readonly Dictionary<SettingButtonKey, Button> settingButtons = new Dictionary<SettingButtonKey, Button>();
	private static readonly Color normalButtonColor = new Color(0.18f, 0.22f, 0.3f, 1f);
	private static readonly Color hoverButtonColor = new Color(0.28f, 0.35f, 0.52f, 1f);
	private static readonly Color pressedButtonColor = new Color(0.12f, 0.16f, 0.24f, 1f);
	private static readonly Color activeButtonColor = new Color(0.2f, 0.45f, 0.9f, 1f);

	private enum SettingButtonKey
	{
		DistanceOn,
		DistanceOff,
		Metric,
		Imperial,
		AudioOn,
		AudioOff,
		Volume0,
		Volume25,
		Volume50,
		Volume75,
		Volume100
	}

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
			if (buttonPanelRoot == null)
			{
				BuildButtonPanel(worldSpaceCanvas.transform);
			}

			UpdateSelectedButtonVisuals();
			worldSpaceCanvas.gameObject.SetActive(false);
		}
	}

	private void OnEnable()
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetUseWorldSpaceCanvasSettingsPanelPrototypeEnabled(true);
		}

		UpdateSelectedButtonVisuals();
	}

	private void OnDisable()
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetUseWorldSpaceCanvasSettingsPanelPrototypeEnabled(false);
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

		bool shouldShow = savedItemFinderExample.IsSettingsMode() && savedItemFinderExample.IsWorldSpaceCanvasSettingsPanelPrototypeEnabled();
		if (worldSpaceCanvas.gameObject.activeSelf != shouldShow)
		{
			worldSpaceCanvas.gameObject.SetActive(shouldShow);
		}

		if (shouldShow)
		{
			UpdateCanvasPose();
			UpdateSelectedButtonVisuals();
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
		GameObject canvasObject = new GameObject("WorldSpaceSettingsCanvasPrototype");
		worldSpaceCanvas = canvasObject.AddComponent<Canvas>();
		canvasObject.AddComponent<CanvasScaler>();
		canvasObject.AddComponent<GraphicRaycaster>();
		EnsureCanvasIsWorldSpace(worldSpaceCanvas);
		EnsureTrackedDeviceGraphicRaycaster(worldSpaceCanvas);
		UpdateCanvasPose();

		BuildButtonPanel(canvasObject.transform);
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

	private void BuildButtonPanel(Transform canvasTransform)
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

		buttonPanelRoot = panelRect;

		CreateHeader(panelRect, "Settings Prototype (World Space Canvas)");
		CreateTwoButtonRow(panelRect, "Distance On", SettingButtonKey.DistanceOn, () => ApplyDistance(true), "Distance Off", SettingButtonKey.DistanceOff, () => ApplyDistance(false));
		CreateTwoButtonRow(panelRect, "Metric", SettingButtonKey.Metric, () => ApplyUnits(false), "Imperial", SettingButtonKey.Imperial, () => ApplyUnits(true));
		CreateTwoButtonRow(panelRect, "Audio On", SettingButtonKey.AudioOn, () => ApplyAudio(true), "Audio Off", SettingButtonKey.AudioOff, () => ApplyAudio(false));
		CreateVolumeRow(panelRect);
		UpdateSelectedButtonVisuals();
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

	private void CreateTwoButtonRow(Transform parent, string leftLabel, SettingButtonKey leftKey, UnityEngine.Events.UnityAction leftAction, string rightLabel, SettingButtonKey rightKey, UnityEngine.Events.UnityAction rightAction)
	{
		GameObject rowObject = CreateUiObject("Row_" + leftLabel.Replace(" ", string.Empty), parent);
		LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
		rowLayout.preferredHeight = 88f;

		HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
		rowGroup.spacing = 10f;
		rowGroup.childControlHeight = true;
		rowGroup.childControlWidth = true;
		rowGroup.childForceExpandHeight = true;
		rowGroup.childForceExpandWidth = true;

		CreateButton(rowObject.transform, leftLabel, leftKey, leftAction);
		CreateButton(rowObject.transform, rightLabel, rightKey, rightAction);
	}

	private void CreateVolumeRow(Transform parent)
	{
		GameObject rowObject = CreateUiObject("Row_Volume", parent);
		LayoutElement rowLayout = rowObject.AddComponent<LayoutElement>();
		rowLayout.preferredHeight = 88f;

		HorizontalLayoutGroup rowGroup = rowObject.AddComponent<HorizontalLayoutGroup>();
		rowGroup.spacing = 8f;
		rowGroup.childControlHeight = true;
		rowGroup.childControlWidth = true;
		rowGroup.childForceExpandHeight = true;
		rowGroup.childForceExpandWidth = true;

		CreateButton(rowObject.transform, "Vol 0", SettingButtonKey.Volume0, () => ApplyVolume(0f));
		CreateButton(rowObject.transform, "Vol 25", SettingButtonKey.Volume25, () => ApplyVolume(0.25f));
		CreateButton(rowObject.transform, "Vol 50", SettingButtonKey.Volume50, () => ApplyVolume(0.5f));
		CreateButton(rowObject.transform, "Vol 75", SettingButtonKey.Volume75, () => ApplyVolume(0.75f));
		CreateButton(rowObject.transform, "Vol 100", SettingButtonKey.Volume100, () => ApplyVolume(1f));
	}

	private void CreateButton(Transform parent, string label, SettingButtonKey buttonKey, UnityEngine.Events.UnityAction action)
	{
		GameObject buttonObject = CreateUiObject("Button_" + label.Replace(" ", string.Empty), parent);
		Image buttonImage = buttonObject.AddComponent<Image>();
		buttonImage.color = normalButtonColor;

		Button button = buttonObject.AddComponent<Button>();
		ApplyButtonColorState(button, false);
		button.onClick.AddListener(() =>
		{
			action();
			// Canvas settings selected-state polish: refresh active blue states immediately after click.
			UpdateSelectedButtonVisuals();
		});
		settingButtons[buttonKey] = button;

		LayoutElement buttonLayout = buttonObject.AddComponent<LayoutElement>();
		buttonLayout.minHeight = 78f;
		buttonLayout.preferredHeight = 78f;

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
		labelText.fontSize = 26;
	}

	// Canvas settings selected-state polish
	private void UpdateSelectedButtonVisuals()
	{
		if (savedItemFinderExample == null || settingButtons.Count == 0)
		{
			return;
		}

		foreach (KeyValuePair<SettingButtonKey, Button> entry in settingButtons)
		{
			if (entry.Value == null)
			{
				continue;
			}

			ApplyButtonColorState(entry.Value, IsButtonSelected(entry.Key));
		}
	}

	// Canvas settings selected-state polish
	private void ApplyButtonColorState(Button button, bool isSelected)
	{
		ColorBlock colors = button.colors;
		colors.normalColor = isSelected ? activeButtonColor : normalButtonColor;
		colors.highlightedColor = hoverButtonColor;
		colors.pressedColor = pressedButtonColor;
		colors.selectedColor = hoverButtonColor;
		button.colors = colors;

		if (button.image != null)
		{
			button.image.color = colors.normalColor;
		}
	}

	// Canvas settings selected-state polish
	private bool IsButtonSelected(SettingButtonKey buttonKey)
	{
		switch (buttonKey)
		{
			case SettingButtonKey.DistanceOn:
				return savedItemFinderExample.IsDistanceTextEnabled();

			case SettingButtonKey.DistanceOff:
				return !savedItemFinderExample.IsDistanceTextEnabled();

			case SettingButtonKey.Metric:
				return !savedItemFinderExample.IsUsingImperialUnits();

			case SettingButtonKey.Imperial:
				return savedItemFinderExample.IsUsingImperialUnits();

			case SettingButtonKey.AudioOn:
				return savedItemFinderExample.IsProximityAudioEnabled();

			case SettingButtonKey.AudioOff:
				return !savedItemFinderExample.IsProximityAudioEnabled();

			case SettingButtonKey.Volume0:
				return IsCurrentVolume(0f);

			case SettingButtonKey.Volume25:
				return IsCurrentVolume(0.25f);

			case SettingButtonKey.Volume50:
				return IsCurrentVolume(0.5f);

			case SettingButtonKey.Volume75:
				return IsCurrentVolume(0.75f);

			case SettingButtonKey.Volume100:
				return IsCurrentVolume(1f);

			default:
				return false;
		}
	}

	private bool IsCurrentVolume(float expectedVolume)
	{
		return Mathf.Approximately(savedItemFinderExample.GetProximityAudioVolume(), expectedVolume);
	}

	private GameObject CreateUiObject(string name, Transform parent)
	{
		GameObject uiObject = new GameObject(name, typeof(RectTransform));
		uiObject.transform.SetParent(parent, false);
		return uiObject;
	}

	private void ApplyDistance(bool isEnabled)
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetDistanceText(isEnabled);
		}
	}

	private void ApplyUnits(bool useImperial)
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetUseImperialUnits(useImperial);
		}
	}

	private void ApplyAudio(bool isEnabled)
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetProximityAudio(isEnabled);
		}
	}

	private void ApplyVolume(float volume)
	{
		if (savedItemFinderExample != null)
		{
			savedItemFinderExample.SetProximityAudioVolume(volume);
		}
	}
}
