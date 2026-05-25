using System;
using UnityEngine;

// Android-specific JNI implementation of IVoiceTranscriptionProvider.
// All Android code is guarded so this file compiles safely in the Unity Editor.
public class AndroidSpeechRecognizerProvider : IVoiceTranscriptionProvider
{
	private const string LogPrefix = "[VoiceNaming]";

#if UNITY_ANDROID
	// Proxy class that bridges android.speech.RecognitionListener to C# callbacks.
	private sealed class RecognitionListenerProxy : AndroidJavaProxy
	{
		private readonly Action<string> onFinalResult;
		private readonly Action<string> onPartialResult;
		private readonly Action<string> _onError;

		public RecognitionListenerProxy(Action<string> onFinalResult, Action<string> onPartialResult, Action<string> onError)
			: base("android.speech.RecognitionListener")
		{
			this.onFinalResult = onFinalResult;
			this.onPartialResult = onPartialResult;
			_onError = onError;
		}

		// Called when the recognizer is ready for speech input.
		public void onReadyForSpeech(AndroidJavaObject bundle)
		{
			Debug.Log(LogPrefix + " RecognitionListener.onReadyForSpeech callback.");
		}

		public void onBeginningOfSpeech()
		{
			Debug.Log(LogPrefix + " RecognitionListener.onBeginningOfSpeech callback.");
		}

		public void onRmsChanged(float rmsdB)
		{
			Debug.Log(LogPrefix + " RecognitionListener.onRmsChanged callback. rmsdB=" + rmsdB);
		}

		public void onBufferReceived(AndroidJavaObject buffer) { }

		public void onEndOfSpeech()
		{
			Debug.Log(LogPrefix + " RecognitionListener.onEndOfSpeech callback.");
		}

		// Called by Android when an error occurs. Error codes match android.speech.SpeechRecognizer constants.
		public void onError(int error)
		{
			string description = DescribeError(error);
			Debug.LogWarning("[VoiceStartup] recognition error: " + description + " (code " + error + ")");
			Debug.LogWarning(LogPrefix + " RecognitionListener.onError callback. " + description + " (code " + error + ")");
			_onError?.Invoke(description);
		}

		// Called when recognition produces a final result.
		public void onResults(AndroidJavaObject bundle)
		{
			Debug.Log(LogPrefix + " RecognitionListener.onResults callback.");
			try
			{
				AndroidJavaObject resultList = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
				if (resultList == null)
				{
					Debug.LogWarning(LogPrefix + " Speech recognition result bundle was empty.");
					_onError?.Invoke("Empty result bundle.");
					return;
				}

				int count = resultList.Call<int>("size");
				if (count == 0)
				{
					Debug.LogWarning(LogPrefix + " Speech recognition returned no results.");
					_onError?.Invoke("No results.");
					return;
				}

				string transcript = resultList.Call<string>("get", 0);
				Debug.Log("[VoiceStartup] final transcript: " + transcript);
				Debug.Log(LogPrefix + " Speech recognition result received: " + transcript);
				onFinalResult?.Invoke(transcript);
			}
			catch (Exception ex)
			{
				Debug.LogWarning(LogPrefix + " Speech recognition result parse error: " + ex.Message);
				_onError?.Invoke(ex.Message);
			}
		}

		public void onPartialResults(AndroidJavaObject bundle)
		{
			Debug.Log(LogPrefix + " RecognitionListener.onPartialResults callback.");
			try
			{
				AndroidJavaObject partialResultList = bundle.Call<AndroidJavaObject>("getStringArrayList", "results_recognition");
				if (partialResultList == null)
				{
					return;
				}

				int count = partialResultList.Call<int>("size");
				if (count <= 0)
				{
					return;
				}

				string partialTranscript = partialResultList.Call<string>("get", 0);
				Debug.Log("[VoiceStartup] partial transcript: " + partialTranscript);
				onPartialResult?.Invoke(partialTranscript);
			}
			catch (Exception ex)
			{
				Debug.LogWarning(LogPrefix + " Partial transcript parse error: " + ex.Message);
			}
		}

		public void onEvent(int eventType, AndroidJavaObject bundle) { }

		private static string DescribeError(int code)
		{
			switch (code)
			{
				case 1: return "NETWORK_TIMEOUT";
				case 2: return "NETWORK";
				case 3: return "AUDIO";
				case 4: return "SERVER";
				case 5: return "CLIENT";
				case 6: return "SPEECH_TIMEOUT";
				case 7: return "NO_MATCH";
				case 8: return "RECOGNIZER_BUSY";
				case 9: return "INSUFFICIENT_PERMISSIONS";
				default: return "UNKNOWN_" + code;
			}
		}
	}

