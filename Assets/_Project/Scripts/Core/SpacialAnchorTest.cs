using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SpacialAnchorTest : MonoBehaviour
{
	[SerializeField] private SceneUnderstandingTest sceneUnderstandingTest;
	[SerializeField] private ARAnchorManager arAnchorManager;
	[SerializeField] private Transform rightControllerTransform;
	[SerializeField] private bool enableAnchorTestInput = false;

	public bool showAnchorDebugSphere = true;
	public Material debugAnchorMaterial;

	private bool wasRightTriggerPressed;
	private GameObject activeDebugSphere;

	private const string LogPrefix = "[SpacialAnchorTest]";

	private void Awake()
	{
		if (sceneUnderstandingTest == null)
		{
			sceneUnderstandingTest = FindFirstObjectByType<SceneUnderstandingTest>();
		}

		if (arAnchorManager == null)
		{
			arAnchorManager = FindFirstObjectByType<ARAnchorManager>();
		}
	}

	private void Start()
	{
		Debug.Log(LogPrefix + " Start fired.");
	}

	private void Update()
	{
		if (!enableAnchorTestInput)
		{
			return;
		}

		bool isPressed = false;
		InputDevice rightHandDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
		if (rightHandDevice.isValid)
		{
			rightHandDevice.TryGetFeatureValue(CommonUsages.triggerButton, out isPressed);
		}

		if (isPressed && !wasRightTriggerPressed)
		{
			Debug.Log(LogPrefix + " Trigger down detected.");
			TryCreateAnchorAtCurrentSceneHit();
		}
		else if (isPressed && wasRightTriggerPressed)
		{
			Debug.Log(LogPrefix + " Placement skipped because trigger already held.");
		}

		wasRightTriggerPressed = isPressed;
	}

	private async void TryCreateAnchorAtCurrentSceneHit()
	{
		if (sceneUnderstandingTest == null)
		{
			Debug.LogWarning(LogPrefix + " Cannot create anchor. SceneUnderstandingTest reference is missing.");
			return;
		}

		if (arAnchorManager == null)
		{
			Debug.LogWarning(LogPrefix + " Cannot create anchor. ARAnchorManager reference is missing.");
			return;
		}

		if (rightControllerTransform == null)
		{
			Debug.LogWarning(LogPrefix + " Cannot create anchor. Right controller transform is missing.");
			return;
		}

		Ray controllerRay = new Ray(rightControllerTransform.position, rightControllerTransform.forward);
		Pose sceneHitPose;
		bool hasSceneHit = sceneUnderstandingTest.TryGetSceneHitFromRay(controllerRay, out sceneHitPose);
		Debug.Log(LogPrefix + " TryGetSceneHitFromRay returned: " + hasSceneHit);

		if (!hasSceneHit)
		{
			Debug.Log(LogPrefix + " No scene hit available from right controller ray.");
			return;
		}

		Debug.Log(LogPrefix + " Scene hit found at position: " + sceneHitPose.position);
		var result = await arAnchorManager.TryAddAnchorAsync(sceneHitPose);
		if (!result.status.IsSuccess())
		{
			Debug.LogWarning(
				LogPrefix
				+ " Failed to create ARAnchor at scene hit pose."
				+ " Status: " + result.status);
			return;
		}

		ARAnchor anchor = result.value;
		if (anchor == null)
		{
			Debug.LogWarning(LogPrefix + " Anchor creation reported success but returned a null ARAnchor.");
			return;
		}

		Debug.Log(
			LogPrefix
			+ " Anchor created successfully."
			+ " TrackableId: " + anchor.trackableId
			+ " World Position: " + anchor.transform.position);

		if (showAnchorDebugSphere)
		{
			CreateDebugSphere(anchor);
		}
	}

	private void CreateDebugSphere(ARAnchor anchor)
	{
		if (activeDebugSphere != null)
		{
			Destroy(activeDebugSphere);
		}

		GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		debugSphere.name = "AnchorDebugSphere_" + anchor.trackableId;
		debugSphere.transform.SetParent(anchor.transform, false);
		debugSphere.transform.localPosition = Vector3.zero;
		debugSphere.transform.localRotation = Quaternion.identity;
		debugSphere.transform.localScale = Vector3.one * 0.08f;
		debugSphere.transform.position = anchor.transform.position;

		Collider sphereCollider = debugSphere.GetComponent<Collider>();
		if (sphereCollider != null)
		{
			sphereCollider.enabled = false;
		}

		if (debugAnchorMaterial != null)
		{
			Renderer sphereRenderer = debugSphere.GetComponent<Renderer>();
			if (sphereRenderer != null)
			{
				sphereRenderer.sharedMaterial = debugAnchorMaterial;
			}
		}

		activeDebugSphere = debugSphere;
	}
}
