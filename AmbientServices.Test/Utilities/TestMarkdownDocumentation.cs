using AmbientServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace AmbientServices.Test;

/// <summary>
/// A class that holds tests for assembly extension methods.
/// </summary>
[TestClass]
public class TestMarkdownDocumentation
{
    [TestMethod]
    public void Markdown()
    {
        MarkdownDocumentation mdd = new(DotNetDocumentation.Load(typeof(DotNetDocumentation).Assembly));
#if false
        foreach ((string humanReadable, string relativePath, string document) in mdd.EnumerateTypeDocumentation())
        {
            string markdownPath = $"markdown/{relativePath.Replace(">", "_").Replace("<", "_")}";
            EnsureDirectoryExists(Path.GetDirectoryName(markdownPath));
            using FileStream fs = new(markdownPath, FileMode.OpenOrCreate, FileAccess.Write);
            using StreamWriter sw = new(fs);
            sw.Write(document);
            sw.Flush();
        }
#endif
    }

    private static void EnsureDirectoryExists(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!Directory.Exists(path))
        {
            EnsureDirectoryExists(Path.GetDirectoryName(path));
            Directory.CreateDirectory(path);
        }
    }
}
