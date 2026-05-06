using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace LeitorEPUB.Views;

public partial class VoiceSelectionDialog : Window
{
    private List<VoiceInfo> _allVoices;
    public VoiceInfo? SelectedVoice { get; private set; }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    public VoiceSelectionDialog(List<VoiceInfo> voices, VoiceInfo? currentVoice)
    {
        InitializeComponent();

        // Corrige a barra de título branca (Bug #2)
        this.SourceInitialized += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int useDarkMode = 1;
            // DWMWA_USE_IMMERSIVE_DARK_MODE = 20 (Windows 10 20H1+)
            DwmSetWindowAttribute(hwnd, 20, ref useDarkMode, sizeof(int));
        };

        _allVoices = voices;

        // Popula a lista com todas as vozes
        RefreshList(_allVoices);

        // Seleciona a voz atual, se existir
        if (currentVoice != null)
        {
            for (int i = 0; i < VoiceListBox.Items.Count; i++)
            {
                var v = (VoiceInfo)VoiceListBox.Items[i];
                if ((v.Description != null && v.Description == currentVoice.Description) ||
                    (v.Name != null && v.Name == currentVoice.Name))
                {
                    VoiceListBox.SelectedIndex = i;
                    VoiceListBox.ScrollIntoView(VoiceListBox.Items[i]);
                    break;
                }
            }
        }

        // Se nada selecionado, seleciona a primeira voz
        if (VoiceListBox.SelectedIndex < 0 && VoiceListBox.Items.Count > 0)
        {
            VoiceListBox.SelectedIndex = 0;
        }

        // Foco no campo de filtro
        Loaded += (s, e) => FilterTextBox.Focus();
    }

    private void RefreshList(List<VoiceInfo> voices)
    {
        // Ordena por descrição para facilitar a busca
        var sorted = voices
            .OrderBy(v => (v.Description ?? v.Name ?? "").ToLower())
            .ToList();

        VoiceListBox.ItemsSource = null;
        VoiceListBox.ItemsSource = sorted;
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = FilterTextBox.Text.ToLower().Trim();

        if (string.IsNullOrEmpty(filter))
        {
            RefreshList(_allVoices);
        }
        else
        {
            var filtered = _allVoices.Where(v =>
            {
                var desc = (v.Description ?? "").ToLower();
                var name = (v.Name ?? "").ToLower();
                var culture = v.Culture?.EnglishName?.ToLower() ?? "";
                var lang = v.Culture?.NativeName?.ToLower() ?? "";
                
                return desc.Contains(filter) || 
                       name.Contains(filter) || 
                       culture.Contains(filter) ||
                       lang.Contains(filter);
            }).ToList();

            RefreshList(filtered);
        }
    }

    private void VoiceListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VoiceListBox.SelectedItem is VoiceInfo voice)
        {
            SelectedVoice = voice;
            DialogResult = true;
            Close();
        }
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        if (VoiceListBox.SelectedItem is VoiceInfo voice)
        {
            SelectedVoice = voice;
            DialogResult = true;
            Close();
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}