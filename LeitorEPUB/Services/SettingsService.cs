using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using LeitorEPUB.Models;
namespace LeitorEPUB.Services;
public class SettingsService
{
    private readonly string _progressPath;
    private readonly string _preferencesPath;
    public Dictionary<string, object> Preferences { get; private set; }
    public SettingsService()
    {
        var appData = Helpers.FileHelper.GetAppDataPath();
        _progressPath = Path.Combine(appData, "progress.json");
        _preferencesPath = Path.Combine(appData, "preferences.json");
        Preferences = new Dictionary<string, object>
        {
            ["voice"] = null,
            ["speed"] = 1.0,
            ["language"] = "pt",
            ["theme"] = "system"
        };
        LoadPreferences();
    }
    private void LoadPreferences()
    {
        if (File.Exists(_preferencesPath))
        {
            try
            {
                var json = File.ReadAllText(_preferencesPath);
                var prefs = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                foreach (var kvp in prefs)
                {
                    Preferences[kvp.Key] = kvp.Value;
                }
            }
            catch { }
        }
    }
    public void SavePreferences()
    {
        var json = JsonConvert.SerializeObject(Preferences, Formatting.Indented);
        File.WriteAllText(_preferencesPath, json);
    }
    public ReadingProgress LoadProgress(string filePath)
    {
        if (!File.Exists(_progressPath)) return null;
        try
        {
            var hash = Helpers.FileHelper.ComputeHash(filePath);
            var json = File.ReadAllText(_progressPath);
            var data = JsonConvert.DeserializeObject<Dictionary<string, ReadingProgress>>(json);
            return data.ContainsKey(hash) ? data[hash] : null;
        }
        catch
        {
            return null;
        }
    }
    public void SaveProgress(string filePath, int globalIndex, int totalParagraphs, double speed)
    {
        var hash = Helpers.FileHelper.ComputeHash(filePath);
        Dictionary<string, ReadingProgress> data;
        if (File.Exists(_progressPath))
        {
            var json = File.ReadAllText(_progressPath);
            data = JsonConvert.DeserializeObject<Dictionary<string, ReadingProgress>>(json);
        }
        else
        {
            data = new Dictionary<string, ReadingProgress>();
        }
        data[hash] = new ReadingProgress
        {
            File = filePath,
            GlobalIndex = globalIndex,
            TotalParagraphs = totalParagraphs,
            Speed = speed,
            LastAccess = DateTime.Now
        };
        var newJson = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(_progressPath, newJson);
    }
    public bool DeleteAllData()
    {
        try
        {
            if (File.Exists(_progressPath)) File.Delete(_progressPath);
            if (File.Exists(_preferencesPath)) File.Delete(_preferencesPath);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
