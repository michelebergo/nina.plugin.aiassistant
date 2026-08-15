using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NINA.Core.Utility;

namespace NINA.Plugin.AIAssistant.Wiki
{
    /// <summary>
    /// File-based knowledge wiki for the AI assistant: plain markdown pages in a shared
    /// folder, an index as the entry map, grep-style search - no embeddings, no database.
    /// The folder is deliberately outside the plugin so other tools (and the user, with
    /// any editor) can read and extend the same knowledge base.
    /// </summary>
    public static class LlmWikiStore
    {
        private const int MaxReadChars = 12000;
        private const int MaxSearchResults = 8;
        private const int SnippetContextLines = 2;

        /// <summary>Shared wiki root, common to all NINA AI tooling on this machine.</summary>
        public static string WikiRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NINA", "llmwiki");

        /// <summary>
        /// Seeds the wiki with the starter pack shipped alongside the plugin when the
        /// wiki folder does not exist yet. Never overwrites existing user content.
        /// </summary>
        public static void EnsureSeeded()
        {
            try
            {
                if (Directory.Exists(WikiRoot) && Directory.EnumerateFiles(WikiRoot, "*.md", SearchOption.AllDirectories).Any())
                {
                    return;
                }

                var seedDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? ".", "WikiSeed");
                var assemblyDir = Path.GetDirectoryName(typeof(LlmWikiStore).Assembly.Location);
                if (!Directory.Exists(seedDir) && assemblyDir != null)
                {
                    seedDir = Path.Combine(assemblyDir, "WikiSeed");
                }

                if (!Directory.Exists(seedDir))
                {
                    Logger.Warning($"LlmWiki: seed folder not found ({seedDir}); creating empty wiki");
                    Directory.CreateDirectory(WikiRoot);
                    return;
                }

                foreach (var file in Directory.EnumerateFiles(seedDir, "*.md", SearchOption.AllDirectories))
                {
                    var relative = Path.GetRelativePath(seedDir, file);
                    var target = Path.Combine(WikiRoot, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    if (!File.Exists(target))
                    {
                        File.Copy(file, target);
                    }
                }

                Logger.Info($"LlmWiki: seeded starter pack into {WikiRoot}");
            }
            catch (Exception ex)
            {
                Logger.Error($"LlmWiki: seeding failed: {ex.Message}");
            }
        }