	private AndroidJavaObject speechRecognizer;
	private RecognitionListenerProxy warmupListener;
#endif

#if UNITY_ANDROID
	public bool InitializeRecognizer()
	{
		try
		{
			using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
			{
				bool isAvailable;
				using (AndroidJavaClass recognizerClass = new AndroidJavaClass("android.speech.SpeechRecognizer"))
				{
					isAvailable = recognizerClass.CallStatic<bool>("isRecognitionAvailable", activity);
				}

				Debug.Log("[VoiceStartup] SpeechRecognizer.isRecognitionAvailable = " + isAvailable);
				Debug.Log(LogPrefix + " SpeechRecognizer.isRecognitionAvailable = " + isAvailable);
				if (!isAvailable)
				{
					Debug.LogWarning(LogPrefix + " Speech recognition unavailable on this device.");
					return false;
				}

				if (speechRecognizer != null)
				{
					speechRecognizer.Call("destroy");
					speechRecognizer.Dispose();
					speechRecognizer = null;
				}

				using (AndroidJavaClass recognizerFactory = new AndroidJavaClass("android.speech.SpeechRecognizer"))
				{
					speechRecognizer = recognizerFactory.CallStatic<AndroidJavaObject>("createSpeechRecognizer", activity);
				}

				if (speechRecognizer == null)
				{
					Debug.LogWarning(LogPrefix + " Failed to create Android SpeechRecognizer.");
					return false;
				}

				warmupListener = new RecognitionListenerProxy(null, null, null);
				speechRecognizer.Call("setRecognitionListener", warmupListener);
				Debug.Log("[VoiceStartup] recognizer created successfully");
				Debug.Log(LogPrefix + " SpeechRecognizer created successfully.");
				Debug.Log(LogPrefix + " Using in-process SpeechRecognizer; no external speech UI/activity will be launched.");
				return true;
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning(LogPrefix + " Speech recognizer initialization error: " + ex.Message);
			return false;
		}
	}
#endif

	public void StartListening(Action<string> onTranscriptReceived, Action<string> onError = null)
	{
#if UNITY_ANDROID
		StartListeningWithProgress(
			onPartialTranscriptReceived: null,
			onFinalTranscriptReceived: onTranscriptReceived,
			onError: onError);
#else
		Debug.LogWarning(LogPrefix + " AndroidSpeechRecognizerProvider is not supported in the Unity Editor. Use MockVoiceTranscriptionProvider instead.");
		onError?.Invoke("AndroidSpeechRecognizerProvider not supported outside Android.");
#endif
	}

#if UNITY_ANDROID
	public bool StartListeningWithProgress(Action<string> onPartialTranscriptReceived, Action<string> onFinalTranscriptReceived, Action<string> onError = null)
	{
		Debug.Log(LogPrefix + " StartListening called.");
		Debug.Log("[VoiceStartup] startListening called");
		try
		{
			if (!InitializeRecognizer())
			{
				onError?.Invoke("Speech recognition unavailable on this device.");
				return false;
			}

			using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
			using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
			{
				var listener = new RecognitionListenerProxy(onFinalTranscriptReceived, onPartialTranscriptReceived, onError);
				speechRecognizer.Call("setRecognitionListener", listener);

				// Retrieve RecognizerIntent constants, then build and dispatch the intent.
				string actionRecognize;
				string extraLanguageModel;
				string languageModelFreeForm;
				using (AndroidJavaClass recognizerIntentClass = new AndroidJavaClass("android.speech.RecognizerIntent"))
				{
					actionRecognize       = recognizerIntentClass.GetStatic<string>("ACTION_RECOGNIZE_SPEECH");
					extraLanguageModel    = recognizerIntentClass.GetStatic<string>("EXTRA_LANGUAGE_MODEL");
					languageModelFreeForm = recognizerIntentClass.GetStatic<string>("LANGUAGE_MODEL_FREE_FORM");
				}

				using (AndroidJavaObject intent = new AndroidJavaObject("android.content.Intent", actionRecognize))
				{
					intent.Call<AndroidJavaObject>("putExtra", "android.speech.extra.PARTIAL_RESULTS", true);
					intent.Call<AndroidJavaObject>("putExtra", extraLanguageModel, languageModelFreeForm);
					Debug.Log(LogPrefix + " Calling SpeechRecognizer.startListening.");
					speechRecognizer.Call("startListening", intent);
					Debug.Log(LogPrefix + " Android speech recognition provider returned (listener attached, awaiting result).");
					return true;
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning("[VoiceStartup] recognition error: " + ex.Message);
			Debug.LogWarning(LogPrefix + " Speech recognition error: " + ex.Message);
			onError?.Invoke(ex.Message);
			return false;
		}
	}
#endif
}
