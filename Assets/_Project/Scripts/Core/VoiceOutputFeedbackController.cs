using System;
using Meta.WitAi.TTS;
using Meta.WitAi.TTS.Utilities;
using Meta.WitAi.Requests;
using Oculus.Voice.Dictation;
using UnityEngine;

public class VoiceOutputFeedbackController : MonoBehaviour
{
    private const string LogPrefix = "[VoiceOutput]";
    private const string NamePromptText = "What would you like to call this?";

    [SerializeField] private TTSSpeaker ttsSpeaker;
    [SerializeField] private AppDictationExperience dictationExperience;

    private struct PendingUtterance
    {
        public string Text;
        public string Reason;
    }

    private bool hasPendingUtterance;
    private PendingUtterance pendingUtterance;
    private bool listenersRegistered;

    private void Awake()
    {
        if (ttsSpeaker == null)
        {
            ttsSpeaker = FindFirstObjectByType<TTSSpeaker>();
        }

        if (dictationExperience == null)
        {
            dictationExperience = FindFirstObjectByType<AppDictationExperience>();
        }

        RegisterLifecycleListenersIfNeeded();
    }

    private void OnEnable()
    {
        RegisterLifecycleListenersIfNeeded();
    }

    private void OnDisable()
    {
        UnregisterLifecycleListenersIfNeeded();
    }

    public void SpeakNamePrompt()
    {
        Debug.Log(LogPrefix + " SpeakNamePrompt invoked.");
        TrySpeak(NamePromptText, "name prompt");
    }

    public void SpeakSaveConfirmation(string itemName)
    {
        string resolvedName = string.IsNullOrWhiteSpace(itemName) ? "item" : itemName.Trim();
        string confirmationText = "Got it. I saved " + resolvedName + ".";
        Debug.Log(LogPrefix + " SpeakSaveConfirmation invoked for item: '" + resolvedName + "'.");

        if (dictationExperience != null && dictationExperience.MicActive)
        {
            Debug.Log(LogPrefix + " Save confirmation requested while dictation active. Requesting dictation Deactivate().");
            dictationExperience.Deactivate();
        }

        TrySpeak(confirmationText, "save confirmation");
    }

    private void TrySpeak(string textToSpeak, string reason)
    {
        Debug.Log(LogPrefix + " TrySpeak invoked for " + reason + ". Text: '" + textToSpeak + "'.");

        RegisterLifecycleListenersIfNeeded();

        if (string.IsNullOrWhiteSpace(textToSpeak))
        {
            Debug.LogWarning(LogPrefix + " Skipping " + reason + " because text is empty.");
            return;
        }

        bool isDictationMicActive = dictationExperience != null && dictationExperience.MicActive;
        Debug.Log(LogPrefix + " Dictation mic active: " + isDictationMicActive + ".");

        if (isDictationMicActive)
        {
            StorePendingUtterance(textToSpeak, reason, "dictation is still active");
            return;
        }

        Debug.Log(LogPrefix + " TTSSpeaker reference valid: " + (ttsSpeaker != null) + ".");
        if (ttsSpeaker == null)
        {
            Debug.LogWarning(LogPrefix + " TTSSpeaker is missing. Skipping " + reason + ".");
            return;
        }

        TTSService ttsService = ttsSpeaker.TTSService;
        Debug.Log(LogPrefix + " TTSService reference valid: " + (ttsService != null) + ".");
        if (ttsService == null)
        {
            Debug.LogWarning(LogPrefix + " TTSService is missing. Skipping " + reason + ".");
            return;
        }

        string invalidError = ttsService.GetInvalidError();
        Debug.Log(LogPrefix + " TTS GetInvalidError result: '" + (string.IsNullOrEmpty(invalidError) ? "<none>" : invalidError) + "'.");
        if (!string.IsNullOrEmpty(invalidError))
        {
            Debug.LogWarning(LogPrefix + " TTS configuration is invalid. Skipping " + reason + ". Error: " + invalidError);
            return;
        }

        bool isSpeakerBusy = ttsSpeaker.IsActive || ttsSpeaker.IsLoading || ttsSpeaker.IsSpeaking;
        Debug.Log(LogPrefix + " TTSSpeaker busy state: active=" + ttsSpeaker.IsActive + ", loading=" + ttsSpeaker.IsLoading + ", speaking=" + ttsSpeaker.IsSpeaking + ".");
        if (isSpeakerBusy)
        {
            StorePendingUtterance(textToSpeak, reason, "TTS is busy");
            return;
        }

        SpeakNow(textToSpeak, reason);
    }

