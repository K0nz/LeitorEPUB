# Leitor EPUB

A free and open source (FOSS) EPUB reader with text-to-speech capabilities using Windows SAPI voices. Read your digital books while listening to natural voice synthesis.

## Features

- Open EPUB files and read them with text-to-speech
- Voice selection from all installed Windows TTS voices
- Reading speed control from 0.50x to 2.00x
- Chapter index for quick navigation between chapters
- Auto-save and resume reading progress
- Close EPUB without closing the application (Ctrl+W)
- Interface available in 12 languages
- Light, dark and system theme support
- Keyboard shortcuts: Space bar to Play/Pause, Ctrl+W to close EPUB

## Supported Languages

Portuguese, English, Spanish, French, German, Italian, Arabic, Hindi, Chinese, Japanese, Korean, Russian

## Download

Download the latest version from the [Releases page](https://github.com/K0nz/LeitorEPUB/releases).

Requirements:
- Windows 10 or 11 (64-bit)
- .NET 10 Runtime (download at https://dotnet.microsoft.com/download/dotnet/10.0)
- Windows TTS voices (included by default)

## 🎤 More Voices (Natural Voices)

Windows includes basic SAPI voices by default. To unlock more natural voices, and others, we recommend using **[NaturalVoiceSAPIAdapter](https://github.com/gexgd0419/NaturalVoiceSAPIAdapter)**.

This free tool registers the modern Windows Natural Voices (OneCore) into the legacy SAPI system, making them available in Leitor EPUB and any other SAPI-compatible application.

## How to Use

1. Open **File > Open EPUB** and select your `.epub` file
2. Click **Play** to start voice reading
3. Use **Previous** and **Next** buttons to navigate between paragraphs
4. Open the chapter index with the **T** button on the top right
5. Change voice in **Settings > Voice**
6. Adjust reading speed with the **+/-** buttons at the bottom right
7. Change interface language in **Settings > Language**
8. Switch theme in **Settings > Theme**
9. Close the current book with **File > Close EPUB** (Ctrl+W) without exiting the app

## Build from Source

Prerequisites:
- .NET 10 SDK


```
git clone https://github.com/K0nz/LeitorEPUB.git
cd LeitorEPUB
dotnet restore
dotnet build
dotnet run
```

To create a standalone executable:
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## How to Use

1. Open File > Open EPUB and select your .epub file
2. Click Play to start voice reading
3. Use Previous and Next buttons to navigate between paragraphs
4. Open the chapter index with the T button on the top right
5. Change voice in Settings > Voice
6. Adjust reading speed at the bottom right
7. Change interface language in Settings > Language
8. Switch theme in Settings > Theme

## Project Structure

```
LeitorEPUB/
├── App.xaml
├── MainWindow.xaml
├── MainWindow.xaml.cs
├── LeitorEPUB.csproj
├── Models/
│   ├── BookInfo.cs
│   ├── Chapter.cs
│   └── ReadingProgress.cs
├── Services/
│   ├── EpubService.cs
│   ├── SettingsService.cs
│   ├── ThemeService.cs
│   └── TtsService.cs
├── Helpers/
│   ├── FileHelper.cs
│   └── LanguageHelper.cs
└── Resources/
    └── Languages/
        ├── ar.json
        ├── de.json
        ├── en.json
        ├── es.json
        ├── fr.json
        ├── hi.json
        ├── it.json
        ├── jp.json
        ├── ko.json
        ├── pt.json
        ├── ru.json
        └── zh.json
```

## Technologies

- C# / .NET 10
- WPF (Windows Presentation Foundation)
- VersOne.Epub for EPUB parsing
- System.Speech (SAPI) for text-to-speech
- Newtonsoft.Json for JSON serialization

## License

MIT License

## Author

K0nz
