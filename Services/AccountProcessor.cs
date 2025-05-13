
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
            var accountDefs = _accountParser.ParseDefinitions(Path.Combine(accountsDir, "Accounts.xxa"));

            var outputMatch = new List<string>();
            var createdAccounts = new List<string>();
            var updatedTree = new List<string>();
            var usageTracker = new Dictionary<string, List<(string file, string objectName)>>();

            foreach (var file in Directory.GetFiles(databaseDir, "*.xmo"))
            {
                var doc = XDocument.Load(file);
                foreach (var obj in doc.Descendants("Object"))
                {
                    var idElem = obj.Element("AccountID");
                    var nameElem = obj.Element("Name");
                    if (idElem == null || nameElem == null) continue;

                    string accountId = idElem.Value.Trim();
                    string objectName = nameElem.Value.Trim();

                    if (tree.ContainsKey(accountId))
                    {
                        outputMatch.Add($"{Path.GetFileName(file)} \"{accountId}\"");

                        if (!usageTracker.ContainsKey(accountId))
                            usageTracker[accountId] = new List<(string, string)>();

                        usageTracker[accountId].Add((file, objectName));
                    }
                }
            }

            File.WriteAllLines(Path.Combine(databaseDir, "AccountsWithObjectOutput.txt"), outputMatch.Distinct());

            foreach (var kvp in usageTracker)
            {
                var accountId = kvp.Key;
                var users = kvp.Value;

                if (users.Count <= 1) continue;

                string caption = accountDefs.ContainsKey(accountId) && accountDefs[accountId].RawLine.Contains('$')
                    ? accountDefs[accountId].RawLine.Split('$')[1].Trim()
                    : "";


                foreach (var (file, objectName) in users)
                {
                    var initials = new string(objectName.Split().Where(w => w.Length > 0).Select(w => char.ToUpper(w[0])).ToArray());
                    var newId = $"Ac{initials}_{accountId.Substring(2)}";
                    var newLine = $"+{newId}$ {caption}";

                    createdAccounts.Add(newLine);
                    updatedTree.Add($"+{accountId}$ {caption}");
                    updatedTree.Add($"   +{newId}$ {caption}");

                    var doc = XDocument.Load(file);
                    var accElem = doc.Descendants("AccountID").FirstOrDefault(e => e.Value.Trim() == accountId);
                    if (accElem != null)
                    {
                        accElem.Value = newId;
                        doc.Save(file);
                    }
                }
            }

            File.WriteAllLines(Path.Combine(databaseDir, "createdAccounts.txt"), createdAccounts.Distinct());
            File.WriteAllLines(Path.Combine(databaseDir, "updatedTree.txt"), updatedTree.Distinct());

            Console.WriteLine("Processing complete.");
        }
    }
}
