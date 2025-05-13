
using System;
using System.Collections.Generic;
using System.IO;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class TreeTraversalService
    {
        private Dictionary<string, AccountNode> _indexedAccounts = new();

        public void IndexAccounts(string accountsDir)
        {
            _indexedAccounts.Clear();

            foreach (var file in Directory.GetFiles(accountsDir, "*.xxa"))
            {
                var lines = File.ReadAllLines(file);
                AccountNode? currentParent = null;

                foreach (var line in lines)
                {
                    if (!line.StartsWith("+")) continue;

                    var parts = line.Substring(1).Split('$');
                    if (parts.Length < 2) continue;

                    var id = parts[0].Trim();
                    var caption = parts[1].Trim();

                    var node = new AccountNode(new Account { Id = id, Caption = caption })
            {
                Children = new List<AccountNode>()
            };

                    if (currentParent == null)
                    {
                        _indexedAccounts[id] = node;
                        currentParent = node;
                    }
                    else
                    {
                        currentParent.Children.Add(node);
                    }
                }
            }
        }

        public Dictionary<string, List<AccountNode>> GetRecursiveChildren(string rootId)
        {
            var results = new Dictionary<string, List<AccountNode>>();

            if (_indexedAccounts.TryGetValue(rootId, out var root))
                Traverse(root, results);

            return results;
        }

        private void Traverse(AccountNode node, Dictionary<string, List<AccountNode>> results)
        {
            if (!results.ContainsKey(node.Account.Id))
                results[node.Account.Id] = new List<AccountNode>();

            foreach (var child in node.Children)
            {
                results[node.Account.Id].Add(child);
                Traverse(child, results);
            }
        }

        public AccountNode? FindAccountNode(string accountId)
        {
            foreach (var tree in _indexedAccounts.Values)
            {
                var found = FindNodeRecursive(tree, accountId);
                if (found != null) return found;
            }
            return null;
        }

        private AccountNode? FindNodeRecursive(AccountNode node, string id)
        {
            if (node.Account.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                return node;

            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, id);
                if (found != null) return found;
            }

            return null;
        }
    }
}
