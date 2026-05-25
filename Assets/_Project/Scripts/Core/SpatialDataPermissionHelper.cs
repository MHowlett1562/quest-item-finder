using System;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public static class SpatialDataPermissionHelper
{
	private const string SpatialPermissionId = "com.oculus.permission.USE_SCENE";

	public static string GetSpatialPermissionStateText()
	{
		return GetSpatialPermissionState().ToString().ToLowerInvariant();
	}

	public static SpatialPermissionState GetSpatialPermissionState()
	{
#if UNITY_ANDROID
		return Permission.HasUserAuthorizedPermission(SpatialPermissionId)
			? SpatialPermissionState.Granted
			: SpatialPermissionState.Denied;
#else
		return SpatialPermissionState.Unavailable;
#endif
	}

	public static void RequestSpatialPermission(Action onGranted = null, Action onDenied = null)
	{
		Debug.Log("[SceneUnderstanding] Spatial permission check started.");

#if UNITY_ANDROID
		if (Permission.HasUserAuthorizedPermission(SpatialPermissionId))
		{
			Debug.Log("[SceneUnderstanding] Spatial permission already granted.");
			Debug.Log("[SceneUnderstanding] Spatial permission state: " + GetSpatialPermissionStateText() + ".");
			onGranted?.Invoke();
			return;
		}

		Debug.Log("[SceneUnderstanding] Requesting spatial permission.");
		PermissionCallbacks callbacks = new PermissionCallbacks();

		callbacks.PermissionGranted += _ =>
		{
			Debug.Log("[SceneUnderstanding] Spatial permission granted.");
			Debug.Log("[SceneUnderstanding] Spatial permission state: " + GetSpatialPermissionStateText() + ".");
			onGranted?.Invoke();
		};

		callbacks.PermissionDenied += _ =>
		{
			Debug.LogWarning("[SceneUnderstanding] Spatial permission denied.");
			onDenied?.Invoke();
		};

		callbacks.PermissionDeniedAndDontAskAgain += _ =>
		{
			Debug.LogWarning("[SceneUnderstanding] Spatial permission denied.");
			onDenied?.Invoke();
		};

		Permission.RequestUserPermission(SpatialPermissionId, callbacks);
#else
		Debug.Log("[SceneUnderstanding] Spatial permission already granted.");
		Debug.Log("[SceneUnderstanding] Spatial permission state: " + GetSpatialPermissionStateText() + ".");
		onGranted?.Invoke();
#endif
	}
}

public enum SpatialPermissionState
{
	Unavailable,
	Granted,
	Denied
}