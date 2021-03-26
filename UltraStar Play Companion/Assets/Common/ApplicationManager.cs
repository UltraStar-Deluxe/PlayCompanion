﻿using System;
using System.Collections.Generic;
using UnityEngine;

public class ApplicationManager : MonoBehaviour
{
    public static ApplicationManager Instance
    {
        get
        {
            return GameObjectUtils.FindComponentWithTag<ApplicationManager>("ApplicationManager");
        }
    }

    public List<string> simulatedCommandLineArguments = new List<string>();

    [Range(-1, 60)]
    public int targetFrameRate = 30;

    void Start()
    {
        targetFrameRate = SettingsManager.Instance.Settings.TargetFps;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = targetFrameRate;
    }

    void Update()
    {
        if (Application.targetFrameRate != targetFrameRate)
        {
            Application.targetFrameRate = targetFrameRate;
        }
    }

    void OnEnable()
    {
        Application.logMessageReceived += Log.HandleUnityLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log.HandleUnityLog;
    }

    public bool HasCommandLineArgument(string argumentName)
    {
        string[] args = GetCommandLineArguments();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public string GetCommandLineArgument(string argumentName)
    {
        string[] args = GetCommandLineArguments();
        for (int i = 0; i < (args.Length - 1); i++)
        {
            if (string.Equals(args[i], argumentName, StringComparison.InvariantCultureIgnoreCase))
            {
                return args[i + 1];
            }
        }
        return "";
    }

    public string[] GetCommandLineArguments()
    {
        if (Application.isEditor)
        {
            return simulatedCommandLineArguments.ToArray();
        }
        else
        {
            if (PlatformUtils.IsStandalone)
            {
                return System.Environment.GetCommandLineArgs();
            }
            else
            {
                return Array.Empty<string>();
            }
        }
    }
}
