﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using LeitorEPUB.Services;

namespace LeitorEPUB;

public partial class MainWindow : Window
{
    private readonly EpubService _epub = new();
    private readonly TtsService _tts = new();
    private readonly SettingsService _settings = new();
    private readonly Helpers.LanguageHelper _lang = new();
    private bool _tocOpen;
    private string _theme = "system";

    private static readonly Dictionary<string, string> LanguageToTtsCode = new()
    {
        ["ar"] = "ar", ["de"] = "de", ["en"] = "en", ["es"] = "es",
        ["fr"] = "fr", ["hi"] = "hi", ["it"] = "it", ["jp"] = "ja",
        ["ko"] = "ko", ["pt"] = "pt", ["ru"] = "ru", ["zh"] = "zh"
    };

    public MainWindow()
    {
        InitializeComponent();
        
        _tts.ReadingUpdate += i => Dispatcher.Invoke(() => 
        { 
            _epub.GlobalIndex = i; 
            ShowParagraph(); 
            UpdateStatusText(); 
        });
        
        _tts.ReadingEnded += () => Dispatcher.Invoke(() => 
        { 
            PlayButton.Content = _lang.T("play");
            MessageBox.Show(_lang.T("end_of_book"), _lang.T("end_title")); 
        });
        
        KeyDown += (s, e) => 
        { 
            if (e.Key == Key.Space) 
            { 
                PlayButton_Click(null!, null!); 
                e.Handled = true; 
            } 
        };
        
        this.Closing += (s, e) =>
        {
            _tts.Stop();
            if (_epub.HasContent() && !string.IsNullOrEmpty(_epub.CurrentFile))
            {
                _settings.SaveProgress(_epub.CurrentFile, _epub.GlobalIndex, _epub.GetTotalParagraphs(), _tts.Speed);
            }
        };
        
        _theme = _settings.Preferences["theme"]?.ToString() ?? "system";
        ThemeService.ApplyTheme(_theme);
        
        var langCode = _settings.Preferences["language"]?.ToString() ?? "pt";
        _lang.LoadLanguage(langCode);
        
        var ttsLang = LanguageToTtsCode.ContainsKey(langCode) ? LanguageToTtsCode[langCode] : "pt";
        _tts.SelectVoiceForLanguage(ttsLang);
        var voice = _settings.Preferences["voice"]?.ToString();
        if (!string.IsNullOrEmpty(voice)) _tts.SetVoiceByDescription(voice);
        
        // Construir menu de voz dinâmico
        MenuVoice.SubmenuOpened += (s, e) => BuildVoiceMenu();
        
        Loaded += (s, e) =>
        {
            BuildVoiceMenu();
            ThemeService.ApplyTitleBarTheme(this);
            RefreshAllUI();
        };
        
        if (_settings.Preferences.ContainsKey("speed"))
        {
            var savedSpeed = Convert.ToDouble(_settings.Preferences["speed"]);
            _tts.SetSpeed(savedSpeed);
            var speeds = new[] { 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00, 2.25, 2.50, 2.75, 3.00 };
            var idx = Array.IndexOf(speeds, savedSpeed);
            if (idx >= 0) SpeedSelector.SelectedIndex = idx;
        }
    }

    private void RefreshAllUI()
    {
        // Menu Arquivo
        MenuFile.Header = _lang.T("file");
        MenuOpen.Header = _lang.T("open_epub");
        MenuDelete.Header = _lang.T("delete_data");
        MenuExit.Header = "Sair"; // não tem no JSON
        
        // Menu Configurações
        MenuSettings.Header = _lang.T("settings");
        MenuLanguage.Header = _lang.T("language");
        MenuVoice.Header = _lang.T("voice");
        MenuTheme.Header = _lang.T("theme");
        ThemeLight.Header = _lang.T("theme_light");
        ThemeDark.Header = _lang.T("theme_dark");
        ThemeSystem.Header = _lang.T("theme_system");
        
        // Menu Sobre
        MenuAbout.Header = _lang.T("about");
        
        // Botões
        PrevButton.Content = _lang.T("previous");
        NextButton.Content = _lang.T("next");
        
        // Play/Pause
        if (_tts.IsPlaying && !_tts.IsPaused)
            PlayButton.Content = _lang.T("pause");
        else
            PlayButton.Content = _lang.T("play");
        
        // Velocidade
        SpeedLabel.Text = _lang.T("speed") + ":";
        
        // Índice
        TocTitle.Text = _lang.T("toc_title");
        
        // Status
        if (_epub.HasContent())
            UpdateStatusText();
        else
            StatusText.Text = _lang.T("ready");
        
        // Atualizar checks dos idiomas
        var currentLang = _settings.Preferences["language"]?.ToString() ?? "pt";
        Lang_pt.IsChecked = (currentLang == "pt");
        Lang_en.IsChecked = (currentLang == "en");
        Lang_es.IsChecked = (currentLang == "es");
        Lang_fr.IsChecked = (currentLang == "fr");
        Lang_de.IsChecked = (currentLang == "de");
        Lang_it.IsChecked = (currentLang == "it");
        Lang_ar.IsChecked = (currentLang == "ar");
        Lang_hi.IsChecked = (currentLang == "hi");
        Lang_zh.IsChecked = (currentLang == "zh");
        Lang_jp.IsChecked = (currentLang == "jp");
        Lang_ko.IsChecked = (currentLang == "ko");
        Lang_ru.IsChecked = (currentLang == "ru");
    }

