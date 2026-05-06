﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;

namespace LeitorEPUB.Services;

public class TtsService
{
    private SpeechSynthesizer _synthesizer;
    private CancellationTokenSource? _cts;
    private Task? _readingTask;
    private volatile bool _stopRequested;
    private readonly object _lock = new();

    public List<VoiceInfo> AvailableVoices { get; private set; } = new();
    public string CurrentVoice { get; private set; } = "";
    public double Speed { get; private set; } = 1.0;
    public string CurrentLanguage { get; private set; } = "pt";
    public bool IsPlaying { get; private set; }
    public bool IsPaused { get; private set; }

    public event Action<int>? ReadingUpdate;
    public event Action? ReadingEnded;

    private static readonly Dictionary<string, string> LanguageCodes = new()
    {
        ["pt"] = "416", ["en"] = "409", ["es"] = "C0A",
        ["zh"] = "804", ["hi"] = "439", ["fr"] = "40C",
        ["ar"] = "401", ["ru"] = "419", ["de"] = "407",
        ["ja"] = "411", ["ko"] = "412", ["it"] = "410"
    };

    public TtsService()
    {
        _synthesizer = new SpeechSynthesizer();
        _synthesizer.SetOutputToDefaultAudioDevice();
        
        try
        {
            var allVoices = _synthesizer.GetInstalledVoices();
            AvailableVoices = allVoices
                .Select(v => v.VoiceInfo)
                .Where(v => v != null)
                .ToList();
            
            if (AvailableVoices.Count > 0)
            {
                _synthesizer.SelectVoice(AvailableVoices[0].Name);
                CurrentVoice = AvailableVoices[0].Description ?? AvailableVoices[0].Name ?? "Desconhecida";
            }
        }
        catch
        {
            AvailableVoices = new List<VoiceInfo>();
        }
    }

    public void ChangeVoiceSafe(VoiceInfo voiceInfo)
    {
        lock (_lock)
        {
            try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
            
            var timeout = DateTime.Now.AddSeconds(3);
            while (_synthesizer.State != SynthesizerState.Ready && DateTime.Now < timeout)
                Thread.Sleep(50);

            SetVoiceInternal(voiceInfo);
            CurrentVoice = voiceInfo.Description ?? voiceInfo.Name ?? "Desconhecida";
        }
    }

    public string GetCurrentVoiceLanguageCode()
    {
        var voice = AvailableVoices.FirstOrDefault(v => 
            (v.Description ?? v.Name) == CurrentVoice);
        
        if (voice != null)
        {
            var lcid = voice.Culture.LCID.ToString("X");
            return LanguageCodes.FirstOrDefault(kv => kv.Value == lcid).Key ?? "en";
        }
        return "en";
    }

    public string GetCurrentVoiceLanguageName()
    {
        var code = GetCurrentVoiceLanguageCode();
        return code switch
        {
            "pt" => "Português", "en" => "English", "es" => "Español",
            "fr" => "Français", "de" => "Deutsch", "it" => "Italiano",
            "zh" => "中文", "ja" => "日本語", "ko" => "한국어",
            "ar" => "العربية", "hi" => "हिन्दी", "ru" => "Русский",
            _ => code
        };
    }

    public string? SelectVoiceForLanguage(string languageCode)
    {
        CurrentLanguage = languageCode;
        if (!AvailableVoices.Any()) return null;

        var voice = FindBestVoice(languageCode);
        if (voice != null)
        {
            SetVoice(voice);
            return voice.Description ?? voice.Name;
        }

        if (AvailableVoices.Count > 0)
        {
            SetVoice(AvailableVoices[0]);
            return AvailableVoices[0].Description ?? AvailableVoices[0].Name;
        }
        return null;
    }

    private VoiceInfo? FindBestVoice(string languageCode)
    {
        if (!LanguageCodes.ContainsKey(languageCode)) return null;
        var targetCulture = LanguageCodes[languageCode];

        var matchingVoices = AvailableVoices
            .Where(v => v.Culture.LCID.ToString("X") == targetCulture).ToList();

        if (!matchingVoices.Any())
            matchingVoices = AvailableVoices.Where(v => v.Culture.Name.StartsWith("en")).ToList();
        if (!matchingVoices.Any()) return null;

        var female = matchingVoices.FirstOrDefault(v =>
            v.Gender == VoiceGender.Female || 
            v.Description.Contains("Maria") ||
            v.Name.Contains("Zira") || 
            v.Description.Contains("Let") ||
            v.Name.Contains("Let"));

        return female ?? matchingVoices.FirstOrDefault();
    }

    public void SetVoice(VoiceInfo voiceInfo)
    {
        SetVoiceInternal(voiceInfo);
        CurrentVoice = voiceInfo.Description ?? voiceInfo.Name ?? "Desconhecida";
    }

