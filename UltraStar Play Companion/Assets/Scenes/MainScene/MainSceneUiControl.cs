using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;

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
    
    [Inject(key = "#toggleRecordingButton")]
    private Button toggleRecordingButton;
    
    [Inject(key = "#recordingDeviceButtonContainer")]
    private VisualElement recordingDeviceButtonContainer;
    
    [Inject(key = "#connectionStatusText")]
    private Label connectionStatusText;

    [Inject(key = "#selectedRecordingDeviceText")]
    private Label selectedRecordingDeviceText;
    
    [Inject(key = "#reconnectButton")]
    private Button reconnectButton;
    
    private void Start()
    {
        clientSideMicSampleRecorder.SelectedRecordingDeviceEventStream
            .Subscribe(evt => selectedRecordingDeviceText.text = evt.DeviceName);
        selectedRecordingDeviceText.text = clientSideMicSampleRecorder.DeviceName;

        // Make all Buttons focusable
        uiDoc.rootVisualElement.Query<Button>().ForEach(button => button.focusable = true);

        reconnectButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        toggleRecordingButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
        
        toggleRecordingButton.RegisterCallbackButtonTriggered(() => ToggleRecording());
        reconnectButton.RegisterCallbackButtonTriggered(() => clientSideConnectRequestManager.CloseConnectionAndReconnect());
        
        UpdateVersionInfoText();

        clientSideConnectRequestManager.ConnectEventStream.Subscribe(UpdateConnectionStatus);
    }

    private void UpdateConnectionStatus(ConnectEvent connectEvent)
    {
        if (connectEvent.IsSuccess)
        {
            connectionStatusText.text = $"Connected To {connectEvent.ServerIpEndPoint.Address.MapToIPv4()}";
            
            reconnectButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            toggleRecordingButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
            toggleRecordingButton.Focus();

            CreateRecordingDeviceButtons();
        }
        else
        {
            connectionStatusText.text = connectEvent.ConnectRequestCount > 0
                ? $"Connecting... (attempt {connectEvent.ConnectRequestCount} failed)"
                : "Connecting...";
            
            reconnectButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            toggleRecordingButton.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);

            ClearRecordingDeviceButtons();
        }
    }

    private void ClearRecordingDeviceButtons()
    {
        recordingDeviceButtonContainer.Clear();
    }

    private void CreateRecordingDeviceButtons()
    {
        foreach (string device in Microphone.devices)
        {
            Button deviceButton = new Button();
            deviceButton.RegisterCallbackButtonTriggered(() => clientSideMicSampleRecorder.SelectRecordingDevice(device));
            deviceButton.text = $"{device}";
            deviceButton.style.width = new StyleLength(Length.Percent(100));
            recordingDeviceButtonContainer.Add(deviceButton);
        }
    }

    private void ToggleRecording()
    {
        if (clientSideMicSampleRecorder.IsRecording)
        {
            clientSideMicSampleRecorder.StopRecording();
        }
        else
        {
            clientSideMicSampleRecorder.StartRecording();
        }
        
        toggleRecordingButton.text = clientSideMicSampleRecorder.IsRecording
            ? "Stop Recording"
            : "Start Recording";
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
