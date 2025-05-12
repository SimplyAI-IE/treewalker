
// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\TreeTraversalService.cs
using System;
using System.Collections.Generic;
using System.IO;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class TreeTraversalService
    {
        private AccountParserService _parser = new AccountParserService();

        private Dictionary<string, List<AccountNode>> _accountIndex = new();

        public void IndexAccounts(string directory)
        {
            var trees = _parser.ParseTreeFiles(directory);

            Queue<AccountNode> queue = new Queue<AccountNode>(trees);
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!_accountIndex.ContainsKey(node.Account.Id.ToLower()))
                    _accountIndex[node.Account.Id.ToLower()] = new List<AccountNode>();

                _accountIndex[node.Account.Id.ToLower()].Add(node);

                foreach (var child in node.Children)
                    queue.Enqueue(child);
            }
        }

        public List<AccountNode> GetChildren(string accountId)
        {
            var result = new List<AccountNode>();

            if (_accountIndex.ContainsKey(accountId.ToLower()))
            {
                foreach (var node in _accountIndex[accountId.ToLower()])
                    result.AddRange(node.Children);
            }

            return result;
        }

        public Dictionary<string, List<AccountNode>> GetRecursiveChildren(string accountId)
        {
            var result = new Dictionary<string, List<AccountNode>>();
            var visited = new HashSet<string>();

            void Recurse(string id)
            {
                if (visited.Contains(id)) return;
                visited.Add(id);

                var children = GetChildren(id);
                if (children.Count > 0)
                {
                    result[id] = children;
                    foreach (var child in children)
                        Recurse(child.Account.Id);
                }
            }

            Recurse(accountId);
            return result;
        }
    }
}
