using System;
using Meta.Voice;
using Meta.WitAi.Configuration;
using Meta.WitAi.Data.Configuration;
using Meta.WitAi.Requests;
using Oculus.Voice.Dictation;
using UnityEngine;

public class MetaVoiceTranscriptionProvider : IVoiceTranscriptionProvider
{
	private const string LogPrefix = "[VoiceNaming]";
	private const string MissingExperienceError = "Meta voice runtime is missing.";
	private const string MissingRuntimeConfigurationError = "Meta voice runtime configuration is missing.";
	private const string MissingWitConfigurationError = "Meta voice WitConfiguration is missing.";

	private readonly AppDictationExperience dictationExperience;
	private Action onListeningStarted;
	private Action<string> onPartialTranscriptReceived;
	private Action<string> onFinalTranscriptReceived;
	private Action<string> onError;
	private bool isRequestInProgress;

	public MetaVoiceTranscriptionProvider(AppDictationExperience dictationExperience)
	{
		this.dictationExperience = dictationExperience;
	}

	public bool HasValidConfiguration(out string errorMessage)
	{
		if (dictationExperience == null)
		{
			errorMessage = MissingExperienceError;
			return false;
		}

		if (dictationExperience.RuntimeDictationConfiguration == null)
		{
			errorMessage = MissingRuntimeConfigurationError;
			return false;
		}

		WitConfiguration witConfiguration = dictationExperience.RuntimeDictationConfiguration.witConfiguration;
		if (witConfiguration == null)
		{
			errorMessage = MissingWitConfigurationError;
			return false;
		}

		errorMessage = null;
		return true;
	}

	public void StartListening(Action<string> onTranscriptReceived, Action<string> onError = null)
	{
		StartListeningWithProgress(null, null, onTranscriptReceived, onError);
	}

	public bool StartListeningWithProgress(
		Action onListeningStarted,
		Action<string> onPartialTranscriptReceived,
		Action<string> onFinalTranscriptReceived,
		Action<string> onError = null)
	{
		if (!HasValidConfiguration(out string configurationError))
		{
			Debug.LogWarning(LogPrefix + " Meta voice provider configuration error: " + configurationError);
			onError?.Invoke(configurationError);
			return false;
		}

		string activateAudioError = dictationExperience.GetActivateAudioError();
		if (!string.IsNullOrEmpty(activateAudioError))
		{
			Debug.LogWarning(LogPrefix + " Meta voice activation blocked: " + activateAudioError);
			onError?.Invoke(activateAudioError);
			return false;
		}

		if (isRequestInProgress)
		{
			const string duplicateRequestError = "Meta voice dictation is already active.";
			Debug.LogWarning(LogPrefix + " " + duplicateRequestError);
			onError?.Invoke(duplicateRequestError);
			return false;
		}

		this.onListeningStarted = onListeningStarted;
		this.onPartialTranscriptReceived = onPartialTranscriptReceived;
		this.onFinalTranscriptReceived = onFinalTranscriptReceived;
		this.onError = onError;
		isRequestInProgress = true;

		RegisterListeners();

		VoiceServiceRequest request = dictationExperience.Activate(new WitRequestOptions(), new VoiceServiceRequestEvents());
		if (request == null && isRequestInProgress)
		{
			const string activationFailedError = "Meta voice activation failed.";
			Debug.LogWarning(LogPrefix + " " + activationFailedError);
			HandleError("activation_failed", activationFailedError);
			return false;
		}

		return true;
	}

	private void RegisterListeners()
	{
		dictationExperience.DictationEvents.OnStartListening.AddListener(HandleStartListening);
		dictationExperience.DictationEvents.OnPartialTranscription.AddListener(HandlePartialTranscription);
		dictationExperience.DictationEvents.OnFullTranscription.AddListener(HandleFullTranscription);
		dictationExperience.DictationEvents.OnError.AddListener(HandleError);
		dictationExperience.DictationEvents.OnStoppedListening.AddListener(HandleStoppedListening);
		dictationExperience.DictationEvents.OnComplete.AddListener(HandleComplete);
	}

	private void UnregisterListeners()
	{
		if (dictationExperience == null)
		{
			return;
		}

		dictationExperience.DictationEvents.OnStartListening.RemoveListener(HandleStartListening);
		dictationExperience.DictationEvents.OnPartialTranscription.RemoveListener(HandlePartialTranscription);
		dictationExperience.DictationEvents.OnFullTranscription.RemoveListener(HandleFullTranscription);
		dictationExperience.DictationEvents.OnError.RemoveListener(HandleError);
		dictationExperience.DictationEvents.OnStoppedListening.RemoveListener(HandleStoppedListening);
		dictationExperience.DictationEvents.OnComplete.RemoveListener(HandleComplete);
	}

	private void HandleStartListening()
	{
		Debug.Log(LogPrefix + " Meta voice started listening.");
		onListeningStarted?.Invoke();
	}

	private void HandlePartialTranscription(string transcript)
	{
		onPartialTranscriptReceived?.Invoke(transcript);
	}

	private void HandleFullTranscription(string transcript)
	{
		onFinalTranscriptReceived?.Invoke(transcript);
	}

	private void HandleError(string errorCode, string errorMessage)
	{
		string combinedError = string.IsNullOrWhiteSpace(errorCode)
			? errorMessage
			: errorCode + ": " + errorMessage;

		onError?.Invoke(combinedError);
		CleanupRequest();
	}

	private void HandleStoppedListening()
	{
		Debug.Log(LogPrefix + " Meta voice stopped listening.");
	}

	private void HandleComplete(VoiceServiceRequest request)
	{
		CleanupRequest();
	}

	private void CleanupRequest()
	{
		UnregisterListeners();
		onListeningStarted = null;
		onPartialTranscriptReceived = null;
		onFinalTranscriptReceived = null;
		onError = null;
		isRequestInProgress = false;
	}
}