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
	[SerializeField] private bool showDebugHitSphere = false;
	[SerializeField] private Material debugHitMaterial;

	private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
	private float nextStatusLogTime;
	private float nextRaycastLogTime;
	private GameObject debugHitSphere;
	private bool isDebugHitSphereVisible = true;
	public bool HasLatestHit { get; private set; }
	public Vector3 LatestHitPosition { get; private set; }
	public Quaternion LatestHitRotation { get; private set; }

	private const string LogPrefix = "[SceneUnderstandingTest]";
	private const string DebugHitName = "SceneUnderstandingDebugHit";
	private static readonly MethodInfo raycastRayMethod = typeof(ARRaycastManager).GetMethod(
		"Raycast",
		new[] { typeof(Ray), typeof(List<ARRaycastHit>), typeof(TrackableType) });

	private void Awake()
	{
		FindManagersIfNeeded();
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
	}

	private void Update()
	{
		FindManagersIfNeeded();
		LogStatusEverySecond();
		UpdateCenterViewportRaycastHit();
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

		int meshCount = 0;
		if (hasMeshManager)
		{
			meshCount = arMeshManager.GetComponentsInChildren<MeshFilter>(true).Length;
		}

		Debug.Log(
			LogPrefix
			+ " Managers found - Plane: " + hasPlaneManager
			+ ", Raycast: " + hasRaycastManager
			+ ", Mesh: " + hasMeshManager
			+ " | Plane count: " + planeCount
			+ " | Mesh count: " + meshCount);
	}

	private void UpdateCenterViewportRaycastHit()
	{
		if (arRaycastManager == null)
		{
			return;
		}

		if (Camera.main == null)
		{
			return;
		}

		Vector2 viewportCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		TrackableType hitTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Depth;

		bool hasHit = arRaycastManager.Raycast(viewportCenter, raycastHits, hitTypes);
		if (!hasHit || raycastHits.Count == 0)
		{
			HasLatestHit = false;

			if (Time.time >= nextRaycastLogTime)
			{
				nextRaycastLogTime = Time.time + 1f;
				Debug.Log(LogPrefix + " No AR raycast hit.");
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

		if (Time.time >= nextRaycastLogTime)
		{
			nextRaycastLogTime = Time.time + 1f;
			Debug.Log(LogPrefix + " AR raycast hit at " + hitPose.position);
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
			raycastHits.Clear();
			object result = raycastRayMethod.Invoke(arRaycastManager, new object[] { ray, raycastHits, hitTypes });
			if (result is bool hasHit && hasHit && raycastHits.Count > 0)
			{
				hitPose = raycastHits[0].pose;
				return true;
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
}