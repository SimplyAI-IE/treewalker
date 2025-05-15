using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace AccountTreeApp.Services
{
    public class AccountProcessor
    {
        private readonly Dictionary<string, List<string>> usageMap = new();
        private readonly Dictionary<string, string> firstDefinitions = new();
        private readonly Dictionary<string, Dictionary<string, string>> triplicates = new();
        private readonly HashSet<string> treeAccounts = new();


        public void Run(string treePath, string accountsDir, string xmoDir)
        {
            string accountsFile = Path.Combine(accountsDir, "Accounts.xxa");
            string updatedTreePath = Path.Combine(xmoDir, "updatedTree.txt");

            LoadAccounts(accountsFile);
            LoadTreeAccounts(treePath);
            ScanXmoUsage(xmoDir);
            FixConflicts(accountsFile, xmoDir, updatedTreePath);

            Console.WriteLine("✅ Processing complete.");
        }

        private void LoadAccounts(string file)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line) || (line[0] != '+' && line[0] != '-')) continue;

                var parts = ParseLine(line);
                if (parts is not { } p) continue;

                string key = p.Type + p.Id;
                if (!firstDefinitions.ContainsKey(key))
                    firstDefinitions[key] = line;

                if (IsTriplicateType(p.Type))
                {
                    string groupKey = p.Id + p.Mods;
                    if (!triplicates.ContainsKey(groupKey))
                        triplicates[groupKey] = new Dictionary<string, string>();
                    triplicates[groupKey][p.Type] = line;
                }
            }
        }

private void LoadTreeAccounts(string treePath)
{
    foreach (var line in File.ReadLines(treePath))
    {
        string trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed[0] != '+') continue;

        string type = trimmed.Substring(1, 2);
        int modIndex = trimmed.IndexOfAny(new[] { '$', '%', '#', '*', ' ' });
        if (modIndex < 3) modIndex = trimmed.Length;

        string id = trimmed.Substring(3, modIndex - 3);
        string mods = "";
        while (modIndex < trimmed.Length && "$%#*".Contains(trimmed[modIndex]))
            mods += trimmed[modIndex++];

        string groupKey = id + mods;
        treeAccounts.Add(type + id);

        // Always add all types in the triplicate group
        if (triplicates.TryGetValue(groupKey, out var parts))
        {
            foreach (var partType in parts.Keys)
            {
                treeAccounts.Add(partType + id);
            }
        }
    }
}




private void ScanXmoUsage(string dir)
{
    foreach (var file in Directory.GetFiles(dir, "*.xmo"))
    {
        Console.WriteLine($"[READ] Scanning {Path.GetFileName(file)}");

        XDocument doc;
        try
        {
            doc = XDocument.Load(file);
            Console.WriteLine($"[READ] Successfully parsed {Path.GetFileName(file)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Could not load XML from {file}: {ex.Message}");
            continue;
        }

        string obj = doc.Descendants("Name").FirstOrDefault()?.Value ?? "Unknown";

        var accountNames = doc.Descendants("Account")
                              .Elements("Name")
                              .Select(x => x.Value.Trim());

        foreach (var acc in accountNames)
        {
            // Look for any case-insensitive match in treeAccounts
            var match = treeAccounts.FirstOrDefault(t =>
                string.Equals(t, acc, StringComparison.OrdinalIgnoreCase));

            if (match == null) continue;

            string key = ResolveKey(match); // Use the exact casing from treeAccounts

            if (!usageMap.ContainsKey(key))
                usageMap[key] = new();
            if (!usageMap[key].Contains(obj))
                usageMap[key].Add(obj);
        }
    }
}



