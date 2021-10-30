using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UniInject;
using UniRx;
using ProTrans;
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

    [InjectedInInspector]
    public SongListRequestor songListRequestor;
    
    [Inject]
    private ClientSideConnectRequestManager clientSideConnectRequestManager;

    [Inject]
    private ClientSideMicSampleRecorder clientSideMicSampleRecorder;

    [Inject]
    private Settings settings;
    
    [Inject(key = "#semanticVersionText")]
    private Label semanticVersionText;

    [Inject(key = "#buildTimeStampText")]
    private Label buildTimeStampText;

    [Inject(key = "#commitHashText")]
    private Label commitHashText;
    
    [Inject(key = "#fpsText")]
    private Label fpsText;
    
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

    [Inject(key = "#connectionThroubleshootingText")]
    private Label connectionThroubleshootingText;
    
    [Inject(key = "#serverErrorResponseText")]
    private Label serverErrorResponseText;

    [Inject(key = "#songListContainer")]
    private VisualElement songListContainer;
    
    [Inject(key = "#songListView")]
    private ScrollView songListView;
    
    [Inject(key = "#showSongListButton")]
    private Button showSongListButton;
    
    [Inject(key = "#closeSongListButton")]
    private Button closeSongListButton;
    
    [Inject(key = "#sceneTitle")]
    private Label sceneTitle;

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
        serverErrorResponseText.Hide();
        
        toggleRecordingButton.RegisterCallbackButtonTriggered(ToggleRecording);

        clientNameTextField.value = settings.ClientName;
        clientNameTextField.RegisterCallback<NavigationSubmitEvent>(_ => OnClientNameTextFieldSubmit());
        clientNameTextField.RegisterCallback<BlurEvent>(_ => OnClientNameTextFieldSubmit());
        
        visualizeAudioToggle.value = settings.ShowAudioWaveForm;
        audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
        visualizeAudioToggle.RegisterValueChangedCallback(changeEvent =>
        {
            audioWaveForm.SetVisible(changeEvent.newValue);
            settings.ShowAudioWaveForm = changeEvent.newValue;
        });
        
        clientSideConnectRequestManager.ConnectEventStream
            .Subscribe(UpdateConnectionStatus);

        songListRequestor.SongListEventStream.Subscribe(evt => HandleSongListEvent(evt));
        
        showSongListButton.RegisterCallbackButtonTriggered(() => ShowSongList());
        closeSongListButton.RegisterCallbackButtonTriggered(() => songListContainer.Hide());
        
        UpdateVersionInfoText();
    }

    public void UpdateTranslation()
    {
        if (!Application.isPlaying && connectionStatusText == null)
        {
            SceneInjectionManager.Instance.DoInjection();
        }
        connectionStatusText.text = TranslationManager.GetTranslation(R.Messages.connecting);
        sceneTitle.text = TranslationManager.GetTranslation(R.Messages.title);
        visualizeAudioToggle.label = TranslationManager.GetTranslation(R.Messages.visualizeMicInput);
        showSongListButton.text = TranslationManager.GetTranslation(R.Messages.songList_show);
        closeSongListButton.text = TranslationManager.GetTranslation(R.Messages.songList_hide);
    }
    
    private void HandleSongListEvent(SongListEvent evt)
    {
        songListView.Clear();
        if (!evt.ErrorMessage.IsNullOrEmpty())
        {
            AddSongListLabel(evt.ErrorMessage);
            return;
        }

        evt.LoadedSongsDto.SongList.Sort((a,b) => string.Compare(a.Artist, b.Artist, StringComparison.InvariantCulture));
        foreach (SongDto songDto in evt.LoadedSongsDto.SongList)
        {
            AddSongListLabel(songDto.Artist + " - " + songDto.Title);
        }

        if (!evt.LoadedSongsDto.IsSongScanFinished)
        {
            AddSongListLabel("...");
        }
    }

    private void ShowSongList()
    {
        songListContainer.Show();
        
        if (!songListRequestor.SuccessfullyLoadedAllSongs)
        {
            songListView.Clear();
            AddSongListLabel("Loading songs list...");
            songListRequestor.RequestSongList();
        }
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
            fpsText.text = TranslationManager.GetTranslation(R.Messages.fps, "value", fps);
            frameCount = 0;
            frameCountTime = 0;
        }
    }

    private void OnClientNameTextFieldSubmit()
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
            toggleRecordingButton.text = TranslationManager.GetTranslation(R.Messages.stopRecording);
            toggleRecordingButton.AddToClassList("stopRecordingButton");
        }
        else
        {
            toggleRecordingButton.text = TranslationManager.GetTranslation(R.Messages.startRecording);
            toggleRecordingButton.RemoveFromClassList("stopRecordingButton");
        }
    }

    private void UpdateConnectionStatus(ConnectEvent connectEvent)
    {
        if (connectEvent.IsSuccess)
        {
            connectionStatusText.text = TranslationManager.GetTranslation(R.Messages.connectedTo, "remote" , connectEvent.ServerIpEndPoint.Address);
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Show());
            audioWaveForm.SetVisible(settings.ShowAudioWaveForm);
            connectionThroubleshootingText.Hide();
            serverErrorResponseText.Hide();
            toggleRecordingButton.Focus();
            UpdateRecordingDeviceButtons();
        }
        else
        {
            connectionStatusText.text = connectEvent.ConnectRequestCount > 0
                ? TranslationManager.GetTranslation(R.Messages.connectingWithFailedAttempts, "count", connectEvent.ConnectRequestCount)
                : TranslationManager.GetTranslation(R.Messages.connecting);
            
            uiDoc.rootVisualElement.Query(null, "onlyVisibleWhenConnected").ForEach(it => it.Hide());
            if (connectEvent.ConnectRequestCount > ConnectRequestCountShowTroubleshootingHintThreshold)
            {
                connectionThroubleshootingText.Show();
                connectionThroubleshootingText.text = TranslationManager.GetTranslation(R.Messages.troubleShootingHints);
            }

            if (!connectEvent.errorMessage.IsNullOrEmpty())
            {
                serverErrorResponseText.Show();
                serverErrorResponseText.text = connectEvent.errorMessage;
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
            sampleRateButton.text = TranslationManager.GetTranslation(R.Messages.sampleRateHz, "value", sampleRate);
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
        semanticVersionText.text = TranslationManager.GetTranslation(R.Messages.version, "value", release);

        // Show the commit hash of the build
        versionProperties.TryGetValue("commit_hash", out string commitHash);
        commitHashText.text = TranslationManager.GetTranslation(R.Messages.commit, "value", commitHash);
        
        // Show the build timestamp only for development builds
        if (Debug.isDebugBuild)
        {
            versionProperties.TryGetValue("build_timestamp", out string buildTimeStamp);
            buildTimeStampText.text = TranslationManager.GetTranslation(R.Messages.buildTimeStamp, "value", buildTimeStamp);
        }
        else
        {
            buildTimeStampText.text = "";
        }
    }

    public List<UniInject.IBinding> GetBindings()
    {
        BindingBuilder bb = new BindingBuilder();
        bb.BindExistingInstance(uiDoc);
        return bb.GetBindings();
    }

    private void AddSongListLabel(string text)
    {
        Label label = new Label(text);
        label.AddToClassList("songListElement");
        label.style.whiteSpace = WhiteSpace.Normal;
        songListView.Add(label);
    }
}
