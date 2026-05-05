﻿using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace LeitorEPUB.Services;

public static class ThemeService
{
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, uint attr, ref int attrValue, int attrSize);

    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const uint DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

    public static bool IsDarkMode { get; private set; }
    public static string CurrentTheme { get; private set; } = "system";

    public static void ApplyTheme(string theme)
    {
        CurrentTheme = theme;
        IsDarkMode = theme switch
        {
            "dark" => true,
            "light" => false,
            "system" => IsSystemDark(),
            _ => false
        };
        UpdateResources();
    }

    private static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            return value is int intValue && intValue == 0;
        }
        catch { return false; }
    }

    public static void ApplyTitleBarTheme(Window window)
    {
        if (!IsDarkMode) return;

        try
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(window);
            var hwnd = helper.EnsureHandle();

            int useDarkMode = 1;
            int result = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDarkMode, sizeof(int));
            if (result != 0)
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD,
                    ref useDarkMode, sizeof(int));
        }
        catch { }
    }

    private static void UpdateResources()
    {
        var app = Application.Current;
        if (app == null) return;
        var resources = app.Resources;
        if (resources == null) return;

        if (IsDarkMode)
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            resources["ForegroundBrush"] = new SolidColorBrush(Colors.White);
            resources["TextBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(40, 40, 40));
            resources["ButtonBrush"] = new SolidColorBrush(Color.FromRgb(50, 50, 50));
            resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(80, 80, 80));
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(70, 70, 70));
            resources["ChoiceBrush"] = new SolidColorBrush(Color.FromRgb(60, 60, 60));
            resources["PanelBrush"] = new SolidColorBrush(Color.FromRgb(35, 35, 35));
            resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(35, 35, 35));
            resources["MenuItemHoverBrush"] = new SolidColorBrush(Color.FromRgb(70, 70, 70));
            resources["SubMenuBrush"] = new SolidColorBrush(Color.FromRgb(45, 45, 45));
            resources["SubMenuBorderBrush"] = new SolidColorBrush(Color.FromRgb(70, 70, 70));
        }
        else
        {
            resources["BackgroundBrush"] = new SolidColorBrush(Colors.White);
            resources["ForegroundBrush"] = new SolidColorBrush(Colors.Black);
            resources["TextBackgroundBrush"] = new SolidColorBrush(Colors.White);
            resources["ButtonBrush"] = new SolidColorBrush(Color.FromRgb(240, 240, 240));
            resources["ButtonHoverBrush"] = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            resources["BorderBrush"] = new SolidColorBrush(Color.FromRgb(210, 210, 210));
            resources["ChoiceBrush"] = new SolidColorBrush(Colors.White);
            resources["PanelBrush"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            resources["MenuBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            resources["MenuItemHoverBrush"] = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            resources["SubMenuBrush"] = new SolidColorBrush(Colors.White);
            resources["SubMenuBorderBrush"] = new SolidColorBrush(Color.FromRgb(210, 210, 210));
        }

        foreach (Window window in Application.Current.Windows)
        {
            window.InvalidateVisual();
        }
    }
}