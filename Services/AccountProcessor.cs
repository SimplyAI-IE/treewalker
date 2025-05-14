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
                if (string.IsNullOrWhiteSpace(line) || line[0] != '+') continue;
                string trimmed = line.TrimStart('+', '-').Trim();
                var type = trimmed.Substring(0, 2);
                int modIndex = trimmed.IndexOfAny(new[] { '$', '%', '#', '*', ' ' });
                if (modIndex < 2) continue;
                string id = trimmed.Substring(2, modIndex - 2);
                treeAccounts.Add(type + id);
            }
        }



private void ScanXmoUsage(string dir)
{
    foreach (var file in Directory.GetFiles(dir, "*.xmo"))
    {
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

        string obj = doc.Descendants("Name").FirstOrDefault()?.Value ?? "Unknown";

        var accountNames = doc.Descendants("Account")
                              .Elements("Name")
                              .Select(x => x.Value.Trim());

        foreach (var acc in accountNames)
        {
            string key = ResolveKey(acc);
            if (!treeAccounts.Contains(key)) continue;

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

    var writtenTreeParents = new HashSet<string>();
    var treeChildren = new Dictionary<string, HashSet<string>>();

    void WriteTreeRelation(string parent, string child)
    {
        if (!writtenTreeParents.Contains(parent))
        {
            treeWriter.WriteLine(parent);
            writtenTreeParents.Add(parent);
        }

        if (!treeChildren.ContainsKey(parent))
            treeChildren[parent] = new HashSet<string>();

        if (!treeChildren[parent].Contains(child))
        {
            treeWriter.WriteLine($"   {child}");
            treeChildren[parent].Add(child);
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

        foreach (var obj in objects)
        {
            string initials = string.Concat(obj.Split(' ').Select(w => w[0])).ToUpper();
            string newId = $"Ac{initials}_{accountId}";
            string newLine = "+" + newId + origLine.Substring(key.Length + 1);

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
                    else if (trio.TryGetValue("Co", out var coLine))
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
            

foreach (var objName in objects)
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
        if (ResolveKey(tagValue) == key)
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
        File.WriteAllText(file, text, new System.Text.UTF8Encoding(false));
    }
}


        }
    }
}





        private string ResolveKey(string raw)
        {
            if (raw.StartsWith("Cf"))
            {
                string id = raw[2..];
                foreach (var type in new[] { "Co", "Rv" })
                    if (firstDefinitions.ContainsKey(type + id))
                        return type + id;
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