    private void UpdateStatusText()
    {
        if (_epub.HasContent())
        {
            var ch = _epub.GetCurrentChapter();
            var chapterTitle = ch != null ? ch.Title : "";
            try
            {
                StatusText.Text = string.Format(
                    _lang.T("paragraph_position"),
                    _epub.GlobalIndex + 1,
                    _epub.GetTotalParagraphs(),
                    _epub.GetProgressPercentage(),
                    chapterTitle
                );
            }
            catch
            {
                StatusText.Text = (_epub.GlobalIndex + 1) + "/" + _epub.GetTotalParagraphs();
            }
        }
        else
        {
            StatusText.Text = _lang.T("ready");
        }
    }

    private void BuildVoiceMenu()
    {
        MenuVoice.Items.Clear();
        var voices = _tts.AvailableVoices;
        
        if (voices == null || voices.Count == 0)
        {
            MenuVoice.Items.Add(new MenuItem { Header = _lang.T("no_voice"), IsEnabled = false });
            return;
        }
        
        foreach (var v in voices)
        {
            var displayName = !string.IsNullOrWhiteSpace(v.Description) ? v.Description : v.Name;
            if (string.IsNullOrWhiteSpace(displayName)) continue;
            
            var item = new MenuItem
            {
                Header = displayName,
                IsCheckable = true,
                IsChecked = (_tts.CurrentVoice == v.Description || _tts.CurrentVoice == v.Name),
                Tag = v
            };
            item.Click += VoiceMenuItem_Click;
            MenuVoice.Items.Add(item);
        }
    }

    private void VoiceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var menuItem = (MenuItem)sender;
        var voiceInfo = (VoiceInfo)menuItem.Tag;
        _tts.SetVoice(voiceInfo);
        _settings.Preferences["voice"] = voiceInfo.Description ?? voiceInfo.Name;
        _settings.SavePreferences();
        
