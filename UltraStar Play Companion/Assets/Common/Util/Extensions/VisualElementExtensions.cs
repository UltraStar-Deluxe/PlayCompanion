using System;
using UnityEngine;
using UnityEngine.UIElements;

public static class VisualElementExtensions
{
    public static void RegisterCallbackButtonTriggered(this Button button, Action callback)
    {
        button.RegisterCallback<ClickEvent>(_ => callback());
        button.RegisterCallback<NavigationSubmitEvent>(_ => callback());
        button.RegisterCallback<KeyDownEvent>(e =>
        {
            if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
            {
                callback();
            }
        });
    }

    public static void Hide(this VisualElement visualElement)
    {
        visualElement.style.display = DisplayStyle.None;
    }
    
    public static void Show(this VisualElement visualElement)
    {
        visualElement.style.display = DisplayStyle.Flex;
    }
    
    public static void SetVisible(this VisualElement visualElement, bool isVisible)
    {
        if (isVisible)
        {
            visualElement.Show();
        }
        else
        {
            visualElement.Hide();
        }
    }
}
