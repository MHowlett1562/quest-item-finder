using System.Collections;
using Oculus.Voice.Dictation;
using UnityEngine;
using UnityEngine.UI;

public class VoiceNamingController : MonoBehaviour
{
	private const string LogPrefix = "[VoiceNaming]";
	private const float SpeechSystemsWarmupDelaySeconds = 0.75f;
	private const string ListeningStatusText = "Listening...";
	private const string RecognitionUnavailableStatusText = "Speech recognition unavailable";
	private const string MetaVoiceUnavailableStatusText = "Meta voice unavailable";
	private const string PermissionDeniedStatusText = "Permission denied";

	[SerializeField] private string mockTranscript = "test item";
	[SerializeField] private AppDictationExperience metaVoiceExperience;

	private IVoiceTranscriptionProvider transcriptionProvider;
	private Coroutine startupInitializationCoroutine;

	// Cached result of the startup permission check so the button path never shows a dialog.
	private bool micPermissionGranted = false;
	private bool areSpeechSystemsReady = false;

	private void Awake()
	{
		Debug.Log(LogPrefix + " VoiceNamingController Awake.");
		Debug.Log(LogPrefix + " VoiceNamingController initialized.");
#if UNITY_ANDROID
		if (metaVoiceExperience != null)
		{
			transcriptionProvider = new MetaVoiceTranscriptionProvider(metaVoiceExperience);
			Debug.Log(LogPrefix + " Selected provider: MetaVoiceTranscriptionProvider");
		}
		else
		{
			transcriptionProvider = null;
			Debug.LogWarning(LogPrefix + " Meta voice runtime not assigned. Manual typing is still active.");
		}
#else
		transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
		micPermissionGranted = true; // Non-Android: always treated as granted.
		Debug.Log(LogPrefix + " Selected provider: MockVoiceTranscriptionProvider");
#endif
	}

	private void Start()
	{
		Debug.Log(LogPrefix + " VoiceNamingController Start.");
		Debug.Log("[VoiceStartup] Permission gate started.");
		VoicePermissionState startupState = VoicePermissionHelper.GetStartupMicrophonePermissionState();
		Debug.Log(LogPrefix + " Startup mic permission state: " + StartupPermissionStateToText(startupState) + ".");

		VoicePermissionHelper.RequestMicrophonePermission(
			onGranted: () =>
			{
				micPermissionGranted = true;
				Debug.Log("[VoiceStartup] Permission granted, initializing speech systems.");
				if (startupInitializationCoroutine != null)
				{
					StopCoroutine(startupInitializationCoroutine);
				}

				startupInitializationCoroutine = StartCoroutine(InitializeSpeechSystemsAfterPermissionGranted());
			},
			onDenied: () =>
			{
				micPermissionGranted = false;
				areSpeechSystemsReady = false;
				Debug.LogWarning("[VoiceStartup] Permission denied; using manual naming only.");
			}
		);
	}

	private void OnDisable()
	{
		if (startupInitializationCoroutine != null)
		{
			StopCoroutine(startupInitializationCoroutine);
			startupInitializationCoroutine = null;
		}
	}

	public void RequestTranscriptIntoField(InputField targetInputField)
	{
		Debug.Log(LogPrefix + " Voice button pressed.");
		VoicePermissionState currentMicState = VoicePermissionHelper.GetCurrentMicrophonePermissionState();
		Debug.Log(LogPrefix + " Mic permission state on Voice press (system): " + StartupPermissionStateToText(currentMicState) + ".");
		Debug.Log(LogPrefix + " Active transcription provider: " + (transcriptionProvider != null ? transcriptionProvider.GetType().Name : "null"));
		Debug.Log(LogPrefix + " AndroidSpeechRecognizerProvider selected: " + (transcriptionProvider is AndroidSpeechRecognizerProvider));

		if (targetInputField == null)
		{
			Debug.LogWarning(LogPrefix + " Target name input field is missing.");
			return;
		}

		targetInputField.text = "voice clicked";

		Debug.Log(LogPrefix + " Permission state before listening: " + (micPermissionGranted ? "granted" : "denied"));

		if (!micPermissionGranted)
		{
			targetInputField.text = PermissionDeniedStatusText;
			Debug.LogWarning(LogPrefix + " Voice input unavailable (permission denied). Manual typing is still active.");
			return;
		}

		if (!areSpeechSystemsReady)
		{
			targetInputField.text = transcriptionProvider is MetaVoiceTranscriptionProvider ? MetaVoiceUnavailableStatusText : RecognitionUnavailableStatusText;
			Debug.LogWarning(LogPrefix + " Speech systems not ready yet. Manual typing is still active.");
			return;
		}

		BeginListening(targetInputField);
	}