        foreach (MenuItem item in MenuVoice.Items)
        {
            if (item.Tag is VoiceInfo v)
            {
                item.IsChecked = (_tts.CurrentVoice == v.Description || _tts.CurrentVoice == v.Name);
            }
        }
    }

    private void OpenEpub_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "EPUB|*.epub" };
        if (d.ShowDialog() == true)
        {
            _tts.Stop();
            PlayButton.Content = _lang.T("play");
            
            if (!_epub.LoadBook(d.FileName))
            {
                MessageBox.Show(_lang.T("no_content"), _lang.T("error"));
                return;
            }
            
            BookTitle.Text = Path.GetFileName(d.FileName);
            
            var p = _settings.LoadProgress(d.FileName);
            if (p != null && p.GlobalIndex >= 0 && p.GlobalIndex < _epub.GetTotalParagraphs())
            {
                _epub.GlobalIndex = p.GlobalIndex;
                if (p.Speed > 0)
                {
                    _tts.SetSpeed(p.Speed);
                    var speeds = new[] { 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00, 2.25, 2.50, 2.75, 3.00 };
                    var idx = Array.IndexOf(speeds, p.Speed);
                    if (idx >= 0) SpeedSelector.SelectedIndex = idx;
                }
            }
            else
            {
                _epub.GlobalIndex = 0;
            }
            
            ShowParagraph();
            UpdateStatusText();
        }
    }

    private void ShowParagraph()
    {
        if (!_epub.HasContent()) return;
        var ch = _epub.GetCurrentChapter();
        if (ch == null) return;
        
        Title = _lang.T("title") + " - " + ch.Title;
        TextArea.Document.Blocks.Clear();
        var doc = new System.Windows.Documents.FlowDocument();
        
        int currentParaInChapter = _epub.GlobalIndex - ch.GlobalStart;
        
        for (int i = 0; i < ch.Paragraphs.Count; i++)
        {
            var p = new System.Windows.Documents.Paragraph();
            p.Margin = new Thickness(0, 2, 0, 2);
            var prefix = i == currentParaInChapter ? "► " : "  ";
            p.Inlines.Add(new System.Windows.Documents.Run(prefix + ch.Paragraphs[i]));
            
            if (i == currentParaInChapter)
                p.Background = ThemeService.IsDarkMode 
                    ? new SolidColorBrush(Color.FromRgb(70, 70, 0))
                    : new SolidColorBrush(Color.FromRgb(255, 255, 0));
            
            doc.Blocks.Add(p);
        }
        
        TextArea.Document = doc;
        
        Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                if (currentParaInChapter >= 0 && currentParaInChapter < TextArea.Document.Blocks.Count)
                {
                    var block = TextArea.Document.Blocks.ElementAt(currentParaInChapter);
                    block?.BringIntoView();
                }
            }
            catch { }
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void TocToggle_Click(object sender, RoutedEventArgs e)
    {
        _tocOpen = !_tocOpen;
        ChapterPanel.Visibility = _tocOpen ? Visibility.Visible : Visibility.Collapsed;
        TocButton.Content = _tocOpen ? ">" : "T";
        if (_tocOpen)
        {
            ChapterList.Items.Clear();
            for (int i = 0; i < _epub.Chapters.Count; i++)
                ChapterList.Items.Add((i+1) + ". " + _epub.Chapters[i].Title);
        }
    }

    private void ChapterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var i = ChapterList.SelectedIndex;
        if (i >= 0 && i < _epub.Chapters.Count)
        {
            _epub.GlobalIndex = _epub.Chapters[i].GlobalStart;
            ShowParagraph();
            UpdateStatusText();
        }
    }

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent()) return;
        if (_tts.IsPlaying && !_tts.IsPaused)
        {
            _tts.Pause();
            PlayButton.Content = _lang.T("play");
        }
        else
        {
            if (!_tts.IsPlaying)
                _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex);
            else
                _tts.Resume();
            PlayButton.Content = _lang.T("pause");
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent() || _epub.GlobalIndex <= 0) return;
        _epub.GlobalIndex--;
        ShowParagraph();
        UpdateStatusText();
        if (_tts.IsPlaying) { _tts.Stop(); _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex); PlayButton.Content = _lang.T("pause"); }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent() || _epub.GlobalIndex >= _epub.GetTotalParagraphs() - 1) return;
        _epub.GlobalIndex++;
        ShowParagraph();
        UpdateStatusText();
        if (_tts.IsPlaying) { _tts.Stop(); _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex); PlayButton.Content = _lang.T("pause"); }
    }

    private void SpeedSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var speeds = new[] { 0.25, 0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00, 2.25, 2.50, 2.75, 3.00 };
        if (SpeedSelector.SelectedIndex >= 0 && SpeedSelector.SelectedIndex < speeds.Length)
        {
            var speed = speeds[SpeedSelector.SelectedIndex];
            _tts.SetSpeed(speed);
            _settings.Preferences["speed"] = speed;
            _settings.SavePreferences();
        }
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        var lang = (sender as MenuItem)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(lang))
        {
            _lang.LoadLanguage(lang);
            _settings.Preferences["language"] = lang;
            _settings.SavePreferences();
            
            var ttsLang = LanguageToTtsCode.ContainsKey(lang) ? LanguageToTtsCode[lang] : "en";
            _tts.SelectVoiceForLanguage(ttsLang);
            BuildVoiceMenu();
            
            RefreshAllUI();
        }
    }

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        var t = (sender as MenuItem)?.Tag?.ToString();
        if (t != null)
        {
            _theme = t;
            _settings.Preferences["theme"] = t;
            _settings.SavePreferences();
            ThemeService.ApplyTheme(t);
            ThemeService.ApplyTitleBarTheme(this);
            ShowParagraph();
        }
    }

    private void DeleteData_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(_lang.T("delete_data_msg"), _lang.T("delete_data_title"), MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            _settings.DeleteAllData();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(_lang.T("about_text"), _lang.T("about_title"));
    }
}