    private void SpeakNow(string textToSpeak, string reason)
    {
        if (ttsSpeaker == null)
        {
            Debug.LogWarning(LogPrefix + " TTSSpeaker is missing. Cannot speak " + reason + ".");
            return;
        }

        try
        {
            TTSSpeakerClipEvents runtimeEvents = new TTSSpeakerClipEvents();
            runtimeEvents.OnLoadBegin.AddListener((speaker, clip) =>
            {
                Debug.Log(LogPrefix + " TTS load begin for " + reason + ". Text: '" + clip?.textToSpeak + "'.");
            });
            runtimeEvents.OnLoadSuccess.AddListener((speaker, clip) =>
            {
                Debug.Log(LogPrefix + " TTS load success for " + reason + ". Text: '" + clip?.textToSpeak + "'.");
            });
            runtimeEvents.OnLoadFailed.AddListener((speaker, clip, error) =>
            {
                Debug.LogWarning(LogPrefix + " TTS load failed during " + reason + ". Error: " + error);
            });
            runtimeEvents.OnPlaybackStart.AddListener((speaker, clip) =>
            {
                Debug.Log(LogPrefix + " TTS playback start for " + reason + ". Text: '" + clip?.textToSpeak + "'.");
            });
            runtimeEvents.OnPlaybackComplete.AddListener((speaker, clip) =>
            {
                Debug.Log(LogPrefix + " TTS playback complete for " + reason + ". Text: '" + clip?.textToSpeak + "'.");
            });
            runtimeEvents.OnPlaybackCancelled.AddListener((speaker, clip, error) =>
            {
                Debug.LogWarning(LogPrefix + " TTS playback cancelled during " + reason + ". Error: " + error);
            });
            runtimeEvents.OnComplete.AddListener((speaker, clip) =>
            {
                Debug.Log(LogPrefix + " TTS request complete for " + reason + ". Attempting pending flush.");
                TryFlushPendingUtterance("tts request complete");
            });

            Debug.Log(LogPrefix + " Calling TTSSpeaker.Speak for " + reason + ".");
            ttsSpeaker.Speak(textToSpeak, runtimeEvents);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(LogPrefix + " Failed to speak " + reason + ". Exception: " + exception);
            TryFlushPendingUtterance("tts exception");
        }
    }

    private void StorePendingUtterance(string textToSpeak, string reason, string blockedBy)
    {
        if (hasPendingUtterance)
        {
            Debug.LogWarning(LogPrefix + " Replacing pending utterance ('" + pendingUtterance.Reason + "') with newer request ('" + reason + "').");
        }

        pendingUtterance = new PendingUtterance
        {
            Text = textToSpeak,
            Reason = reason
        };
        hasPendingUtterance = true;
        Debug.LogWarning(LogPrefix + " Deferring " + reason + " because " + blockedBy + ". Pending utterance stored.");
    }

    private void RegisterLifecycleListenersIfNeeded()
    {
        if (listenersRegistered)
        {
            return;
        }

        if (dictationExperience != null)
        {
            dictationExperience.DictationEvents.OnStoppedListening.AddListener(HandleDictationStoppedListening);
            dictationExperience.DictationEvents.OnComplete.AddListener(HandleDictationComplete);
        }

        if (ttsSpeaker != null)
        {
            ttsSpeaker.Events.OnPlaybackComplete.AddListener(HandleTtsPlaybackComplete);
            ttsSpeaker.Events.OnPlaybackCancelled.AddListener(HandleTtsPlaybackCancelled);
            ttsSpeaker.Events.OnLoadFailed.AddListener(HandleTtsLoadFailed);
            ttsSpeaker.Events.OnComplete.AddListener(HandleTtsRequestComplete);
        }

        listenersRegistered = true;
        Debug.Log(LogPrefix + " Lifecycle listeners registered.");
    }

