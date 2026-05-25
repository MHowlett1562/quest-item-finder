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
	[SerializeField] private bool showDebugHitSphere = false;
	[SerializeField] private Material debugHitMaterial;

	private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();
	private Coroutine startupInitializationCoroutine;
	private GameObject debugHitSphere;
	private bool isDebugHitSphereVisible = true;
	private bool isSpatialPermissionGranted;
	private bool areSceneSystemsReady;
	public bool HasLatestHit { get; private set; }
	public Vector3 LatestHitPosition { get; private set; }
	public Quaternion LatestHitRotation { get; private set; }

	private const string DebugHitName = "SceneUnderstandingDebugHit";
	private const float SceneSystemsWarmupDelaySeconds = 0.75f;
	private static readonly MethodInfo raycastRayMethod = typeof(ARRaycastManager).GetMethod(
		"Raycast",
		new[] { typeof(Ray), typeof(List<ARRaycastHit>), typeof(TrackableType) });

	private void Awake()
	{
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
	}

	private void OnEnable()
	{
		FindManagersIfNeeded();
	}

	private void OnDisable()
	{
		if (startupInitializationCoroutine != null)
		{
			StopCoroutine(startupInitializationCoroutine);
			startupInitializationCoroutine = null;
		}
	}

	private void Update()
	{
		FindManagersIfNeeded();
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

	private void UpdateCenterViewportRaycastHit()
	{
		if (!areSceneSystemsReady)
		{
			HasLatestHit = false;
			return;
		}

		if (Camera.main == null)
		{
			HasLatestHit = false;
			return;
		}

		if (arRaycastManager == null)
		{
			Ray noArManagerRay = Camera.main.ScreenPointToRay(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
			RaycastHit noArManagerPhysicsHit;
			if (Physics.Raycast(noArManagerRay, out noArManagerPhysicsHit, Mathf.Infinity))
			{
				HasLatestHit = true;
				LatestHitPosition = noArManagerPhysicsHit.point;
				LatestHitRotation = Quaternion.LookRotation(noArManagerPhysicsHit.normal);
			}
			else
			{
				HasLatestHit = false;
			}

			if (debugHitSphere != null)
			{
				debugHitSphere.SetActive(false);
			}

			return;
		}

		Vector2 viewportCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
		Ray centerRay = Camera.main.ScreenPointToRay(viewportCenter);
		TrackableType hitTypes = TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds | TrackableType.Depth;

		bool hasHit = false;
		if (isSpatialPermissionGranted)
		{
			hasHit = arRaycastManager.Raycast(viewportCenter, raycastHits, hitTypes);
		}
		else
		{
			raycastHits.Clear();
		}

		if (!hasHit || raycastHits.Count == 0)
		{
			RaycastHit physicsHit;
			if (Physics.Raycast(centerRay, out physicsHit, Mathf.Infinity))
			{
				HasLatestHit = true;
				LatestHitPosition = physicsHit.point;
				LatestHitRotation = Quaternion.LookRotation(physicsHit.normal);
			}
			else
			{
				HasLatestHit = false;
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

	private IEnumerator InitializeSceneSystemsAfterPermissionGranted()
	{
		areSceneSystemsReady = false;
		FindManagersIfNeeded();
		ToggleSceneSystemManagers(false);

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