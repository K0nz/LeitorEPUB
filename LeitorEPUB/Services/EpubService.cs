using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using VersOne.Epub;
using LeitorEPUB.Models;
namespace LeitorEPUB.Services;
public class EpubService
{
    public string CurrentFile { get; private set; }
    public List<Chapter> Chapters { get; private set; } = new();
    public List<string> AllParagraphs { get; private set; } = new();
    public int GlobalIndex { get; set; }
    public bool LoadBook(string filePath)
    {
        try
        {
            var book = EpubReader.ReadBook(filePath);
            CurrentFile = filePath;
            Chapters.Clear();
            AllParagraphs.Clear();
            foreach (var item in book.ReadingOrder)
            {
                ProcessItem(item);
            }
            return AllParagraphs.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
    }
    private void ProcessItem(EpubLocalTextContentFile item)
    {
        try
        {
            var html = item.Content;
            var title = ExtractTitle(html, Chapters.Count + 1);
            var paragraphs = ExtractParagraphs(html);
            if (paragraphs.Count > 0)
            {
                var chapter = new Chapter
                {
                    Id = item.Key,
                    Title = title,
                    Paragraphs = paragraphs,
                    GlobalStart = AllParagraphs.Count,
                    GlobalEnd = AllParagraphs.Count + paragraphs.Count - 1
                };
                Chapters.Add(chapter);
                AllParagraphs.AddRange(paragraphs);
            }
        }
        catch { }
    }
    private string ExtractTitle(string html, int index)
    {
        foreach (var tag in new[] { "h1", "h2", "h3" })
        {
            var pattern = "<" + tag + "[^>]*>(.*?)</" + tag + ">";
            var match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var title = Regex.Replace(match.Groups[1].Value, "<.*?>", "").Trim();
                if (!string.IsNullOrEmpty(title))
                    return title;
            }
        }
        var titleMatch = Regex.Match(html, "<title>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (titleMatch.Success)
        {
            var title = Regex.Replace(titleMatch.Groups[1].Value, "<.*?>", "").Trim();
            if (!string.IsNullOrEmpty(title))
                return title;
        }
        return "Section " + index;
    }
    private List<string> ExtractParagraphs(string html)
    {
        var paragraphs = new List<string>();
        html = Regex.Replace(html, "<script.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<style.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        html = Regex.Replace(html, "<nav.*?</nav>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        var matches = Regex.Matches(html, "<(p|div|h[1-6])[^>]*>(.*?)</\\1>",
            RegexOptions.Singleline | RegexOptions.IgnoreCase);
        foreach (Match match in matches)
        {
            var text = Regex.Replace(match.Groups[2].Value, "<.*?>", "").Trim();
            if (text.Length > 15)
            {
                paragraphs.Add(text);
            }
        }
        if (paragraphs.Count == 0)
        {
            var text = Regex.Replace(html, "<.*?>", " ").Trim();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 15 && !l.StartsWith("*") && !l.StartsWith("["));
            paragraphs.AddRange(lines);
        }
        return paragraphs;
    }
    public Chapter GetCurrentChapter()
    {
        foreach (var chapter in Chapters)
        {
            if (chapter.GlobalStart <= GlobalIndex && GlobalIndex <= chapter.GlobalEnd)
                return chapter;
        }
        return null;
    }
    public string GetCurrentParagraph()
    {
        if (GlobalIndex >= 0 && GlobalIndex < AllParagraphs.Count)
            return AllParagraphs[GlobalIndex];
        return null;
    }
    public string GetChapterText(Chapter chapter)
    {
        var text = "";
        for (int i = 0; i < chapter.Paragraphs.Count; i++)
        {
            if (chapter.GlobalStart + i == GlobalIndex)
                text += "> " + chapter.Paragraphs[i] + "\n\n";
            else
                text += "  " + chapter.Paragraphs[i] + "\n\n";
        }
        return text;
    }
    public double GetProgressPercentage()
    {
        if (AllParagraphs.Count > 0)
            return (GlobalIndex + 1.0) / AllParagraphs.Count * 100;
        return 0;
    }
    public int GetTotalParagraphs() => AllParagraphs.Count;
    public bool HasContent() => AllParagraphs.Count > 0;
}
