using UnityEngine;
using UnityEngine.UI;

public class VoiceNamingController : MonoBehaviour
{
	[SerializeField] private string mockTranscript = "test item";

	private IVoiceTranscriptionProvider transcriptionProvider;

	private void Awake()
	{
		transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
	}

	public void RequestTranscriptIntoField(InputField targetInputField)
	{
		Debug.Log("[VoiceNaming] Voice button pressed");

		if (targetInputField == null)
		{
			Debug.LogWarning("[VoiceNaming] Target name input field is missing.");
			return;
		}

		if (transcriptionProvider == null)
		{
			transcriptionProvider = new MockVoiceTranscriptionProvider(mockTranscript);
		}

		Debug.Log("[VoiceNaming] Mock listening started");

		transcriptionProvider.StartListening((transcript) =>
		{
			Debug.Log("[VoiceNaming] Mock transcript received: " + transcript);
			targetInputField.text = transcript;
			Debug.Log("[VoiceNaming] Transcript applied to name field");
		});
	}
}
