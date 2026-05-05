using System.Collections.Generic;
namespace LeitorEPUB.Models;
public class Chapter
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Paragraphs { get; set; } = new();
    public int GlobalStart { get; set; }
    public int GlobalEnd { get; set; }
}
