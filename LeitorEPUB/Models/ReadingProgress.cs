using System;
namespace LeitorEPUB.Models;
public class ReadingProgress
{
    public string File { get; set; } = "";
    public int GlobalIndex { get; set; }
    public int TotalParagraphs { get; set; }
    public double Speed { get; set; }
    public DateTime LastAccess { get; set; }
}
