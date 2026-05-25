using System;
using UnityEngine;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public static class VoicePermissionHelper
{
	private const string LogPrefix = "[VoiceNaming]";

	public static VoicePermissionState GetCurrentMicrophonePermissionState()
	{
		return GetStartupMicrophonePermissionState();
	}

	public static VoicePermissionState GetStartupMicrophonePermissionState()
	{
#if UNITY_ANDROID
		if (!IsRecordAudioDeclaredInManifest())
		{
			Debug.LogWarning(LogPrefix + " Android manifest appears to be missing RECORD_AUDIO. Permission dialog may not appear.");
			return VoicePermissionState.Unavailable;
		}

		return Permission.HasUserAuthorizedPermission(Permission.Microphone)
			? VoicePermissionState.Granted
			: VoicePermissionState.Denied;
#else
		return VoicePermissionState.Unavailable;
#endif
	}

	public static void RequestMicrophonePermission(Action onGranted, Action onDenied)
	{
#if UNITY_ANDROID
		if (!IsRecordAudioDeclaredInManifest())
		{
			Debug.LogWarning(LogPrefix + " Android manifest appears to be missing RECORD_AUDIO. Cannot request microphone permission.");
			onDenied?.Invoke();
			return;
		}

		if (Permission.HasUserAuthorizedPermission(Permission.Microphone))
		{
			Debug.Log(LogPrefix + " Microphone permission already granted.");
			onGranted?.Invoke();
			return;
		}

		Debug.Log(LogPrefix + " Requesting microphone permission.");

		var callbacks = new PermissionCallbacks();

		callbacks.PermissionGranted += _ =>
		{
			Debug.Log(LogPrefix + " Microphone permission granted.");
			onGranted?.Invoke();
		};

		callbacks.PermissionDenied += _ =>
		{
			Debug.LogWarning(LogPrefix + " Microphone permission denied. Falling back to manual typing.");
			onDenied?.Invoke();
		};

		callbacks.PermissionDeniedAndDontAskAgain += _ =>
		{
			Debug.LogWarning(LogPrefix + " Microphone permission denied (don't ask again). Falling back to manual typing.");
			onDenied?.Invoke();
		};

		Permission.RequestUserPermission(Permission.Microphone, callbacks);
#else
		Debug.Log(LogPrefix + " Microphone permission already granted.");
		onGranted?.Invoke();
#endif
	}

#if UNITY_ANDROID
	private static bool IsRecordAudioDeclaredInManifest()
	{
		try
		{
			using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
			using (AndroidJavaObject packageManager = activity.Call<AndroidJavaObject>("getPackageManager"))
			using (AndroidJavaClass packageManagerClass = new AndroidJavaClass("android.content.pm.PackageManager"))
			{
				int getPermissionsFlag = packageManagerClass.GetStatic<int>("GET_PERMISSIONS");
				string packageName = activity.Call<string>("getPackageName");
				using (AndroidJavaObject packageInfo = packageManager.Call<AndroidJavaObject>("getPackageInfo", packageName, getPermissionsFlag))
				{
					string[] requestedPermissions = packageInfo.Get<string[]>("requestedPermissions");
					if (requestedPermissions == null)
					{
						return false;
					}

					for (int i = 0; i < requestedPermissions.Length; i++)
					{
						if (requestedPermissions[i] == "android.permission.RECORD_AUDIO")
						{
							return true;
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning(LogPrefix + " Could not verify manifest RECORD_AUDIO declaration: " + ex.Message);
		}

		return false;
	}
#endif
}

public enum VoicePermissionState
{
	Unavailable,
	Granted,
	Denied
}
