using System;
using UnityEngine;

public class MockVoiceTranscriptionProvider : IVoiceTranscriptionProvider
{
	private const string LogPrefix = "[VoiceNaming]";

	private readonly string transcript;

	public MockVoiceTranscriptionProvider(string transcript)
	{
		this.transcript = transcript;
	}

	public void StartListening(Action<string> onTranscriptReceived, Action<string> onError = null)
	{
		Debug.Log(LogPrefix + " Mock listening started.");
		Debug.Log(LogPrefix + " Mock transcript received: " + transcript);
		onTranscriptReceived?.Invoke(transcript);
	}
}
