using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace LeitorEPUB.Helpers;

public class LanguageHelper
{
    private Dictionary<string, string> _strings = new();
    private string _currentLanguage = "pt";
    
    public string CurrentLanguage => _currentLanguage;

    public LanguageHelper()
    {
        LoadLanguage("pt");
    }

    public void LoadLanguage(string languageCode)
    {
        var exeDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(exeDir))
            exeDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
        
        var path = Path.Combine(exeDir, "Resources", "Languages", languageCode + ".json");
        
        if (!File.Exists(path))
        {
            path = Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Languages", languageCode + ".json");
        }
        
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            if (data != null)
            {
                _strings = data;
                _currentLanguage = languageCode;
            }
        }
    }

    public string T(string key, params object[] args)
    {
        if (_strings == null || !_strings.ContainsKey(key))
            return key;
        
        var text = _strings[key];
        if (args != null && args.Length > 0)
        {
            try { return string.Format(text, args); }
            catch { return text; }
        }
        return text;
    }
}