using UnityEngine;
using UnityEngine.UI;

public class VoiceNamingController : MonoBehaviour
{
	private const string LogPrefix = "[VoiceNaming]";

	[SerializeField] private string mockTranscript = "test item";

	private IVoiceTranscriptionProvider transcriptionProvider;

	// Cached result of the startup permission check so the button path never shows a dialog.
	private bool micPermissionGranted = false;

	public VoiceNamingController()
	{
		Debug.Log(LogPrefix + " VoiceNamingController initialized.");
	}

	private void Awake()
	{
		Debug.Log(LogPrefix + " VoiceNamingController Awake.");
		Debug.Log(LogPrefix + " VoiceNamingController initialized.");
#if UNITY_ANDROID
		transcriptionProvider = new AndroidSpeechRecognizerProvider();
		Debug.Log(LogPrefix + " Selected provider: AndroidSpeechRecognizerProvider");
#else
		transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
		micPermissionGranted = true; // Non-Android: always treated as granted.
		Debug.Log(LogPrefix + " Selected provider: MockVoiceTranscriptionProvider");
#endif
	}

	private void Start()
	{
		Debug.Log(LogPrefix + " VoiceNamingController Start.");
		// Request microphone permission at app startup so it is never triggered mid-flow.
		Debug.Log(LogPrefix + " Startup mic permission check started.");
		VoicePermissionState startupState = VoicePermissionHelper.GetStartupMicrophonePermissionState();
		Debug.Log(LogPrefix + " Startup mic permission state: " + StartupPermissionStateToText(startupState) + ".");

		if (startupState == VoicePermissionState.Granted)
		{
			micPermissionGranted = true;
		}

		VoicePermissionHelper.RequestMicrophonePermission(
			onGranted: () =>
			{
				micPermissionGranted = true;
				Debug.Log(LogPrefix + " Voice controller active.");
			},
			onDenied: () =>
			{
				micPermissionGranted = false;
				Debug.LogWarning(LogPrefix + " Microphone permission denied at startup. Voice button will be inactive; manual typing is still usable.");
			}
		);
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

		// Temporary visual diagnostic so click path is obvious on-device before permission/speech logic.
		targetInputField.text = "voice clicked";

		Debug.Log(LogPrefix + " Permission state before listening: " + (micPermissionGranted ? "granted" : "denied"));

		if (!micPermissionGranted)
		{
			Debug.LogWarning(LogPrefix + " Voice input unavailable (permission denied). Manual typing is still active.");
			return;
		}

		BeginListening(targetInputField);
	}

	private void BeginListening(InputField targetInputField)
	{
		if (transcriptionProvider == null)
		{
			transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
			Debug.Log(LogPrefix + " Selected provider: MockVoiceTranscriptionProvider");
		}

		Debug.Log(LogPrefix + " BeginListening called.");

		transcriptionProvider.StartListening(
			onTranscriptReceived: (transcript) =>
			{
				targetInputField.text = transcript;
				Debug.Log(LogPrefix + " Transcript applied to name field: " + transcript);
			},
			onError: (errorMessage) =>
			{
				Debug.LogWarning(LogPrefix + " Android speech recognition provider returned error: " + errorMessage + ". Manual typing is still active.");
			}
		);
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
}
