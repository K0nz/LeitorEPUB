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
    private double _currentSpeed = 1.0;
    private static readonly double[] Speeds = { 0.50, 0.75, 1.00, 1.25, 1.50, 1.75, 2.00 };

    private static readonly Dictionary<string, string> LanguageToTtsCode = new()
    {
        ["ar"] = "ar", ["de"] = "de", ["en"] = "en", ["es"] = "es",
        ["fr"] = "fr", ["hi"] = "hi", ["it"] = "it", ["jp"] = "ja",
        ["ko"] = "ko", ["pt"] = "pt", ["ru"] = "ru", ["zh"] = "zh"
    };

    public MainWindow()
    {
        InitializeComponent();
        
        // Eventos do TTS
        _tts.ReadingUpdate += i => Dispatcher.Invoke(() => 
        { 
            _epub.GlobalIndex = i; 
            ShowParagraph(); 
            UpdateStatusText(); 
        });
        
        _tts.ReadingEnded += () => Dispatcher.Invoke(() => 
        { 
            PlayButton.Content = _lang.T("toolbar_play");
            MessageBox.Show(_lang.T("end_of_book"), _lang.T("end_title")); 
        });
        
        // Atalho: Espaço para Play/Pause
        KeyDown += (s, e) => 
        { 
            if (e.Key == Key.Space) 
            { 
                PlayButton_Click(null!, null!); 
                e.Handled = true; 
            } 
        };
        
        // Atalho: Ctrl+W para Fechar EPUB
        KeyDown += (s, e) =>
        {
            if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control)
            {
                CloseEpub_Click(null!, null!);
                e.Handled = true;
            }
        };
        
        // Salvar progresso ao fechar
        this.Closing += (s, e) =>
        {
            _tts.Stop();
            if (_epub.HasContent() && !string.IsNullOrEmpty(_epub.CurrentFile))
            {
                _settings.SaveProgress(_epub.CurrentFile, _epub.GlobalIndex, _epub.GetTotalParagraphs(), _tts.Speed);
            }
        };
        
        // Carregar configurações
        _theme = _settings.Preferences["theme"]?.ToString() ?? "system";
        ThemeService.ApplyTheme(_theme);
        
        var langCode = _settings.Preferences["language"]?.ToString() ?? "pt";
        _lang.LoadLanguage(langCode);
        
        var ttsLang = LanguageToTtsCode.ContainsKey(langCode) ? LanguageToTtsCode[langCode] : "pt";
        _tts.SelectVoiceForLanguage(ttsLang);
        
        var savedVoice = _settings.Preferences["voice"]?.ToString();
        if (!string.IsNullOrEmpty(savedVoice))
        {
            _tts.SetVoiceByDescription(savedVoice);
        }
        
        Loaded += (s, e) =>
        {
            ThemeService.ApplyTitleBarTheme(this);
            InitSpeed();
            RefreshAllUI();
            UpdateControlsState();
        };
    }

    // ============================================================
    // VELOCIDADE
    // ============================================================

    private void InitSpeed()
    {
        double savedSpeed = 1.0;
        if (_settings.Preferences.ContainsKey("speed"))
        {
            var speedObj = _settings.Preferences["speed"];
            if (speedObj != null)
            {
                try { savedSpeed = Convert.ToDouble(speedObj); }
                catch { savedSpeed = 1.0; }
            }
        }
        _currentSpeed = savedSpeed;
        _tts.SetSpeed(_currentSpeed);
        UpdateSpeedDisplay();
    }

    private void UpdateSpeedDisplay()
    {
        SpeedValue.Text = _currentSpeed.ToString("0.00x");
    }

    private void SpeedMinus_Click(object sender, RoutedEventArgs e)
    {
        var idx = Array.IndexOf(Speeds, _currentSpeed);
        if (idx > 0)
        {
            idx--;
            _currentSpeed = Speeds[idx];
            ApplySpeedChange();
        }
    }

    private void SpeedPlus_Click(object sender, RoutedEventArgs e)
    {
        var idx = Array.IndexOf(Speeds, _currentSpeed);
        if (idx < Speeds.Length - 1)
        {
            idx++;
            _currentSpeed = Speeds[idx];
            ApplySpeedChange();
        }
    }

    private void ApplySpeedChange()
    {
        _tts.SetSpeed(_currentSpeed);
        _settings.Preferences["speed"] = _currentSpeed;
        _settings.SavePreferences();
        UpdateSpeedDisplay();
        
        if (_tts.IsPlaying && !_tts.IsPaused)
        {
            _tts.Stop();
            _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex);
            PlayButton.Content = _lang.T("toolbar_pause");
        }
    }

    // ============================================================
    // MÉTODOS DE INTERFACE (UI)
    // ============================================================

    private void RefreshAllUI()
    {
        // Menu Arquivo
        MenuFile.Header = _lang.T("menu_file");
        MenuOpen.Header = _lang.T("menu_open_epub");
        MenuCloseEpub.Header = _lang.T("menu_close_epub");
        MenuDelete.Header = _lang.T("menu_delete_data") != "menu_delete_data" ? _lang.T("menu_delete_data") : "Apagar Dados";
        MenuExit.Header = _lang.T("menu_exit");

        // Menu Configurações
        MenuSettings.Header = _lang.T("menu_settings") != "menu_settings" ? _lang.T("menu_settings") : "Configurações";
        MenuLanguage.Header = _lang.T("settings_language");
        MenuVoice.Header = _lang.T("settings_voice") != "settings_voice" ? _lang.T("settings_voice") : "Voz";
        MenuTheme.Header = _lang.T("settings_theme");
        
        // Submenu Tema
        ThemeLight.Header = _lang.T("themes_light") != "themes_light" ? _lang.T("themes_light") : "Claro";
        ThemeDark.Header = _lang.T("themes_dark") != "themes_dark" ? _lang.T("themes_dark") : "Escuro";
        ThemeSystem.Header = _lang.T("themes_system") != "themes_system" ? _lang.T("themes_system") : "Sistema";

        // Menu Sobre
        MenuAbout.Header = _lang.T("menu_about") != "menu_about" ? _lang.T("menu_about") : "Sobre";

        // Botões
        PrevButton.Content = _lang.T("toolbar_previous");
        NextButton.Content = _lang.T("toolbar_next");
        TocButton.ToolTip = _lang.T("toolbar_chapters");

        // Play/Pause
        if (_tts.IsPlaying && !_tts.IsPaused)
            PlayButton.Content = _lang.T("toolbar_pause");
        else
            PlayButton.Content = _lang.T("toolbar_play");

        // Velocidade
        SpeedLabel.Text = _lang.T("settings_speed") + ":";
        UpdateSpeedDisplay();

        // Índice
        TocTitle.Text = _lang.T("toolbar_chapters");

        // Status
        if (_epub.HasContent())
            UpdateStatusText();
        else
            StatusText.Text = _lang.T("messages_no_book_open") != "messages_no_book_open"
                ? _lang.T("messages_no_book_open")
                : "Pronto";

        // Título do livro na barra superior
        if (!_epub.HasContent())
            BookTitle.Text = _lang.T("messages_no_book_open") != "messages_no_book_open"
                ? _lang.T("messages_no_book_open")
                : "Nenhum livro aberto";

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

    private void UpdateControlsState()
    {
        bool hasBook = _epub.HasContent();
        
        MenuCloseEpub.IsEnabled = hasBook;
        PrevButton.IsEnabled = hasBook;
        NextButton.IsEnabled = hasBook;
        PlayButton.IsEnabled = hasBook;
        TocButton.IsEnabled = hasBook;
        SpeedMinus.IsEnabled = hasBook;
        SpeedPlus.IsEnabled = hasBook;
    }

    private void UpdateStatusText()
    {
        if (_epub.HasContent())
        {
            var ch = _epub.GetCurrentChapter();
            var chapterTitle = ch != null ? ch.Title : "";
            try
            {
                var format = _lang.T("paragraph_position");
                if (format == "paragraph_position")
                {
                    StatusText.Text = (_epub.GlobalIndex + 1) + "/" + _epub.GetTotalParagraphs() + " - " + chapterTitle;
                }
                else
                {
                    StatusText.Text = string.Format(format,
                        _epub.GlobalIndex + 1,
                        _epub.GetTotalParagraphs(),
                        _epub.GetProgressPercentage().ToString("F0") + "%",
                        chapterTitle
                    );
                }
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

    // ============================================================
    // MENU ARQUIVO
    // ============================================================

    private void OpenEpub_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Filter = "EPUB|*.epub" };
        if (d.ShowDialog() == true)
        {
            _tts.Stop();
            PlayButton.Content = _lang.T("toolbar_play");
            
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
                    _currentSpeed = p.Speed;
                    _tts.SetSpeed(_currentSpeed);
                    UpdateSpeedDisplay();
                }
            }
            else
            {
                _epub.GlobalIndex = 0;
            }
            
            ShowParagraph();
            UpdateStatusText();
            UpdateControlsState();
        }
    }

    private void CloseEpub_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent()) return;

        _tts.Stop();
        PlayButton.Content = _lang.T("toolbar_play");

        if (!string.IsNullOrEmpty(_epub.CurrentFile))
        {
            _settings.SaveProgress(_epub.CurrentFile, _epub.GlobalIndex, _epub.GetTotalParagraphs(), _tts.Speed);
        }

        _epub.CloseBook();

        BookTitle.Text = _lang.T("messages_no_book_open") != "messages_no_book_open" 
            ? _lang.T("messages_no_book_open") 
            : "Nenhum livro aberto";
        Title = "LeitorEPUB";
        TextArea.Document.Blocks.Clear();
        StatusText.Text = _lang.T("messages_no_book_open") != "messages_no_book_open" 
            ? _lang.T("messages_no_book_open") 
            : "Nenhum livro aberto. Use Arquivo > Abrir EPUB para começar.";

        _tocOpen = false;
        ChapterPanel.Visibility = Visibility.Collapsed;
        TocButton.Content = "T";
        ChapterList.Items.Clear();

        UpdateControlsState();
    }

    private void DeleteData_Click(object sender, RoutedEventArgs e)
    {
        var title = _lang.T("delete_data_title") != "delete_data_title" ? _lang.T("delete_data_title") : "Apagar Dados";
        var msg = _lang.T("delete_data_msg") != "delete_data_msg" ? _lang.T("delete_data_msg") : "Tem certeza que deseja apagar todos os dados?";
        
        if (MessageBox.Show(msg, title, MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            _settings.DeleteAllData();
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    // ============================================================
    // MENU CONFIGURAÇÕES > VOZ
    // ============================================================

    private void MenuVoice_Click(object sender, RoutedEventArgs e)
    {
        ShowVoiceDialog();
    }

    private void ShowVoiceDialog()
    {
        var allVoices = new SpeechSynthesizer().GetInstalledVoices()
            .Select(v => v.VoiceInfo)
            .Where(v => v != null)
            .ToList();
        
        bool wasPlaying = _tts.IsPlaying;
        bool wasPaused = _tts.IsPaused;
        int currentIndex = _epub.GlobalIndex;

        var currentVoice = allVoices.FirstOrDefault(v =>
            (v.Description ?? v.Name) == _tts.CurrentVoice);

        var dialog = new Views.VoiceSelectionDialog(allVoices, currentVoice)
        {
            Owner = this
        };

        dialog.Title = _lang.T("dialogs_select_voice") != "dialogs_select_voice" 
            ? _lang.T("dialogs_select_voice") 
            : "Selecionar Voz";
        dialog.DialogTitle.Text = _lang.T("dialogs_choose_voice") != "dialogs_choose_voice" 
            ? _lang.T("dialogs_choose_voice") 
            : "Escolha uma voz para leitura:";
        
        var cancelText = _lang.T("cancel") != "cancel" ? _lang.T("cancel") : "Cancelar";
        dialog.CancelButton.Content = cancelText;

        if (dialog.ShowDialog() == true && dialog.SelectedVoice != null)
        {
            _tts.ChangeVoiceSafe(dialog.SelectedVoice);
            _settings.Preferences["voice"] = dialog.SelectedVoice.Description ?? dialog.SelectedVoice.Name;
            _settings.SavePreferences();

            if (wasPlaying && !wasPaused)
            {
                _epub.GlobalIndex = currentIndex;
                _tts.StartReading(_epub.AllParagraphs, currentIndex);
                PlayButton.Content = _lang.T("toolbar_pause");
            }
            else if (wasPlaying && wasPaused)
            {
                _epub.GlobalIndex = currentIndex;
                PlayButton.Content = _lang.T("toolbar_play");
            }
        }
    }

    // ============================================================
    // MENU CONFIGURAÇÕES > IDIOMA
    // ============================================================

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        var lang = (sender as MenuItem)?.Tag?.ToString();
        if (!string.IsNullOrEmpty(lang))
        {
            _lang.LoadLanguage(lang);
            _settings.Preferences["language"] = lang;
            _settings.SavePreferences();
            
            RefreshAllUI();
            UpdateControlsState();
        }
    }

    // ============================================================
    // MENU CONFIGURAÇÕES > TEMA
    // ============================================================

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

    // ============================================================
    // ÍNDICE DE CAPÍTULOS
    // ============================================================

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

    // ============================================================
    // CONTROLES DE LEITURA
    // ============================================================

    private void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent()) return;
        
        if (_tts.IsPlaying && !_tts.IsPaused)
        {
            _tts.Pause();
            PlayButton.Content = _lang.T("toolbar_play");
        }
        else if (_tts.IsPlaying && _tts.IsPaused)
        {
            _tts.Resume();
            PlayButton.Content = _lang.T("toolbar_pause");
        }
        else
        {
            _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex);
            PlayButton.Content = _lang.T("toolbar_pause");
        }
    }

    private void PrevButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent() || _epub.GlobalIndex <= 0) return;
        _epub.GlobalIndex--;
        ShowParagraph();
        UpdateStatusText();
        
        if (_tts.IsPlaying && !_tts.IsPaused)
        { 
            _tts.Stop(); 
            _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex); 
            PlayButton.Content = _lang.T("toolbar_pause"); 
        }
        else if (_tts.IsPlaying && _tts.IsPaused)
        {
            PlayButton.Content = _lang.T("toolbar_play");
        }
    }

    private void NextButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_epub.HasContent() || _epub.GlobalIndex >= _epub.GetTotalParagraphs() - 1) return;
        _epub.GlobalIndex++;
        ShowParagraph();
        UpdateStatusText();
        
        if (_tts.IsPlaying && !_tts.IsPaused)
        { 
            _tts.Stop(); 
            _tts.StartReading(_epub.AllParagraphs, _epub.GlobalIndex); 
            PlayButton.Content = _lang.T("toolbar_pause"); 
        }
        else if (_tts.IsPlaying && _tts.IsPaused)
        {
            PlayButton.Content = _lang.T("toolbar_play");
        }
    }

    // ============================================================
    // MENU SOBRE
    // ============================================================

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var title = _lang.T("menu_about") != "menu_about" ? _lang.T("menu_about") : "Sobre";
        var text = _lang.T("messages_about_text");
        if (text == "messages_about_text")
        {
            text = "Leitor EPUB\n\nVersão 1.1.0\n\nMIT License";
        }
        MessageBox.Show(text, title);
    }
}