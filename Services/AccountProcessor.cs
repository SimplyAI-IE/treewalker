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

    var byKey = accountDefs.Values
        .GroupBy(a => a.AccountType + a.AccountId, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    var createdAccounts = new HashSet<string>();
    var treeMap = new Dictionary<string, List<string>>();
    var usageMap = new Dictionary<string, List<(string file, string objectName)>>();

    foreach (var file in Directory.GetFiles(databaseDir, "*.xmo"))
    {
        var doc = XDocument.Load(file);
        foreach (var obj in doc.Descendants("Object"))
        {
            var idElem = obj.Element("AccountID");
            var nameElem = obj.Element("Name");
            if (idElem == null || nameElem == null) continue;

            string accRef = idElem.Value.Trim();
            string objectName = nameElem.Value.Trim();

            if (!usageMap.ContainsKey(accRef))
                usageMap[accRef] = new List<(string, string)>();

            usageMap[accRef].Add((file, objectName));
        }
    }

    foreach (var (accRef, users) in usageMap.Where(kvp => kvp.Value.Count > 1))
    {
        if (!byKey.TryGetValue(accRef, out var account)) continue;

        string id = account.AccountId;
        string mods = account.Modifiers;

        // Only check owner-defined parts
        bool hasCo = byKey.TryGetValue("Co" + id, out var co) && co.Modifiers == mods;
        bool hasAp = byKey.TryGetValue("Ap" + id, out var ap) && ap.Modifiers == mods;
        bool hasCf = byKey.TryGetValue("Cf" + id, out var cf) && cf.Modifiers == mods;
        bool hasRv = byKey.TryGetValue("Rv" + id, out var rv) && rv.Modifiers == mods;
        bool hasAr = byKey.TryGetValue("Ar" + id, out var ar) && ar.Modifiers == mods;

        bool isCostTriplicate = hasCo && hasAp && hasCf;
        bool isIncomeTriplicate = hasRv && hasAr && hasCf;

        var parents = new List<string> { account.RawLine };
        if (isCostTriplicate)
            parents = new[] { cf, co }
                .Where(x => x != null)
                .Select(x => x.RawLine).Distinct().ToList();

        else if (isIncomeTriplicate)
            parents = new[] { cf, rv }
                .Where(x => x != null)
                .Select(x => x.RawLine).Distinct().ToList();

        foreach (var (file, objectName) in users)
        {
            string initials = new string(objectName.Split().Where(w => w.Length > 0)
                                                   .Select(w => char.ToUpper(w[0])).ToArray());
            string newId = $"Ac{initials}_{account.AccountId}";
            string newLine = $"+{newId}{account.Modifiers} {account.Caption}".Trim();

            if (createdAccounts.Add(newLine))
                File.AppendAllText(accountsPath, newLine + Environment.NewLine);

            foreach (var parent in parents)
            {
                if (!treeMap.ContainsKey(parent))
                    treeMap[parent] = new List<string>();
                treeMap[parent].Add(newLine);
            }

            var doc = XDocument.Load(file);
            var target = doc.Descendants("AccountID").FirstOrDefault(e => e.Value.Trim() == accRef);
            if (target != null)
            {
                target.Value = newId;
                doc.Save(file);
            }
        }
    }

    var updatedTree = new List<string>();
    foreach (var parent in treeMap.Keys)
    {
        updatedTree.Add(parent);
        foreach (var child in treeMap[parent].Distinct())
            updatedTree.Add($"   {child}");
    }

    File.WriteAllLines(Path.Combine(databaseDir, "updatedTree.txt"), updatedTree);
    Console.WriteLine("Conflicts resolved and updates written.");
}


    }
}
