using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;
using UnityEngine.UI;
using Button = UnityEngine.UIElements.Button;
using Toggle = UnityEngine.UIElements.Toggle;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class MainSceneUiControl : MonoBehaviour, INeedInjection, UniInject.IBinder
{
    public const int ConnectRequestCountShowTroubleshootingHintThreshold = 3;
    
    [InjectedInInspector]
    public TextAsset versionPropertiesTextAsset;

    [InjectedInInspector]
    public UIDocument uiDoc;
    
    [InjectedInInspector]
    public AudioWaveFormVisualizer audioWaveFormVisualizer;

    [Inject]
    private ClientSideConnectRequestManager clientSideConnectRequestManager;

    [Inject]
    private ClientSideMicSampleRecorder clientSideMicSampleRecorder;

    [Inject]
    private Settings settings;
    
    [Inject(key = "#toggleRecordingButton")]
    private Button toggleRecordingButton;
    
    [Inject(key = "#recordingDeviceButtonContainer")]
    private VisualElement recordingDeviceButtonContainer;
    
    [Inject(key = "#sampleRateButtonContainer")]
    private VisualElement sampleRateButtonContainer;
    
    [Inject(key = "#connectionStatusText")]
    private Label connectionStatusText;

    [Inject(key = "#selectedRecordingDeviceText")]
    private Label selectedRecordingDeviceText;
    
    [Inject(key = "#clientNameTextField")]
    private TextField clientNameTextField;
    
    [Inject(key = "#visualizeAudioToggle")]
    private Toggle visualizeAudioToggle;
    
    [Inject(key = "#audioWaveForm")]
    private VisualElement audioWaveForm;

    [Inject(key = "#fpsText")]
    private Label fpsText;

    [Inject(key = "#connectionThroubleshootingText")]
    private Label connectionThroubleshootingText;
    
    private float frameCountTime;
    private int frameCount;
    
    private void Start()
    {
        clientSideMicSampleRecorder.DeviceName
            .Subscribe(_ => UpdateSelectedRecordingDeviceText());
        clientSideMicSampleRecorder.SampleRateHz
            .Subscribe(_ => UpdateSelectedRecordingDeviceText());
        clientSideMicSampleRecorder.IsRecording
            .Subscribe(OnRecordingStateChanged);
        clientSideMicSampleRecorder.SelectedRecordingDeviceEventStream
            .Subscribe(recordingDeviceEvent => UpdateSampleRateButtons(recordingDeviceEvent));

        // All controls are hidden until a connection has been established.
        uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Hide());
        connectionThroubleshootingText.Hide();
        
        toggleRecordingButton.RegisterCallbackButtonTriggered(ToggleRecording);

        clientNameTextField.value = settings.ClientName;
        clientNameTextField.RegisterCallback<NavigationSubmitEvent>(_ => OnClientNameTextFieldChanged());
        clientNameTextField.RegisterCallback<BlurEvent>(_ => OnClientNameTextFieldChanged());

        visualizeAudioToggle.value = settings.ShowAudioWaveForm;
        audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
        visualizeAudioToggle.RegisterValueChangedCallback(changeEvent =>
        {
            audioWaveForm.SetVisible(changeEvent.newValue);
            settings.ShowAudioWaveForm = changeEvent.newValue;
        });
        
        clientSideConnectRequestManager.ConnectEventStream
            .Subscribe(UpdateConnectionStatus);
        
        UpdateVersionInfoText();
    }

    private void Update()
    {
        if (audioWaveForm.style.display != DisplayStyle.None)
        {
            audioWaveFormVisualizer.DrawWaveFormMinAndMaxValues(clientSideMicSampleRecorder.micSampleBuffer);
        }
        UpdateFps();
    }

    private void UpdateFps()
    {
        frameCountTime += Time.deltaTime;
        frameCount++;
        if (frameCountTime > 1)
        {
            int fps = (int)(frameCount / frameCountTime);
            fpsText.text = "FPS: " + fps;
            frameCount = 0;
            frameCountTime = 0;
        }
    }

    private void OnClientNameTextFieldChanged()
    {
        settings.ClientName = clientNameTextField.value;
        // Reconnect to let the main know about the new clientName.
        clientSideConnectRequestManager.CloseConnectionAndReconnect();
    }

    private void UpdateSelectedRecordingDeviceText()
    {
        selectedRecordingDeviceText.text = $"{clientSideMicSampleRecorder.DeviceName}\n({clientSideMicSampleRecorder.SampleRateHz} Hz)";
    }

    private void OnRecordingStateChanged(bool isRecording)
    {
        if (isRecording)
        {
            toggleRecordingButton.text = "Stop Recording";
            toggleRecordingButton.AddToClassList("stopRecordingButton");
        }
        else
        {
            toggleRecordingButton.text = "Start Recording";
            toggleRecordingButton.RemoveFromClassList("stopRecordingButton");
        }
    }

    private void UpdateConnectionStatus(ConnectEvent connectEvent)
    {
        if (connectEvent.IsSuccess)
        {
            connectionStatusText.text = $"Connected To {connectEvent.ServerIpEndPoint.Address}";
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Show());
            audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
            connectionThroubleshootingText.Hide();
            toggleRecordingButton.Focus();
            UpdateRecordingDeviceButtons();
        }
        else
        {
            connectionStatusText.text = connectEvent.ConnectRequestCount > 0
                ? $"Connecting...\n(attempt {connectEvent.ConnectRequestCount} failed)"
                : "Connecting...";
            
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Hide());
            if (connectEvent.ConnectRequestCount > ConnectRequestCountShowTroubleshootingHintThreshold)
            {
                connectionThroubleshootingText.Show();
                connectionThroubleshootingText.text = "Troubleshooting hints:\n"
                    + "- Check main game is running\n\n"
                    + "- Check main game and Companion app use the same WLAN\n\n"
                    + "- Try to temporarily disable firewall (on Windows)\n\n";
            }
        }
    }

    private void UpdateRecordingDeviceButtons()
    {
        recordingDeviceButtonContainer.Clear();
        if (Microphone.devices.Length <= 1)
        {
            // No real choice
            return;
        }
        
        Microphone.devices.ForEach(device =>
        {
            Button deviceButton = new Button();
            deviceButton.RegisterCallbackButtonTriggered(
                () => clientSideMicSampleRecorder.SelectRecordingDevice(device));
            deviceButton.text = $"{device}";
            recordingDeviceButtonContainer.Add(deviceButton);
        });
    }

    private void UpdateSampleRateButtons(RecordingDeviceEvent recordingDeviceEvent)
    {
        sampleRateButtonContainer.Clear();
        if (recordingDeviceEvent.minSampleRateHz == recordingDeviceEvent.maxSampleRateHz)
        {
            // No real choice
            return;
        }

        List<int> sampleRates = new List<int>
        {
            recordingDeviceEvent.minSampleRateHz,
            recordingDeviceEvent.maxSampleRateHz,        
        };
        sampleRates.ForEach(sampleRate =>
        {
            Button sampleRateButton = new Button();
            sampleRateButton.RegisterCallbackButtonTriggered(
                () => clientSideMicSampleRecorder.SelectRecordingDevice(clientSideMicSampleRecorder.DeviceName.Value, sampleRate));
            sampleRateButton.text = $"Sample rate: {sampleRate} Hz";
            recordingDeviceButtonContainer.Add(sampleRateButton);
        });
    }
    
    private void ToggleRecording()
    {
        if (clientSideMicSampleRecorder.IsRecording.Value)
        {
            clientSideMicSampleRecorder.StopRecording();
        }
        else
        {
            clientSideMicSampleRecorder.StartRecording();
        }
    }

    private void UpdateVersionInfoText()
    {
        Dictionary<string, string> versionProperties = PropertiesFileParser.ParseText(versionPropertiesTextAsset.text);

        // Show the release number (e.g. release date, or some version number)
        versionProperties.TryGetValue("release", out string release);
        uiDoc.rootVisualElement.Q<Label>("releaseInfoText").text = "Release: " + release;

        // Show the build timestamp only for development builds
        if (Debug.isDebugBuild)
        {
            versionProperties.TryGetValue("build_timestamp", out string buildTimeStamp);
            uiDoc.rootVisualElement.Q<Label>("buildInfoText").text = "Build: " + buildTimeStamp;
        }
        else
        {
            uiDoc.rootVisualElement.Q<Label>("buildInfoText").text = "";
        }
    }

    public List<UniInject.IBinding> GetBindings()
    {
        BindingBuilder bb = new BindingBuilder();
        bb.BindExistingInstance(uiDoc);
        return bb.GetBindings();
    }
}
