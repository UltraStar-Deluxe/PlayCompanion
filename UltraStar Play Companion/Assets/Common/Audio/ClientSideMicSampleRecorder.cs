using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using UniInject;
using UnityEngine;
using UniRx;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class ClientSideMicSampleRecorder: MonoBehaviour, INeedInjection
{
    public static ClientSideMicSampleRecorder Instance
    {
        get
        {
            return GameObjectUtils.FindComponentWithTag<ClientSideMicSampleRecorder>("ClientSideMicSampleRecorder");
        }
    }
    
    public string DeviceName { get; private set; }

    public bool playRecordedAudio;
    
    public bool IsRecording { get; private set; }
    public int SampleRateHz { get; private set; }
    
    // The MicSamples array has the length of the SampleRateHz (one float value per sample.)
    public float[] MicSamples { get; private set; }

    private AudioClip micAudioClip;
    
    [Inject(searchMethod = SearchMethods.GetComponent)]
    private AudioSource audioSource;
    
    private int lastSamplePosition;
    
    private Subject<RecordingEvent> recordingEventStream = new Subject<RecordingEvent>();
    public IObservable<RecordingEvent> RecordingEventStream => recordingEventStream;
    
    private readonly Subject<RecordingDeviceEvent> selectedRecordingDeviceEventStream = new Subject<RecordingDeviceEvent>();
    public IObservable<RecordingDeviceEvent> SelectedRecordingDeviceEventStream => selectedRecordingDeviceEventStream;
    
    private void Start()
    {
        SelectRecordingDevice(Microphone.devices.FirstOrDefault());
    }

    public void SelectRecordingDevice(string deviceName)
    {
        if (IsRecording)
        {
            StopRecording();
        }
        DeviceName = deviceName;
        Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);
        SampleRateHz = maxFreq;
        if (SampleRateHz > 0)
        {
            MicSamples = new float[SampleRateHz];
        }

        selectedRecordingDeviceEventStream.OnNext(new RecordingDeviceEvent(DeviceName));
        
        Debug.Log($"Selected recording device '{DeviceName}' with sample rate {SampleRateHz} Hz");
    }

    private void Update()
    {
        UpdateMicrophoneAudioPlayback();
        if (IsRecording)
        {
            UpdateRecording();
        }
    }

    public void StartRecording()
    {
        if (IsRecording)
        {
            throw new UnityException("Already recording");
        }
        if (DeviceName.IsNullOrEmpty())
        {
            throw new UnityException("Cannot start recording. No recording device selected.");
        }
        if (SampleRateHz <= 0)
        {
            throw new UnityException($"Cannot start recording. Sample rate is invalid: {SampleRateHz}.");
        }
        
        Debug.Log($"Starting recording with '{DeviceName}' at {SampleRateHz} Hz");

        // Code for low-latency microphone input taken from
        // https://support.unity3d.com/hc/en-us/articles/206485253-How-do-I-get-Unity-to-playback-a-Microphone-input-in-real-time-
        micAudioClip = UnityEngine.Microphone.Start(DeviceName, true, 1, SampleRateHz);
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        while (UnityEngine.Microphone.GetPosition(DeviceName) <= 0)
        {
            // <Busy waiting>
            // Emergency exit
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                IsRecording = false;
                Debug.LogError("Microphone did not provide any samples. Took emergency exit out of busy waiting.");
                return;
            }
        }

        // Configure audio playback
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = micAudioClip;
        audioSource.loop = true;
        
        IsRecording = true;
    }
    
    private void UpdateRecording()
    {
        if (!IsRecording)
        {
            return;
        }

        if (micAudioClip == null)
        {
            Debug.LogError("AudioClip for microphone is null");
            StopRecording();
            return;
        }

        // Fill buffer with raw sample data from microphone
        int currentSamplePosition = UnityEngine.Microphone.GetPosition(DeviceName);
        micAudioClip.GetData(MicSamples, currentSamplePosition);

        // Process the portion that has been buffered by Unity since the last frame.
        // New samples come into the buffer "from the right", i.e., highest index holds the newest sample.
        int newSamplesCount = GetNewSampleCountInCircularBuffer(lastSamplePosition, currentSamplePosition);
        int newSamplesStartIndex = MicSamples.Length - newSamplesCount;
        int newSamplesEndIndex = MicSamples.Length - 1;

        // Notify listeners
        RecordingEvent recordingEvent = new RecordingEvent(MicSamples, newSamplesStartIndex, newSamplesEndIndex);
        recordingEventStream.OnNext(recordingEvent);

        lastSamplePosition = currentSamplePosition;
    }
    
    private int GetNewSampleCountInCircularBuffer(int lastSamplePosition, int currentSamplePosition)
    {
        int bufferLength = MicSamples.Length;

        // Check if the recording re-started from index 0 after reaching the end of the buffer.
        if (currentSamplePosition <= lastSamplePosition)
        {
            return (bufferLength - lastSamplePosition) + currentSamplePosition;
        }
        else
        {
            return currentSamplePosition - lastSamplePosition;
        }
    }
    
    public void StopRecording()
    {
        Debug.Log($"Stopping recording with '{DeviceName}'");
        IsRecording = false;
        UnityEngine.Microphone.End(DeviceName);
    }
    
    private void UpdateMicrophoneAudioPlayback()
    {
        if (playRecordedAudio && !audioSource.isPlaying)
        {
            audioSource.Play();
        }
        else if (!playRecordedAudio && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
}
