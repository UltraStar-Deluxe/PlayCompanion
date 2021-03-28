﻿using System;
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

    private const int DefaultSampleRateHz = 44100;

    public ReactiveProperty<string> DeviceName { get; private set; } = new ReactiveProperty<string>();
    public ReactiveProperty<int> SampleRateHz { get; private set; } = new ReactiveProperty<int>();
    public ReactiveProperty<bool> IsRecording { get; private set; } = new ReactiveProperty<bool>();
    
    // The MicSamples array has the length of the SampleRateHz (one float value per sample.)
    public float[] micSampleBuffer { get; private set; }

    [Inject(searchMethod = SearchMethods.GetComponent)]
    private AudioSource audioSource;
    
    [Inject]
    private Settings settings;
    
    private AudioClip micAudioClip;
    
    private int lastSamplePosition;
    
    private readonly Subject<RecordingDeviceEvent> selectedRecordingDeviceEventStream = new Subject<RecordingDeviceEvent>();
    public IObservable<RecordingDeviceEvent> SelectedRecordingDeviceEventStream => selectedRecordingDeviceEventStream;
    
    private readonly Subject<RecordingEvent> recordingEventStream = new Subject<RecordingEvent>();
    public IObservable<RecordingEvent> RecordingEventStream => recordingEventStream;
    
    private void Start()
    {
        string initialRecordingDeviceName = settings.RecordingDeviceName.IsNullOrEmpty() || !Microphone.devices.Contains(settings.RecordingDeviceName)
            ? Microphone.devices.FirstOrDefault()
            : settings.RecordingDeviceName;
        SelectRecordingDevice(initialRecordingDeviceName);
        DeviceName.Subscribe(newValue => settings.RecordingDeviceName = newValue);
    }

    public void SelectRecordingDevice(string deviceName, int sampleRate = 0)
    {
        if (IsRecording.Value)
        {
            StopRecording();
        }
        DeviceName.Value = deviceName;
        Microphone.GetDeviceCaps(deviceName, out int minFreq, out int maxFreq);

        if (sampleRate == minFreq)
        {
            SampleRateHz.Value = minFreq;
        }
        else
        {
            SampleRateHz.Value = maxFreq;
        }

        if (SampleRateHz.Value == 0)
        {
            // A value of 0 indicates that any sample rate can be used
            SampleRateHz.Value = DefaultSampleRateHz;
        }
        
        if (SampleRateHz.Value > 0)
        {
            micSampleBuffer = new float[SampleRateHz.Value];
        }

        selectedRecordingDeviceEventStream.OnNext(new RecordingDeviceEvent(DeviceName.Value, minFreq, maxFreq));
        
        Debug.Log($"Selected recording device '{DeviceName}' with sample rate {SampleRateHz} Hz");
    }

    private void Update()
    {
        if (IsRecording.Value)
        {
            UpdateRecording();
        }
    }

    public void StartRecording()
    {
        if (IsRecording.Value)
        {
            throw new UnityException("Already recording");
        }
        if (DeviceName.Value.IsNullOrEmpty())
        {
            throw new UnityException("Cannot start recording. No recording device selected.");
        }
        if (SampleRateHz.Value <= 0)
        {
            throw new UnityException($"Cannot start recording. Sample rate is invalid: {SampleRateHz}.");
        }
        
        Debug.Log($"Starting recording with '{DeviceName}' at {SampleRateHz} Hz");

        // Code for low-latency microphone input taken from
        // https://support.unity3d.com/hc/en-us/articles/206485253-How-do-I-get-Unity-to-playback-a-Microphone-input-in-real-time-
        micAudioClip = Microphone.Start(DeviceName.Value, true, 1, SampleRateHz.Value);
        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        stopwatch.Start();
        while (Microphone.GetPosition(DeviceName.Value) <= 0)
        {
            // <Busy waiting>
            // Emergency exit
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                IsRecording.Value = false;
                Debug.LogError("Microphone did not provide any samples. Took emergency exit out of busy waiting.");
                return;
            }
        }

        // Configure audio playback
        audioSource = GetComponent<AudioSource>();
        audioSource.clip = micAudioClip;
        audioSource.loop = true;
        
        IsRecording.Value = true;
    }
    
    private void UpdateRecording()
    {
        if (!IsRecording.Value)
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
        int currentSamplePosition = Microphone.GetPosition(DeviceName.Value);
        micAudioClip.GetData(micSampleBuffer, currentSamplePosition);
        if (currentSamplePosition == lastSamplePosition)
        {
            // No new samples yet (or all samples changed, which is unlikely because the buffer has a length of 1 second and FPS should be > 1).
            return;
        }

        // Process the portion that has been buffered by Unity since the last frame.
        // New samples come into the buffer "from the right", i.e., highest index holds the newest sample.
        int newSamplesCount = GetNewSampleCountInCircularBuffer(lastSamplePosition, currentSamplePosition);
        int newSamplesStartIndex = micSampleBuffer.Length - newSamplesCount;
        int newSamplesEndIndex = micSampleBuffer.Length - 1;

        // Notify listeners
        RecordingEvent recordingEvent = new RecordingEvent(micSampleBuffer, newSamplesStartIndex, newSamplesEndIndex);
        recordingEventStream.OnNext(recordingEvent);

        lastSamplePosition = currentSamplePosition;
    }
    
    private int GetNewSampleCountInCircularBuffer(int lastSamplePosition, int currentSamplePosition)
    {
        int bufferLength = micSampleBuffer.Length;

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
        if (!IsRecording.Value)
        {
            return;
        }
        
        Debug.Log($"Stopping recording with '{DeviceName}'");
        IsRecording.Value = false;
        Microphone.End(DeviceName.Value);
    }
}
