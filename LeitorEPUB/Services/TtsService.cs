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
        }
        catch
        {
            AvailableVoices = new List<VoiceInfo>();
        }
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
            
            CurrentVoice = voiceInfo.Description ?? voiceInfo.Name ?? "Desconhecida";
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
        _synthesizer.Rate = (int)((Speed - 1) * 10);
    }

    public void StartReading(List<string> paragraphs, int startIndex)
    {
        Stop();
        IsPlaying = true;
        IsPaused = false;
        _cts = new CancellationTokenSource();
        _readingTask = Task.Run(() => ReadLoop(paragraphs, startIndex, _cts.Token));
    }

    private async Task ReadLoop(List<string> paragraphs, int currentIndex, CancellationToken ct)
    {
        try
        {
            while (currentIndex < paragraphs.Count && !ct.IsCancellationRequested)
            {
                if (IsPaused)
                {
                    _synthesizer.Pause();
                    while (IsPaused && !ct.IsCancellationRequested)
                        await Task.Delay(100, ct);
                    _synthesizer.Resume();
                }

                var index = currentIndex;
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ReadingUpdate?.Invoke(index));

                var paragraph = paragraphs[currentIndex];
                if (!string.IsNullOrWhiteSpace(paragraph))
                {
                    try 
                    { 
                        _synthesizer.Speak(paragraph); 
                    }
                    catch (OperationCanceledException) { break; }
                    catch { }
                }

                currentIndex++;
                await Task.Delay(200, ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (currentIndex >= paragraphs.Count)
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    ReadingEnded?.Invoke());
            IsPlaying = false;
            IsPaused = false;
        }
    }

    public void Pause()
    {
        IsPaused = true;
        try { _synthesizer.Pause(); } catch { }
    }

    public void Resume()
    {
        IsPaused = false;
        try { _synthesizer.Resume(); } catch { }
    }

    public void Stop()
    {
        IsPlaying = false;
        IsPaused = false;
        _cts?.Cancel();
        try { _synthesizer.SpeakAsyncCancelAll(); } catch { }
        try { _readingTask?.Wait(1000); } catch { }
    }

    public void Cleanup()
    {
        Stop();
        _synthesizer?.Dispose();
    }
}