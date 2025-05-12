// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\AccountProcessor.cs
using System;
using System.Collections.Generic;
using System.IO;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class AccountProcessor
    {
        private AccountParserService _parser = new AccountParserService();
        private ObjectMatcherService _matcher = new ObjectMatcherService();
        private AccountClonerService _cloner = new AccountClonerService();
        private TreeTraversalService _traversal = new TreeTraversalService();

        public void Run(string accountId, string accountsDir, string databaseDir)
        {
            Console.WriteLine("Parsing account definitions...");
            var definitionsPath = Path.Combine(accountsDir, "Accounts.xxa");
            var accountDefs = _parser.ParseDefinitions(definitionsPath);

            Console.WriteLine("Parsing account trees...");
            var accountTrees = _parser.ParseTreeFiles(accountsDir);
            Console.WriteLine($"Parsed {accountTrees.Count} root nodes.");

            Console.WriteLine("Indexing accounts for traversal...");
            _traversal.IndexAccounts(accountsDir);

            Console.WriteLine($"Recursively finding all children of {accountId}...");
            var allDescendants = _traversal.GetRecursiveChildren(accountId);
            foreach (var parent in allDescendants.Keys)
            {
                Console.WriteLine($"> {parent} has children:");
                foreach (var child in allDescendants[parent])
                    Console.WriteLine($"    - {child.Account.Id}: {child.Account.Caption}");
            }

            Console.WriteLine("Matching objects by account ID...");
            var matches = _matcher.MatchObjectsByAccountId(databaseDir);
            Console.WriteLine($"Found {matches.Count} unique account references in XML objects.");

            Dictionary<string, AccountNode> allAccounts = new();
            Queue<AccountNode> queue = new Queue<AccountNode>(accountTrees);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                allAccounts[node.Account.Id.ToLower()] = node;
                foreach (var child in node.Children)
                    queue.Enqueue(child);
            }

            foreach (var match in matches)
            {
                string originalId = match.Key;
                List<string> xmlPaths = match.Value;

                if (xmlPaths.Count <= 1 || !allAccounts.ContainsKey(originalId.ToLower()))
                    continue;

                for (int i = 0; i < xmlPaths.Count; i++)
                {
                    var originalNode = allAccounts[originalId.ToLower()];
                    var parent = originalNode.Parent;

                    string objectName;
                    _cloner.UpdateXmlFiles(new List<string> { xmlPaths[i] }, originalId, "", out objectName);

                    var clone = _cloner.CloneAccountNode(originalNode, objectName, accountDefs);


                    parent?.AddChild(clone);

                    _cloner.RecordCreatedAccount(clone.Account.RawLine);
                    if (parent != null)
                        _cloner.RecordUpdatedTreeRelationship(originalNode.Account.RawLine, clone.Account.RawLine);


                    Console.WriteLine($"Cloned account {originalId} -> {clone.Account.Id} for file {Path.GetFileName(xmlPaths[i])}");
                }
            }

            Console.WriteLine("Account cloning and XML update complete.");

            if (databaseDir.TrimEnd('\\', '/').EndsWith("_extracted", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    Directory.Delete(databaseDir, true);
                    Console.WriteLine($"Deleted temp folder: {databaseDir}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to delete temp folder. Reason: {ex.Message}");
                }
            }
        }
    }
}
