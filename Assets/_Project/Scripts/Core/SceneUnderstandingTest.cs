using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class SceneUnderstandingTest : MonoBehaviour
{
	[SerializeField] private ARPlaneManager arPlaneManager;
	[SerializeField] private ARRaycastManager arRaycastManager;
	[SerializeField] private ARMeshManager arMeshManager;
	[SerializeField] private SavedItemFinderExample savedItemFinderExample;
	[SerializeField] private bool showVisibleDebugOverlay = true;
	[SerializeField] private Vector3 visibleDebugOverlayLocalPosition = new Vector3(0f, 0.16f, 0.8f);
	[SerializeField] private bool showDebugHitSphere = false;
	[SerializeField] private Material debugHitMaterial;

	private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
	private float nextStatusLogTime;
	private float nextRaycastLogTime;
	private float nextVisibleDebugWarningTime;
	private Coroutine startupInitializationCoroutine;
	private GameObject debugHitSphere;
	private TextMesh visibleDebugText;
	private bool isDebugHitSphereVisible = true;
	private bool hasLoggedFirstUpdate;
	private bool isSpatialPermissionGranted;
	private bool areSceneSystemsReady;
	private float lastRaycastAttemptTime = -1f;
	private int lastRaycastHitCount = -1;
	private int lastPlaneCount = -1;
	private int lastMeshColliderCount = -1;
	private bool lastPhysicsFallbackHit;
	private bool hasLastFirstHitPosition;
	private Vector3 lastFirstHitPosition;
	private Vector3 lastRayOrigin;
	private Vector3 lastRayDirection;
	public bool HasLatestHit { get; private set; }
	public Vector3 LatestHitPosition { get; private set; }
	public Quaternion LatestHitRotation { get; private set; }

	private const string LogPrefix = "[SceneUnderstandingTest]";
	private const string VisibleDebugPrefix = "[SceneUnderstandingVisibleDebug]";
	private const string DebugHitName = "SceneUnderstandingDebugHit";
	private const string VisibleDebugTextName = "SceneUnderstandingVisibleDebugText";
	private const float SceneSystemsWarmupDelaySeconds = 0.75f;
	private static readonly MethodInfo raycastRayMethod = typeof(ARRaycastManager).GetMethod(
		"Raycast",
		new[] { typeof(Ray), typeof(List<ARRaycastHit>), typeof(TrackableType) });

	private void Awake()
	{
		Debug.LogWarning(LogPrefix + " Awake running.");
		Debug.Log("[SceneUnderstandingStartup] Permission gate started.");
		SpatialDataPermissionHelper.RequestSpatialPermission(
			onGranted: () =>
			{
				Debug.Log("[SceneUnderstandingStartup] Permission granted, initializing scene systems.");
				isSpatialPermissionGranted = true;
				if (startupInitializationCoroutine != null)
				{
					StopCoroutine(startupInitializationCoroutine);
				}

				startupInitializationCoroutine = StartCoroutine(InitializeSceneSystemsAfterPermissionGranted());
			},
			onDenied: () =>
			{
				isSpatialPermissionGranted = false;
				areSceneSystemsReady = true;
				Debug.LogWarning("[SceneUnderstandingStartup] Permission denied; using fallback placement.");
			});
		FindManagersIfNeeded();
		if (showVisibleDebugOverlay)
		{
			EnsureVisibleDebugText();
		}
	}

	private void Start()
	{
		Debug.LogWarning(LogPrefix + " Start running.");
	}

	private void OnEnable()
	{
		FindManagersIfNeeded();
		SubscribePlaneChanges();
		nextStatusLogTime = Time.time + 1f;
		nextRaycastLogTime = Time.time + 1f;
	}

	private void OnDisable()
	{
		UnsubscribePlaneChanges();
		if (startupInitializationCoroutine != null)
		{
			StopCoroutine(startupInitializationCoroutine);
			startupInitializationCoroutine = null;
		}
	}

	private void Update()
	{
		if (!hasLoggedFirstUpdate)
		{
			hasLoggedFirstUpdate = true;
			Debug.LogWarning(LogPrefix + " Update running.");
		}

		FindManagersIfNeeded();
		LogStatusEverySecond();
		UpdateCenterViewportRaycastHit();
		UpdateVisibleDebugDiagnostics();
	}

	private void FindManagersIfNeeded()
	{
		if (arPlaneManager == null)
		{
			arPlaneManager = FindFirstObjectByType<ARPlaneManager>();
		}

		if (arRaycastManager == null)
		{
			arRaycastManager = FindFirstObjectByType<ARRaycastManager>();
		}

		if (arMeshManager == null)
		{
			arMeshManager = FindFirstObjectByType<ARMeshManager>();
		}

		if (savedItemFinderExample == null)
		{
			savedItemFinderExample = FindFirstObjectByType<SavedItemFinderExample>();
		}
	}

	private void SubscribePlaneChanges()
	{
		if (arPlaneManager == null)
		{
			Debug.LogWarning(LogPrefix + " ARPlaneManager not found. Plane change subscription skipped.");
			return;
		}

		arPlaneManager.trackablesChanged.RemoveListener(OnPlaneTrackablesChanged);
		arPlaneManager.trackablesChanged.AddListener(OnPlaneTrackablesChanged);
		Debug.Log(LogPrefix + " Subscribed to ARPlaneManager.trackablesChanged.");
	}

	private void UnsubscribePlaneChanges()
	{
		if (arPlaneManager == null)
		{
			return;
		}

		arPlaneManager.trackablesChanged.RemoveListener(OnPlaneTrackablesChanged);
	}

	private void OnPlaneTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> eventArgs)
	{
		if (eventArgs.added.Count > 0)
		{
			for (int i = 0; i < eventArgs.added.Count; i++)
			{
				ARPlane plane = eventArgs.added[i];
				Debug.Log(LogPrefix + " Plane added: " + plane.trackableId);
			}
		}

		if (eventArgs.updated.Count > 0)
		{
			for (int i = 0; i < eventArgs.updated.Count; i++)
			{
				ARPlane plane = eventArgs.updated[i];
				Debug.Log(LogPrefix + " Plane updated: " + plane.trackableId);
			}
		}

		if (eventArgs.removed.Count > 0)
		{
			for (int i = 0; i < eventArgs.removed.Count; i++)
			{
				var removedPlanePair = eventArgs.removed[i];
				Debug.Log(LogPrefix + " Plane removed: " + removedPlanePair.Key);
			}
		}
	}

	private void LogStatusEverySecond()
	{
		if (Time.time < nextStatusLogTime)
		{
			return;
		}

		nextStatusLogTime = Time.time + 1f;

		bool hasPlaneManager = arPlaneManager != null;
		bool hasRaycastManager = arRaycastManager != null;
		bool hasMeshManager = arMeshManager != null;

		int planeCount = 0;
		if (hasPlaneManager)
		{
			planeCount = arPlaneManager.trackables.count;
		}
		lastPlaneCount = planeCount;

		int meshCount = 0;
		if (hasMeshManager)
		{
			meshCount = arMeshManager.GetComponentsInChildren<MeshFilter>(true).Length;
		}

		MeshCollider[] meshColliders = Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
		lastMeshColliderCount = meshColliders.Length;

		Debug.Log(
			LogPrefix
			+ " Managers found - Plane: " + hasPlaneManager
			+ " (enabled: " + (hasPlaneManager && arPlaneManager.enabled) + ")"
			+ ", Raycast: " + hasRaycastManager
			+ " (enabled: " + (hasRaycastManager && arRaycastManager.enabled) + ")"
			+ ", Mesh: " + hasMeshManager
			+ " | Plane count: " + planeCount
			+ " | Mesh count: " + meshCount
			+ " | Mesh collider count: " + lastMeshColliderCount);

		if (hasPlaneManager && arPlaneManager.enabled && planeCount == 0)
		{
			Debug.LogWarning(LogPrefix + " ARPlaneManager is enabled but has zero planes/trackables.");
		}

		if (lastMeshColliderCount == 0)
		{
			Debug.LogWarning(LogPrefix + " No mesh/scene colliders found in scene for physics fallback.");
		}
	}

	private void UpdateCenterViewportRaycastHit()
	{
		if (!areSceneSystemsReady)
		{
			lastRaycastHitCount = -1;
			hasLastFirstHitPosition = false;
			lastPhysicsFallbackHit = false;
			return;
		}

		if (Camera.main == null)
		{
			lastRaycastHitCount = -1;
			hasLastFirstHitPosition = false;
			lastPhysicsFallbackHit = false;
			return;
		}

		if (arRaycastManager == null)
		{
			lastRaycastHitCount = -1;
			hasLastFirstHitPosition = false;

			Ray noArManagerRay = Camera.main.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
			lastRayOrigin = noArManagerRay.origin;
			lastRayDirection = noArManagerRay.direction;
			lastRaycastAttemptTime = Time.time;

			RaycastHit noArManagerPhysicsHit;
			lastPhysicsFallbackHit = Physics.Raycast(noArManagerRay, out noArManagerPhysicsHit, Mathf.Infinity);
			if (lastPhysicsFallbackHit)
			{
				HasLatestHit = true;
				LatestHitPosition = noArManagerPhysicsHit.point;
				LatestHitRotation = Quaternion.LookRotation(noArManagerPhysicsHit.normal);
				hasLastFirstHitPosition = true;
				lastFirstHitPosition = noArManagerPhysicsHit.point;
			}
			else
			{
				HasLatestHit = false;
			}

			return;
		}

		Vector2 viewportCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		Ray centerRay = Camera.main.ScreenPointToRay(viewportCenter);
		lastRayOrigin = centerRay.origin;
		lastRayDirection = centerRay.direction;
		TrackableType hitTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Depth;
		bool shouldLogRaycastThisSecond = Time.time >= nextRaycastLogTime;
		bool isSavePlacementActive = IsSavePlacementActive();

		if (shouldLogRaycastThisSecond)
		{
			nextRaycastLogTime = Time.time + 1f;

			if (isSavePlacementActive)
			{
				bool raycastManagerEnabled = arRaycastManager != null && arRaycastManager.enabled;
				bool planeManagerEnabled = arPlaneManager != null && arPlaneManager.enabled;

				Debug.Log(
					LogPrefix
					+ " Raycast attempt (save placement active)."
					+ " Spatial permission state: " + SpatialDataPermissionHelper.GetSpatialPermissionStateText()
					+ " | ARRaycastManager exists/enabled: " + (arRaycastManager != null) + "/" + raycastManagerEnabled
					+ " | ARPlaneManager exists/enabled: " + (arPlaneManager != null) + "/" + planeManagerEnabled
					+ " | Ray origin: " + centerRay.origin + " | Ray direction: " + centerRay.direction);
			}
			else
			{
				Debug.Log(
					LogPrefix
					+ " Raycast attempt (always-on diagnostics)."
					+ " | Ray origin: " + centerRay.origin
					+ " | Ray direction: " + centerRay.direction);
			}
		}

		bool hasHit = false;
		if (isSpatialPermissionGranted)
		{
			hasHit = arRaycastManager.Raycast(viewportCenter, raycastHits, hitTypes);
		}
		else
		{
			raycastHits.Clear();
		}
		lastRaycastAttemptTime = Time.time;
		lastRaycastHitCount = raycastHits.Count;
		lastPhysicsFallbackHit = false;
		if (!hasHit || raycastHits.Count == 0)
		{
			RaycastHit physicsHit;
			lastPhysicsFallbackHit = Physics.Raycast(centerRay, out physicsHit, Mathf.Infinity);
			if (lastPhysicsFallbackHit)
			{
				HasLatestHit = true;
				LatestHitPosition = physicsHit.point;
				LatestHitRotation = Quaternion.LookRotation(physicsHit.normal);
				hasLastFirstHitPosition = true;
				lastFirstHitPosition = physicsHit.point;
			}
			else
			{
				HasLatestHit = false;
				hasLastFirstHitPosition = false;
			}

			if (shouldLogRaycastThisSecond)
			{
				Debug.Log(
					LogPrefix
					+ " AR raycast hit count: 0"
					+ " | physics fallback hit: " + lastPhysicsFallbackHit
					+ " | ray origin: " + centerRay.origin
					+ " | ray direction: " + centerRay.direction);
				if (arPlaneManager != null && arPlaneManager.enabled && arPlaneManager.trackables.count == 0)
				{
					Debug.LogWarning(LogPrefix + " ARPlaneManager enabled but currently has zero planes while raycast returned 0 hits.");
				}
			}

			if (debugHitSphere != null)
			{
				debugHitSphere.SetActive(false);
			}

			return;
		}

		Pose hitPose = raycastHits[0].pose;
		HasLatestHit = true;
		LatestHitPosition = hitPose.position;
		LatestHitRotation = hitPose.rotation;
		hasLastFirstHitPosition = true;
		lastFirstHitPosition = hitPose.position;

		if (shouldLogRaycastThisSecond)
		{
			Debug.Log(
				LogPrefix
				+ " AR raycast hit count: " + raycastHits.Count
				+ " | physics fallback hit: false"
				+ " | ray origin: " + centerRay.origin
				+ " | ray direction: " + centerRay.direction);
			Debug.Log(LogPrefix + " First hit pose - position: " + hitPose.position + ", rotation: " + hitPose.rotation.eulerAngles);
		}

		if (!showDebugHitSphere || !isDebugHitSphereVisible)
		{
			if (debugHitSphere != null)
			{
				debugHitSphere.SetActive(false);
			}
			return;
		}

		EnsureDebugHitSphere();
		debugHitSphere.SetActive(true);
		debugHitSphere.transform.SetPositionAndRotation(hitPose.position, hitPose.rotation);
	}

	public void SetDebugHitSphereVisible(bool isVisible)
	{
		isDebugHitSphereVisible = isVisible;

		if (!isVisible && debugHitSphere != null)
		{
			debugHitSphere.SetActive(false);
		}
	}

	public bool TryGetSceneHitFromRay(Ray ray, out Pose hitPose)
	{
		hitPose = default;
		FindManagersIfNeeded();

		TrackableType hitTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Depth;

		if (arRaycastManager != null && raycastRayMethod != null)
		{
			if (areSceneSystemsReady && isSpatialPermissionGranted)
			{
				raycastHits.Clear();
				object result = raycastRayMethod.Invoke(arRaycastManager, new object[] { ray, raycastHits, hitTypes });
				if (result is bool hasHit && hasHit && raycastHits.Count > 0)
				{
					hitPose = raycastHits[0].pose;
					return true;
				}
			}
		}

		// Compile-safe fallback: raycast against scene colliders (AR planes/meshes when collider components exist).
		RaycastHit physicsHit;
		if (Physics.Raycast(ray, out physicsHit, Mathf.Infinity))
		{
			hitPose = new Pose(physicsHit.point, Quaternion.LookRotation(physicsHit.normal));
			return true;
		}

		return false;
	}

	private void EnsureDebugHitSphere()
	{
		if (debugHitSphere != null)
		{
			return;
		}

		GameObject existing = GameObject.Find(DebugHitName);
		if (existing != null)
		{
			debugHitSphere = existing;
		}
		else
		{
			debugHitSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
			debugHitSphere.name = DebugHitName;
			debugHitSphere.transform.localScale = Vector3.one * 0.05f;
		}

		if (debugHitMaterial != null)
		{
			Renderer renderer = debugHitSphere.GetComponent<Renderer>();
			if (renderer != null)
			{
				renderer.sharedMaterial = debugHitMaterial;
			}
		}

		Collider collider = debugHitSphere.GetComponent<Collider>();
		if (collider != null)
		{
			collider.enabled = false;
		}
	}

	private bool IsSavePlacementActive()
	{
		return savedItemFinderExample != null && savedItemFinderExample.IsSaveMode();
	}

	private void EnsureVisibleDebugText()
	{
		if (visibleDebugText != null)
		{
			return;
		}

		GameObject existing = GameObject.Find(VisibleDebugTextName);
		GameObject debugTextObject = existing != null ? existing : new GameObject(VisibleDebugTextName);
		visibleDebugText = debugTextObject.GetComponent<TextMesh>();
		if (visibleDebugText == null)
		{
			visibleDebugText = debugTextObject.AddComponent<TextMesh>();
		}

		visibleDebugText.fontSize = 42;
		visibleDebugText.characterSize = 0.005f;
		visibleDebugText.color = Color.yellow;
		visibleDebugText.anchor = TextAnchor.UpperLeft;
		visibleDebugText.alignment = TextAlignment.Left;
	}

	private void UpdateVisibleDebugDiagnostics()
	{
		if (!showVisibleDebugOverlay)
		{
			if (visibleDebugText != null)
			{
				visibleDebugText.gameObject.SetActive(false);
			}
			return;
		}

		if (Camera.main == null)
		{
			return;
		}

		EnsureVisibleDebugText();
		visibleDebugText.gameObject.SetActive(true);
		visibleDebugText.transform.SetParent(Camera.main.transform, false);
		visibleDebugText.transform.localPosition = visibleDebugOverlayLocalPosition;
		visibleDebugText.transform.localRotation = Quaternion.identity;
		visibleDebugText.text = BuildVisibleDebugStatusMultiline();

		if (Time.time >= nextVisibleDebugWarningTime)
		{
			nextVisibleDebugWarningTime = Time.time + 2f;
			Debug.LogWarning(VisibleDebugPrefix + " " + BuildVisibleDebugStatusSingleLine());
		}
	}

	private string BuildVisibleDebugStatusMultiline()
	{
		bool hasRaycastManager = arRaycastManager != null;
		bool raycastEnabled = hasRaycastManager && arRaycastManager.enabled;
		bool hasPlaneManager = arPlaneManager != null;
		bool planeEnabled = hasPlaneManager && arPlaneManager.enabled;
		string lastAttemptText = lastRaycastAttemptTime < 0f ? "never" : lastRaycastAttemptTime.ToString("F1") + "s";
		string firstHitText = hasLastFirstHitPosition ? lastFirstHitPosition.ToString("F3") : "none";

		return "Scene Understanding Debug\n"
			+ "systems ready: " + areSceneSystemsReady + "\n"
			+ "perm: " + SpatialDataPermissionHelper.GetSpatialPermissionStateText() + "\n"
			+ "raycast mgr: " + hasRaycastManager + "/" + raycastEnabled + "\n"
			+ "plane mgr: " + hasPlaneManager + "/" + planeEnabled + "\n"
			+ "AR plane count: " + lastPlaneCount + "\n"
			+ "mesh collider count: " + lastMeshColliderCount + "\n"
			+ "AR raycast hit count: " + lastRaycastHitCount + "\n"
			+ "physics fallback hit: " + lastPhysicsFallbackHit + "\n"
			+ "ray origin: " + lastRayOrigin.ToString("F3") + "\n"
			+ "ray direction: " + lastRayDirection.ToString("F3") + "\n"
			+ "last raycast attempt: " + lastAttemptText + "\n"
			+ "first hit pos: " + firstHitText;
	}

	private string BuildVisibleDebugStatusSingleLine()
	{
		bool hasRaycastManager = arRaycastManager != null;
		bool raycastEnabled = hasRaycastManager && arRaycastManager.enabled;
		bool hasPlaneManager = arPlaneManager != null;
		bool planeEnabled = hasPlaneManager && arPlaneManager.enabled;
		string lastAttemptText = lastRaycastAttemptTime < 0f ? "never" : lastRaycastAttemptTime.ToString("F1") + "s";
		string firstHitText = hasLastFirstHitPosition ? lastFirstHitPosition.ToString("F3") : "none";

		return "ready=" + areSceneSystemsReady
			+ " | perm=" + SpatialDataPermissionHelper.GetSpatialPermissionStateText()
			+ " | raycastMgr=" + hasRaycastManager + "/" + raycastEnabled
			+ " | planeMgr=" + hasPlaneManager + "/" + planeEnabled
			+ " | planeCount=" + lastPlaneCount
			+ " | meshColliders=" + lastMeshColliderCount
			+ " | arHitCount=" + lastRaycastHitCount
			+ " | physicsFallbackHit=" + lastPhysicsFallbackHit
			+ " | rayOrigin=" + lastRayOrigin.ToString("F2")
			+ " | rayDir=" + lastRayDirection.ToString("F2")
			+ " | lastAttempt=" + lastAttemptText
			+ " | firstHit=" + firstHitText;
	}

	private IEnumerator InitializeSceneSystemsAfterPermissionGranted()
	{
		areSceneSystemsReady = false;
		FindManagersIfNeeded();
		ToggleSceneSystemManagers(false);

		Debug.Log("[SceneUnderstandingStartup] Waiting for scene systems to warm up.");
		yield return new WaitForSeconds(SceneSystemsWarmupDelaySeconds);

		FindManagersIfNeeded();
		ToggleSceneSystemManagers(true);
		areSceneSystemsReady = true;
		startupInitializationCoroutine = null;
		Debug.Log("[SceneUnderstandingStartup] Scene systems ready.");
	}

	private void ToggleSceneSystemManagers(bool isEnabled)
	{
		if (arPlaneManager != null)
		{
			arPlaneManager.enabled = isEnabled;
		}

		if (arRaycastManager != null)
		{
			arRaycastManager.enabled = isEnabled;
		}

		if (arMeshManager != null)
		{
			arMeshManager.enabled = isEnabled;
		}
	}
}