        /// <summary>Lists every page with its first heading, index.md first.</summary>
        public static string GetIndex()
        {
            EnsureSeeded();
            try
            {
                var pages = Directory.EnumerateFiles(WikiRoot, "*.md", SearchOption.AllDirectories)
                    .Select(f => Path.GetRelativePath(WikiRoot, f).Replace('\\', '/'))
                    .OrderBy(p => p == "index.md" ? "" : p, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (pages.Count == 0)
                {
                    return "The wiki is empty.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Wiki pages ({pages.Count}) in {WikiRoot}:");
                foreach (var page in pages)
                {
                    var title = FirstHeading(Path.Combine(WikiRoot, page));
                    sb.AppendLine(string.IsNullOrEmpty(title) ? $"- {page}" : $"- {page} — {title}");
                }
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Wiki index failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Case-insensitive full-text search: every query term must appear in the page;
        /// results are ranked by hit count and returned as page + matching snippets.
        /// </summary>
        public static string Search(string? query)
        {
            EnsureSeeded();
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Empty search query.";
            }

            try
            {
                var terms = query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim())
                    .Where(t => t.Length > 1)
                    .ToArray();
                if (terms.Length == 0)
                {
                    return "Search terms too short.";
                }

                var results = new List<(string page, int score, string snippet)>();
                foreach (var file in Directory.EnumerateFiles(WikiRoot, "*.md", SearchOption.AllDirectories))
                {
                    var lines = File.ReadAllLines(file);
                    var content = string.Join("\n", lines);
                    if (!terms.All(t => content.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    var score = terms.Sum(t => CountOccurrences(content, t));
                    var snippet = BuildSnippet(lines, terms);
                    results.Add((Path.GetRelativePath(WikiRoot, file).Replace('\\', '/'), score, snippet));
                }

                if (results.Count == 0)
                {
                    return $"No wiki page matches '{query}'. Use wiki_index to see what exists.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"{results.Count} page(s) match '{query}':");
                foreach (var (page, score, snippet) in results.OrderByDescending(r => r.score).Take(MaxSearchResults))
                {
                    sb.AppendLine($"\n## {page}");
                    sb.AppendLine(snippet);
                }
                sb.AppendLine("\nUse wiki_read with the page path to read a full page.");
                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Wiki search failed: {ex.Message}";
            }
        }

        /// <summary>Reads one page by its path relative to the wiki root.</summary>
        public static string Read(string? page)
        {
            EnsureSeeded();
            if (string.IsNullOrWhiteSpace(page))
            {
                return "No page specified. Use wiki_index to list pages.";
            }

            try
            {
                var normalized = page.Replace('\\', '/').TrimStart('/');
                if (!normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    normalized += ".md";
                }

                var full = Path.GetFullPath(Path.Combine(WikiRoot, normalized));
                if (!full.StartsWith(Path.GetFullPath(WikiRoot), StringComparison.OrdinalIgnoreCase))
                {
                    return "Invalid page path.";
                }

                if (!File.Exists(full))
                {
                    return $"Page '{normalized}' does not exist. Use wiki_index or wiki_search to find pages.";
                }

                var content = File.ReadAllText(full);
                if (content.Length > MaxReadChars)
                {
                    content = content.Substring(0, MaxReadChars) + "\n\n[... page truncated ...]";
                }
                return content;
            }
            catch (Exception ex)
            {
                return $"Wiki read failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Appends an immutable observation to raw/&lt;writer&gt;-YYYY-MM-DD.md. Raw files are
        /// the append-only inbox the ingest agent later consolidates into wiki pages;
        /// in-session writes never touch wiki/ pages directly.
        /// </summary>
        public static string AppendRawNote(string writer, string? note)
        {
            EnsureSeeded();
            if (string.IsNullOrWhiteSpace(note))
            {
                return "Empty note - nothing recorded.";
            }

            try
            {
                var rawDir = Path.Combine(WikiRoot, "raw");
                Directory.CreateDirectory(rawDir);

                var fileName = $"{writer}-{DateTime.Now:yyyy-MM-dd}.md";
                var file = Path.Combine(rawDir, fileName);
                var isNew = !File.Exists(file);

                var sb = new StringBuilder();
                if (isNew)
                {
                    sb.AppendLine($"# {writer} — {DateTime.Now:yyyy-MM-dd}");
                    sb.AppendLine();
                }
                sb.AppendLine($"- {DateTime.Now:HH:mm} — {note.Trim().Replace("\r", " ").Replace("\n", " ")}");
                File.AppendAllText(file, sb.ToString());

                if (isNew)
                {
                    try
                    {
                        File.AppendAllText(Path.Combine(WikiRoot, "log.md"),
                            $"- {DateTime.Now:yyyy-MM-dd} — raw/{fileName} created by {writer}.{Environment.NewLine}");
                    }
                    catch
                    {
                        // best-effort
                    }
                }

                return $"Recorded in raw/{fileName}. The ingest agent will consolidate it into the wiki.";
            }
            catch (Exception ex)
            {
                return $"Wiki append failed: {ex.Message}";
            }
        }

        private static string? FirstHeading(string file)
        {
            try
            {
                foreach (var line in File.ReadLines(file).Take(10))
                {
                    if (line.StartsWith("# "))
                    {
                        return line.Substring(2).Trim();
                    }
                }
            }
            catch
            {
                // best-effort
            }
            return null;
        }

        private static int CountOccurrences(string content, string term)
        {
            var count = 0;
            var index = 0;
            while ((index = content.IndexOf(term, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += term.Length;
            }
            return count;
        }

        private static string BuildSnippet(string[] lines, string[] terms)
        {
            var picked = new SortedSet<int>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (terms.Any(t => lines[i].Contains(t, StringComparison.OrdinalIgnoreCase)))
                {
                    for (var c = Math.Max(0, i - SnippetContextLines); c <= Math.Min(lines.Length - 1, i + SnippetContextLines); c++)
                    {
                        picked.Add(c);
                    }
                }
                if (picked.Count > 12) break;
            }

            var sb = new StringBuilder();
            int? previous = null;
            foreach (var i in picked)
            {
                if (previous != null && i > previous + 1)
                {
                    sb.AppendLine("...");
                }
                sb.AppendLine(lines[i]);
                previous = i;
            }
            return sb.ToString().TrimEnd();
        }
    }
}
