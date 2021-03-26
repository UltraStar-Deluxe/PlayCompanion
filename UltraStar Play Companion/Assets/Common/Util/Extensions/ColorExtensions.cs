﻿using System;
using System.Text;
using UnityEngine;

public static class ColorExtensions
{
    public static Color WithRed(this Color color, float red)
    {
        return new Color(red, color.g, color.b, color.a);
    }

    public static Color WithGreen(this Color color, float green)
    {
        return new Color(color.r, green, color.b, color.a);
    }

    public static Color WithBlue(this Color color, float blue)
    {
        return new Color(color.r, color.g, blue, color.a);
    }

    public static Color WithAlpha(this Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    public static Color RgbToHsv(this Color color)
    {
        Color.RGBToHSV(color, out float h, out float s, out float v);
        return new Color(h, s, v, color.a);
    }

    public static Color HsvToRgb(this Color color)
    {
        return Color.HSVToRGB(color.r, color.g, color.b).WithAlpha(color.a);
    }

    ///////////////////////////////////////////////////////
    // Color32
    ///////////////////////////////////////////////////////
    public static Color32 WithRed(this Color32 color, byte red)
    {
        return new Color32(red, color.g, color.b, color.a);
    }

    public static Color32 WithGreen(this Color32 color, byte green)
    {
        return new Color32(color.r, green, color.b, color.a);
    }

    public static Color32 WithBlue(this Color32 color, byte blue)
    {
        return new Color32(color.r, color.g, blue, color.a);
    }

    public static Color32 WithAlpha(this Color32 color, byte alpha)
    {
        return new Color32(color.r, color.g, color.b, alpha);
    }

    public static Color32 HsvToRgb(this Color32 color)
    {
        return Color.HSVToRGB(color.r / 255f, color.g / 255f, color.b / 255f).WithAlpha(color.a / 255f);
    }
}