    private void UnregisterLifecycleListenersIfNeeded()
    {
        if (!listenersRegistered)
        {
            return;
        }

        if (dictationExperience != null)
        {
            dictationExperience.DictationEvents.OnStoppedListening.RemoveListener(HandleDictationStoppedListening);
            dictationExperience.DictationEvents.OnComplete.RemoveListener(HandleDictationComplete);
        }

        if (ttsSpeaker != null)
        {
            ttsSpeaker.Events.OnPlaybackComplete.RemoveListener(HandleTtsPlaybackComplete);
            ttsSpeaker.Events.OnPlaybackCancelled.RemoveListener(HandleTtsPlaybackCancelled);
            ttsSpeaker.Events.OnLoadFailed.RemoveListener(HandleTtsLoadFailed);
            ttsSpeaker.Events.OnComplete.RemoveListener(HandleTtsRequestComplete);
        }

        listenersRegistered = false;
        Debug.Log(LogPrefix + " Lifecycle listeners unregistered.");
    }

    private void HandleDictationStoppedListening()
    {
        Debug.Log(LogPrefix + " Lifecycle event received: dictation stopped listening.");
        TryFlushPendingUtterance("dictation stopped listening");
    }

    private void HandleDictationComplete(VoiceServiceRequest request)
    {
        Debug.Log(LogPrefix + " Lifecycle event received: dictation complete.");
        TryFlushPendingUtterance("dictation complete");
    }

    private void HandleTtsPlaybackComplete(TTSSpeaker speaker, Meta.WitAi.TTS.Data.TTSClipData clip)
    {
        Debug.Log(LogPrefix + " Lifecycle event received: TTS playback complete.");
        TryFlushPendingUtterance("tts playback complete");
    }

    private void HandleTtsPlaybackCancelled(TTSSpeaker speaker, Meta.WitAi.TTS.Data.TTSClipData clip, string error)
    {
        Debug.LogWarning(LogPrefix + " Lifecycle event received: TTS playback cancelled. Error: " + error);
        TryFlushPendingUtterance("tts playback cancelled");
    }

    private void HandleTtsLoadFailed(TTSSpeaker speaker, Meta.WitAi.TTS.Data.TTSClipData clip, string error)
    {
        Debug.LogWarning(LogPrefix + " Lifecycle event received: TTS load failed. Error: " + error);
        TryFlushPendingUtterance("tts load failed");
    }

    private void HandleTtsRequestComplete(TTSSpeaker speaker, Meta.WitAi.TTS.Data.TTSClipData clip)
    {
        Debug.Log(LogPrefix + " Lifecycle event received: TTS request complete.");
        TryFlushPendingUtterance("tts request complete");
    }

    private void TryFlushPendingUtterance(string source)
    {
        if (!hasPendingUtterance)
        {
            return;
        }

        bool micActive = dictationExperience != null && dictationExperience.MicActive;
        bool speakerBusy = ttsSpeaker != null && (ttsSpeaker.IsActive || ttsSpeaker.IsLoading || ttsSpeaker.IsSpeaking);

        if (micActive || speakerBusy)
        {
            Debug.Log(LogPrefix + " Pending utterance still blocked after " + source + ". micActive=" + micActive + ", speakerBusy=" + speakerBusy + ".");
            return;
        }

        PendingUtterance utteranceToSpeak = pendingUtterance;
        hasPendingUtterance = false;
        pendingUtterance = default;

        Debug.Log(LogPrefix + " Flushing pending utterance from " + source + ": '" + utteranceToSpeak.Reason + "'.");
        SpeakNow(utteranceToSpeak.Text, utteranceToSpeak.Reason);
    }

    private void OnDestroy()
    {
        UnregisterLifecycleListenersIfNeeded();
        hasPendingUtterance = false;
        pendingUtterance = default;
        listenersRegistered = false;
        Debug.Log(LogPrefix + " VoiceOutputFeedbackController destroyed.");
    }
}
