using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;
using UnityEngine.UI;
using Button = UnityEngine.UIElements.Button;

// Disable warning about fields that are never assigned, their values are injected.
#pragma warning disable CS0649

public class MainSceneUiControl : MonoBehaviour, INeedInjection, UniInject.IBinder
{
    [InjectedInInspector]
    public TextAsset versionPropertiesTextAsset;

    [InjectedInInspector]
    public UIDocument uiDoc;

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
    
    [Inject(key = "#reconnectButton")]
    private Button reconnectButton;
    
    [Inject(key = "#clientNameTextField")]
    private TextField clientNameTextField;
    
    [Inject(key = "#controlsContainer")]
    private VisualElement controlsContainer;
    
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
        controlsContainer.Hide();
        
        toggleRecordingButton.RegisterCallbackButtonTriggered(ToggleRecording);
        reconnectButton.RegisterCallbackButtonTriggered(() => clientSideConnectRequestManager.CloseConnectionAndReconnect());

        clientNameTextField.value = settings.ClientName;
        clientNameTextField.RegisterCallback<NavigationSubmitEvent>(_ => OnClientNameTextFieldChanged());
        clientNameTextField.RegisterCallback<BlurEvent>(_ => OnClientNameTextFieldChanged());

        clientSideConnectRequestManager.ConnectEventStream
            .Subscribe(UpdateConnectionStatus);
        
        UpdateVersionInfoText();
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
            controlsContainer.Show();
            toggleRecordingButton.Focus();
            UpdateRecordingDeviceButtons();
        }
        else
        {
            connectionStatusText.text = connectEvent.ConnectRequestCount > 0
                ? $"Connecting...\n(attempt {connectEvent.ConnectRequestCount} failed)"
                : "Connecting...";
            
            controlsContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
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