	private void BeginListening(InputField targetInputField)
	{
		if (transcriptionProvider == null)
		{
		#if UNITY_ANDROID
			targetInputField.text = MetaVoiceUnavailableStatusText;
			Debug.LogWarning(LogPrefix + " Meta voice transcription provider unavailable. Manual typing is still active.");
			return;
		#else
			transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
			Debug.Log(LogPrefix + " Selected provider: MockVoiceTranscriptionProvider");
		#endif
		}

		Debug.Log(LogPrefix + " BeginListening called.");
		Debug.Log("[VoiceStartup] startListening called");

#if UNITY_ANDROID
		MetaVoiceTranscriptionProvider metaProvider = transcriptionProvider as MetaVoiceTranscriptionProvider;
		if (metaProvider != null)
		{
			bool started = metaProvider.StartListeningWithProgress(
				onListeningStarted: () =>
				{
					targetInputField.text = ListeningStatusText;
					Debug.Log("[VoiceStartup] listening started");
				},
				onPartialTranscriptReceived: (partialTranscript) =>
				{
					targetInputField.text = partialTranscript;
					Debug.Log("[VoiceStartup] partial transcript: " + partialTranscript);
				},
				onFinalTranscriptReceived: (finalTranscript) =>
				{
					targetInputField.text = finalTranscript;
					Debug.Log("[VoiceStartup] final transcript: " + finalTranscript);
				},
				onError: (errorMessage) =>
				{
					targetInputField.text = ShouldShowPermissionDenied(errorMessage) ? PermissionDeniedStatusText : MetaVoiceUnavailableStatusText;
					Debug.LogWarning("[VoiceStartup] recognition error: " + errorMessage);
					Debug.LogWarning(LogPrefix + " Meta voice provider returned error: " + errorMessage + ". Manual typing is still active.");
				});

			if (started)
			{
				targetInputField.text = ListeningStatusText;
			}
			else
			{
				targetInputField.text = MetaVoiceUnavailableStatusText;
			}

			return;
		}
#endif

		transcriptionProvider.StartListening(
			onTranscriptReceived: (transcript) =>
			{
				targetInputField.text = transcript;
				Debug.Log("[VoiceStartup] final transcript: " + transcript);
				Debug.Log(LogPrefix + " Transcript applied to name field: " + transcript);
			},
			onError: (errorMessage) =>
			{
				targetInputField.text = ShouldShowPermissionDenied(errorMessage) ? PermissionDeniedStatusText : RecognitionUnavailableStatusText;
				Debug.LogWarning("[VoiceStartup] recognition error: " + errorMessage);
				Debug.LogWarning(LogPrefix + " Voice transcription provider returned error: " + errorMessage + ". Manual typing is still active.");
			}
		);

		targetInputField.text = ListeningStatusText;
	}

	private static string StartupPermissionStateToText(VoicePermissionState state)
	{
		switch (state)
		{
			case VoicePermissionState.Granted:
				return "granted";
			case VoicePermissionState.Denied:
				return "denied";
			default:
				return "unavailable";
		}
	}

	private IEnumerator InitializeSpeechSystemsAfterPermissionGranted()
	{
		areSpeechSystemsReady = false;
		yield return new WaitForSeconds(SpeechSystemsWarmupDelaySeconds);

#if UNITY_ANDROID
		MetaVoiceTranscriptionProvider metaProvider = transcriptionProvider as MetaVoiceTranscriptionProvider;
		if (metaProvider == null && metaVoiceExperience != null)
		{
			metaProvider = new MetaVoiceTranscriptionProvider(metaVoiceExperience);
			transcriptionProvider = metaProvider;
			Debug.Log(LogPrefix + " Selected provider: MetaVoiceTranscriptionProvider");
		}

		if (metaProvider == null)
		{
			areSpeechSystemsReady = false;
			Debug.LogWarning(LogPrefix + " Meta voice runtime not assigned. Manual typing is still active.");
			startupInitializationCoroutine = null;
			yield break;
		}

		if (!metaProvider.HasValidConfiguration(out string configurationError))
		{
			areSpeechSystemsReady = false;
			Debug.LogWarning(LogPrefix + " Meta voice initialization failed: " + configurationError + " Manual typing is still active.");
			startupInitializationCoroutine = null;
			yield break;
		}
#else
		if (transcriptionProvider == null)
		{
			transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
			Debug.Log(LogPrefix + " Selected provider: MockVoiceTranscriptionProvider");
		}
#endif

		areSpeechSystemsReady = true;
		startupInitializationCoroutine = null;
		Debug.Log("[VoiceStartup] Speech systems ready.");
	}

	private static bool ShouldShowPermissionDenied(string errorMessage)
	{
		if (string.IsNullOrWhiteSpace(errorMessage))
		{
			return false;
		}

		string normalized = errorMessage.ToLowerInvariant();
		return normalized.Contains("permission") || normalized.Contains("insufficient_permissions");
	}
}
