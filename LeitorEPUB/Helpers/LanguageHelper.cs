using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            var token = JToken.Parse(json);
            
            if (token is JObject jObject)
            {
                // Verifica se é formato hierárquico (tem objetos aninhados) ou plano
                bool isHierarchical = false;
                foreach (var prop in jObject.Properties())
                {
                    if (prop.Value is JObject)
                    {
                        isHierarchical = true;
                        break;
                    }
                }
                
                if (isHierarchical)
                {
                    // Formato hierárquico: achata as chaves
                    _strings = new Dictionary<string, string>();
                    FlattenJson(jObject, "");
                }
                else
                {
                    // Formato plano (compatível com versão anterior)
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                    if (data != null)
                    {
                        _strings = data;
                    }
                }
            }
            
            _currentLanguage = languageCode;
        }
    }

    private void FlattenJson(JObject obj, string prefix)
    {
        foreach (var property in obj.Properties())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}_{property.Name}";
            
            if (property.Value is JObject nestedObj)
            {
                FlattenJson(nestedObj, key);
            }
            else if (property.Value is JValue jValue)
            {
                _strings[key] = jValue.Value?.ToString() ?? "";
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