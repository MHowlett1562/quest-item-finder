using System;

public interface IVoiceTranscriptionProvider
{
	void StartListening(Action<string> onTranscriptReceived);
}