private void FixConflicts(string accountsFile, string xmoDir, string treeFile)
{
    Directory.CreateDirectory(xmoDir);

    string createdAccountsPath = Path.Combine(xmoDir, "createdAccounts.txt");
    string logPath = Path.Combine(xmoDir, "processing.log");

    using var treeWriter = File.AppendText(treeFile);
    using var accountWriter = File.AppendText(accountsFile);
    using var createdWriter = File.AppendText(createdAccountsPath);
    using var logWriter = File.AppendText(logPath);

    var writtenTreeHeaders = new HashSet<string>(); // ✅ fix: keyed by Type+ID, not full line

    var writtenRelations = new HashSet<string>();

void WriteTreeRelation(string parentLine, string childLine)
{
    string parentKey = parentLine.Substring(1).Split(' ')[0]; // e.g., RvInterestCollected$
    string childKey = childLine.Substring(1).Split(' ')[0];   // e.g., AcIO_InterestCollected$
    string relationKey = parentKey + ">" + childKey;

    if (!writtenRelations.Contains(relationKey))
    {
        if (!writtenTreeHeaders.Contains(parentKey))
        {
            treeWriter.WriteLine(parentLine);
            writtenTreeHeaders.Add(parentKey);
        }


        treeWriter.WriteLine($"   {childLine}");
        writtenRelations.Add(relationKey);
    }
}


    foreach (var (key, objects) in usageMap)
    {
        if (objects.Count <= 1 || !firstDefinitions.ContainsKey(key)) continue;

        logWriter.WriteLine($"[CONFLICT] {key} used by: {string.Join(", ", objects)}");

        string origLine = firstDefinitions[key];
        string accountId = key[2..]; // drop type prefix
        var mods = ExtractModifiers(origLine);
        string groupKey = accountId + mods;

        // Track per-object clone IDs
        var objectToCloneId = new Dictionary<string, string>();

        foreach (var obj in objects)
        {
            writtenTreeHeaders.Clear();       // 👈 RESET PER OBJECT
            writtenRelations.Clear();         // 👈 RESET PER OBJECT
            string initials = string.Concat(
                Regex.Split(obj.Replace("-", " "), @"\s+")
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => Regex.Replace(w, @"\.", "").ToUpperInvariant()));
            string newId = $"Ac{initials}_{accountId}";
            string newLine = "+" + newId + origLine.Substring(key.Length + 1);

            objectToCloneId[obj] = newId;

            accountWriter.WriteLine(newLine);
            createdWriter.WriteLine(newLine);
            logWriter.WriteLine($"[CLONE] {newLine} from {origLine}");

            WriteTreeRelation(origLine, newLine);
            logWriter.WriteLine($"[TREE] Added {newId} under {key}");

            if (triplicates.TryGetValue(groupKey, out var trio))
            {
                string type = key.Substring(0, 2);
                if (type == "Cf")
                {
                    if (trio.TryGetValue("Rv", out var rvLine))
                    {
                        WriteTreeRelation(rvLine, newLine);
                        logWriter.WriteLine($"[TREE] Added {newId} under Rv (triplicate)");
                    }
                    if (trio.TryGetValue("Co", out var coLine))
                    {
                        WriteTreeRelation(coLine, newLine);
                        logWriter.WriteLine($"[TREE] Added {newId} under Co (triplicate)");
                    }
                }
                else if (type == "Rv" || type == "Co")
                {
                    if (trio.TryGetValue("Cf", out var cfLine))
                    {
                        WriteTreeRelation(cfLine, newLine);
                        logWriter.WriteLine($"[TREE] Added {newId} under Cf (triplicate)");
                    }
                }
            }
        }

        // Do targeted replacement per object using their correct newId
        foreach (var (objName, newId) in objectToCloneId)
        {
            var file = Directory.GetFiles(xmoDir, "*.xmo")
                                .FirstOrDefault(f =>
                                {
                                    try
                                    {
                                        var d = XDocument.Load(f);
                                        return d.Descendants("Name").FirstOrDefault()?.Value?.Trim() == objName;
                                    }
                                    catch { return false; }
                                });

            if (string.IsNullOrEmpty(file)) continue;

            string text = File.ReadAllText(file);
            XDocument doc;
            try
            {
                doc = XDocument.Load(file);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Could not load XML from {file}: {ex.Message}");
                continue;
            }

            int replaceCount = 0;
            var accountTags = doc.Descendants("Account").Elements("Name");
            foreach (var tag in accountTags)
            {
                var tagValue = tag.Value.Trim();
                if (string.Equals(ResolveKey(tagValue), key, StringComparison.OrdinalIgnoreCase))
                {
                    string pattern = $">{Regex.Escape(tagValue)}<";
                    string replacement = $">{newId}<";
                    string updatedText = Regex.Replace(text, pattern, replacement);

                    if (!ReferenceEquals(updatedText, text))
                    {
                        text = updatedText;
                        replaceCount++;
                        logWriter.WriteLine($"[REWRITE] {key} → {newId} in {Path.GetFileName(file)}");
                    }
                }
            }

            if (replaceCount > 0)
            {
                File.WriteAllText(file, text, new UTF8Encoding(false));
            }
        }
    }
}



private string ResolveKey(string raw)
{
    raw = raw.Trim();
    if (raw.Length < 3) return raw;

    string type = raw.Substring(0, 2);
    string id = raw.Substring(2);

    if (type.Equals("Cf", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var t in new[] { "Co", "Rv" })
        {
            string candidate = t + id;
            if (firstDefinitions.Keys.Any(k => string.Equals(k, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }
    }

    return raw;
}


        private bool IsTriplicateType(string type) => type is "Cf" or "Co" or "Ap" or "Rv" or "Ar";

        private string ExtractModifiers(string line)
        {
            var m = line.IndexOfAny(new[] { '$', '%', '#', '*', ' ' }, 3);
            if (m < 0) return "";
            var after = line[m..].Trim();
            return string.Concat(after.TakeWhile(c => "$%#*".Contains(c)));
        }

        private (string Dir, string Type, string Id, string Mods, string Caption)? ParseLine(string line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length < 4) return null;
            string dir = trimmed.Substring(0, 1);
            string type = trimmed.Substring(1, 2);
            int i = 3;
            while (i < trimmed.Length && !char.IsWhiteSpace(trimmed[i]) && !"$%#*".Contains(trimmed[i])) i++;
            string id = trimmed.Substring(3, i - 3);
            string mods = "";
            while (i < trimmed.Length && "$%#*".Contains(trimmed[i])) mods += trimmed[i++];
            string cap = trimmed.Substring(i).Trim();
            return (dir, type, id, mods, cap);
        }
    }
}