    private void SetVoiceInternal(VoiceInfo voiceInfo)
    {
        try
        {
            if (!string.IsNullOrEmpty(voiceInfo.Name))
            {
                try
                {
                    _synthesizer.SelectVoice(voiceInfo.Name);
                }
                catch
                {
                    var match = AvailableVoices.FirstOrDefault(v => 
                        v.Description == voiceInfo.Description && !string.IsNullOrEmpty(v.Name));
                    if (match != null)
                        _synthesizer.SelectVoice(match.Name);
                    else
                        _synthesizer.SelectVoiceByHints(voiceInfo.Gender, voiceInfo.Age, 0, voiceInfo.Culture);
                }
            }
            else if (!string.IsNullOrEmpty(voiceInfo.Description))
            {
                var match = AvailableVoices.FirstOrDefault(v => 
                    v.Description == voiceInfo.Description && !string.IsNullOrEmpty(v.Name));
                if (match != null)
                    _synthesizer.SelectVoice(match.Name);
                else
                    _synthesizer.SelectVoiceByHints(voiceInfo.Gender, voiceInfo.Age, 0, voiceInfo.Culture);
            }
        }
        catch { }
    }

    public bool SetVoiceByDescription(string description)
    {
        var voice = AvailableVoices.FirstOrDefault(v => v.Description == description);
        if (voice != null) 
        { 
            SetVoice(voice); 
            return true; 
        }
        return false;
    }
    public void SetSpeed(double speed)
    {
        Speed = Math.Max(0.25, Math.Min(3.0, speed));
        lock (_lock)
        {
            try { _synthesizer.Rate = (int)((Speed - 1) * 10); }
            catch { }
        }
    }

    public void StartReading(List<string> paragraphs, int startIndex)
    {
        StopInternal();
        
        lock (_lock)
        {
            var timeout = DateTime.Now.AddSeconds(2);
            while (_synthesizer.State != SynthesizerState.Ready && DateTime.Now < timeout)
                Thread.Sleep(50);
        }
        
        _stopRequested = false;
        IsPlaying = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();
        _readingTask = Task.Run(() => ReadLoop(paragraphs, startIndex, _cts.Token));
    }

    private async Task ReadLoop(List<string> paragraphs, int currentIndex, CancellationToken ct)
    {
        try
        {
            while (currentIndex < paragraphs.Count && !ct.IsCancellationRequested && !_stopRequested)
            {
                // Aguarda despausar
                while (IsPaused && !ct.IsCancellationRequested && !_stopRequested)
                    await Task.Delay(100, ct);
                
                if (ct.IsCancellationRequested || _stopRequested) break;

                var index = currentIndex;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ReadingUpdate?.Invoke(index));

                var paragraph = paragraphs[currentIndex];
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    bool completed = false;
                    
                    while (!completed && !ct.IsCancellationRequested && !_stopRequested)
                    {
                        var tcs = new TaskCompletionSource<bool>();
                        EventHandler<SpeakCompletedEventArgs>? handler = null;
                        handler = (s, e) =>
                        {
                            _synthesizer.SpeakCompleted -= handler;
                            tcs.TrySetResult(true);
                        };
                        
                        lock (_lock)
                        {
                            _synthesizer.Rate = (int)((Speed - 1) * 10);
                            _synthesizer.SpeakCompleted += handler;
                            _synthesizer.SpeakAsync(paragraph);
                        }
                        
                        // Monitora pausa e stop enquanto fala
                        while (!tcs.Task.IsCompleted && !ct.IsCancellationRequested && !_stopRequested)
                        {
                            if (IsPaused)
                            {
                                lock (_lock) { try { _synthesizer.Pause(); } catch { } }
                                
                                while (IsPaused && !ct.IsCancellationRequested && !_stopRequested)
                                    await Task.Delay(100, ct);
                                
                                if (!_stopRequested && !ct.IsCancellationRequested)
                                {
                                    // Velocidade pode ter mudado durante a pausa
                                    lock (_lock)
                                    {
                                        try
                                        {
                                            _synthesizer.Rate = (int)((Speed - 1) * 10);
                                            _synthesizer.Resume();
                                        }
                                        catch { }
                                    }
                                }
                            }
                            await Task.Delay(100, ct);
                        }
                        
                        if (_stopRequested || ct.IsCancellationRequested)
                        {
                            lock (_lock)
                            {
                                try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
                                _synthesizer.SpeakCompleted -= handler;
                            }
                            break;
                        }
                        
                        try { await tcs.Task; } catch { }
                        completed = true;
                    }
                    
                    if (_stopRequested || ct.IsCancellationRequested) break;
                }

                currentIndex++;
                if (currentIndex < paragraphs.Count && !ct.IsCancellationRequested && !_stopRequested)
                    await Task.Delay(200, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (currentIndex >= paragraphs.Count && !_stopRequested)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ReadingEnded?.Invoke());
            }
            IsPlaying = false;
            IsPaused = false;
            _stopRequested = false;
        }
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
    }

    private void StopInternal()
    {
        _stopRequested = true;
        IsPlaying = false;
        IsPaused = false;
        _cts?.Cancel();
        lock (_lock)
        {
            try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
        }
    }

    public void Stop()
    {
        StopInternal();
        try { _readingTask?.Wait(2000); } catch { }
    }

    public void Cleanup()
    {
        Stop();
        _synthesizer?.Dispose();
    }
}