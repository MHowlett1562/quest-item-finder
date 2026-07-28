using Meta.WitAi.Requests;
using Oculus.Voice.Dictation;
using UnityEngine;
using UnityEngine.UI;

public class VoiceDictationSmokeTestController : MonoBehaviour
{
    [SerializeField] private AppDictationExperience dictationExperience;
    [SerializeField] private Text statusText;
    [SerializeField] private Text transcriptText;

    [SerializeField] private string readyStatus = "Ready";
    [SerializeField] private string listeningStatus = "Listening";
    [SerializeField] private string partialStatus = "Partial transcript";
    [SerializeField] private string finalStatus = "Final transcript";
    [SerializeField] private string errorStatus = "Error/unavailable";

    private void Reset()
    {
        if (dictationExperience == null)
        {
            dictationExperience = GetComponent<AppDictationExperience>();
        }
    }

    private void Awake()
    {
        if (dictationExperience == null)
        {
            dictationExperience = GetComponent<AppDictationExperience>();
        }

        if (dictationExperience == null)
        {
            SetStatus(errorStatus);
            SetTranscript("Dictation component missing.");
            return;
        }

        if (dictationExperience.RuntimeDictationConfiguration == null ||
            dictationExperience.RuntimeDictationConfiguration.witConfiguration == null)
        {
            SetStatus(errorStatus);
            SetTranscript("Wit configuration not assigned.");
            return;
        }

        SetStatus(readyStatus);
    }

    private void OnEnable()
    {
        if (dictationExperience == null)
        {
            return;
        }

        dictationExperience.DictationEvents.OnStartListening.AddListener(OnStartListening);
        dictationExperience.DictationEvents.OnStoppedListening.AddListener(OnStoppedListening);
        dictationExperience.DictationEvents.OnPartialTranscription.AddListener(OnPartialTranscription);
        dictationExperience.DictationEvents.OnFullTranscription.AddListener(OnFullTranscription);
        dictationExperience.DictationEvents.OnError.AddListener(OnError);
        dictationExperience.DictationEvents.OnComplete.AddListener(OnComplete);
    }

    private void OnDisable()
    {
        if (dictationExperience == null)
        {
            return;
        }

        dictationExperience.DictationEvents.OnStartListening.RemoveListener(OnStartListening);
        dictationExperience.DictationEvents.OnStoppedListening.RemoveListener(OnStoppedListening);
        dictationExperience.DictationEvents.OnPartialTranscription.RemoveListener(OnPartialTranscription);
        dictationExperience.DictationEvents.OnFullTranscription.RemoveListener(OnFullTranscription);
        dictationExperience.DictationEvents.OnError.RemoveListener(OnError);
        dictationExperience.DictationEvents.OnComplete.RemoveListener(OnComplete);
    }

    private void OnStartListening()
    {
        SetStatus(listeningStatus);
    }

    private void OnStoppedListening()
    {
        if (statusText != null && statusText.text != errorStatus)
        {
            statusText.text = readyStatus;
        }
    }

    private void OnPartialTranscription(string text)
    {
        SetStatus(partialStatus);
        SetTranscript("Partial: " + text);
    }

    private void OnFullTranscription(string text)
    {
        SetStatus(finalStatus);
        SetTranscript("Final: " + text);
    }

    private void OnError(string errorCode, string errorMessage)
    {
        SetStatus(errorStatus);
        SetTranscript("[" + errorCode + "] " + errorMessage);
    }

    private void OnComplete(VoiceServiceRequest request)
    {
        if (statusText != null && statusText.text != errorStatus)
        {
            statusText.text = readyStatus;
        }
    }

    private void SetStatus(string value)
    {
        if (statusText != null)
        {
            statusText.text = value;
        }
    }

    private void SetTranscript(string value)
    {
        if (transcriptText != null)
        {
            transcriptText.text = value;
        }
    }
}
