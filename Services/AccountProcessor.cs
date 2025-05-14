using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AccountTreeApp.Services
{
    public class AccountProcessor
    {
        private readonly Dictionary<string, List<string>> usageMap = new();
        private readonly Dictionary<string, string> firstDefinitions = new();
        private readonly Dictionary<string, Dictionary<string, string>> triplicates = new();

        public void Run(string treePath, string accountsDir, string xmoDir)
        {
            string accountsFile = Path.Combine(accountsDir, "Accounts.xxa");
            string updatedTreePath = Path.Combine(xmoDir, "updatedTree.txt");

            LoadAccounts(accountsFile);
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

        private void ScanXmoUsage(string dir)
        {
            foreach (var file in Directory.GetFiles(dir, "*.xmo"))
            {
                var doc = XDocument.Load(file);
                string obj = doc.Descendants("Name").FirstOrDefault()?.Value ?? "Unknown";

                foreach (var acc in doc.Descendants("AccountID").Select(x => x.Value))
                {
                    string key = ResolveKey(acc);
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

            treeWriter.WriteLine(origLine);
            treeWriter.WriteLine($"   {newLine}");
            logWriter.WriteLine($"[TREE] Added {newId} under {key}");

            if (triplicates.TryGetValue(groupKey, out var trio))
            {
                string type = key.Substring(0, 2);
                if (type == "Cf")
                {
                    if (trio.TryGetValue("Rv", out var rvLine))
                    {
                        treeWriter.WriteLine(rvLine);
                        treeWriter.WriteLine($"   {newLine}");
                        logWriter.WriteLine($"[TREE] Added {newId} under Rv (triplicate)");
                    }
                    else if (trio.TryGetValue("Co", out var coLine))
                    {
                        treeWriter.WriteLine(coLine);
                        treeWriter.WriteLine($"   {newLine}");
                        logWriter.WriteLine($"[TREE] Added {newId} under Co (triplicate)");
                    }
                }
                else if (type == "Rv" || type == "Co")
                {
                    if (trio.TryGetValue("Cf", out var cfLine))
                    {
                        treeWriter.WriteLine(cfLine);
                        treeWriter.WriteLine($"   {newLine}");
                        logWriter.WriteLine($"[TREE] Added {newId} under Cf (triplicate)");
                    }
                }
            }

            foreach (var file in Directory.GetFiles(xmoDir, "*.xmo"))
            {
                var doc = XDocument.Load(file);
                var name = doc.Descendants("Name").FirstOrDefault()?.Value ?? "";
                if (name != obj) continue;

                foreach (var tag in doc.Descendants("AccountID"))
                {
                    if (ResolveKey(tag.Value) == key)
                    {
                        logWriter.WriteLine($"[REWRITE] {key} → {newId} in {Path.GetFileName(file)}");
                        tag.Value = newId;
                    }
                }
                doc.Save(file);
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
