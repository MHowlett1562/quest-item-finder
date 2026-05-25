using System;

public class MockVoiceTranscriptionProvider : IVoiceTranscriptionProvider
{
	private readonly string transcript;

	public MockVoiceTranscriptionProvider(string transcript)
	{
		this.transcript = transcript;
	}

	public void StartListening(Action<string> onTranscriptReceived)
	{
		onTranscriptReceived?.Invoke(transcript);
	}
}
