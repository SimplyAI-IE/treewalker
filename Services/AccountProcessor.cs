using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class AccountProcessor
    {
        private AccountParserService _accountParser = new AccountParserService();
        private TreeParserService _treeParser = new TreeParserService();
        private AccountClonerService _cloner = new AccountClonerService();

public void Run(string treePath, string accountsDir, string databaseDir)
{
    var tree = _treeParser.ParseTree(treePath);
    var accountsPath = Path.Combine(accountsDir, "Accounts.xxa");
    var accountDefs = _accountParser.ParseDefinitions(accountsPath);

    var createdAccounts = new HashSet<string>();
    var updatedTree = new List<string>();
    var usageTracker = new Dictionary<string, List<(string file, string objectName)>>();

    foreach (string file in Directory.GetFiles(databaseDir, "*.xmo"))
    {
        var doc = XDocument.Load(file);
        foreach (var obj in doc.Descendants("Object"))
        {
            var idElem = obj.Element("AccountID");
            var nameElem = obj.Element("Name");
            if (idElem == null || nameElem == null) continue;

            string originalId = idElem.Value.Trim();
            string objectName = nameElem.Value.Trim();

            var match = accountDefs.Values.FirstOrDefault(a => $"{a.AccountType}{a.AccountId}" == originalId);
            if (match == null) continue;

            string key = $"{match.AccountType}{match.AccountId}";
            if (!usageTracker.ContainsKey(key))
                usageTracker[key] = new List<(string, string)>();

            usageTracker[key].Add((file, objectName));
        }
    }

    foreach (var kvp in usageTracker)
    {
        var key = kvp.Key;
        var users = kvp.Value;

        if (users.Count <= 1) continue;

        var original = accountDefs.Values.First(a => $"{a.AccountType}{a.AccountId}" == key);
        string originalLine = original.RawLine;

        if (!updatedTree.Contains(originalLine))
            updatedTree.Add(originalLine);

        // If Cf type — check for triplicate (Co or Rv)
        string[] linkedTypes = original.AccountType == "Cf" ? new[] { "Co", "Rv" } : Array.Empty<string>();
        foreach (var type in linkedTypes)
        {
            string tripKey = $"{type}{original.AccountId}";
            var trip = accountDefs.Values.FirstOrDefault(a => $"{a.AccountType}{a.AccountId}" == tripKey && a.Modifiers == original.Modifiers);
            if (trip != null && !updatedTree.Contains(trip.RawLine))
                updatedTree.Add(trip.RawLine);
        }

        foreach (var (file, objectName) in users)
        {
            string initials = new string(objectName.Split().Where(w => w.Length > 0).Select(w => char.ToUpper(w[0])).ToArray());
            string newId = $"Ac{initials}_{original.AccountId}";
            string newLine = $"+{newId}{original.Modifiers} {original.Caption}".Trim();

            if (createdAccounts.Add(newLine))
                File.AppendAllText(accountsPath, newLine + Environment.NewLine);

            updatedTree.Add($"   {newLine}");

            foreach (var type in linkedTypes)
            {
                string tripKey = $"{type}{original.AccountId}";
                var trip = accountDefs.Values.FirstOrDefault(a => $"{a.AccountType}{a.AccountId}" == tripKey && a.Modifiers == original.Modifiers);
                if (trip != null)
                    updatedTree.Add($"   {newLine}");
            }

            var doc = XDocument.Load(file);
            var target = doc.Descendants("AccountID").FirstOrDefault(e => e.Value.Trim() == key);
            if (target != null)
            {
                target.Value = newId;
                doc.Save(file);
            }
        }
    }

    File.WriteAllLines(Path.Combine(databaseDir, "updatedTree.txt"), updatedTree.Distinct());
    Console.WriteLine("Conflicts resolved and updates written.");
}

    }
}
