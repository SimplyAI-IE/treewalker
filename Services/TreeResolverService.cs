
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class TreeResolverService
    {
        private Dictionary<string, List<AccountNode>> _parentToChildren = new();
        private Dictionary<string, Account> _idToAccount = new();

        public void LoadFromFiles(string accountsDir)
        {
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

                    var account = new Account { Id = id, Caption = caption };
                    var node = new AccountNode(account);

                    if (!_idToAccount.ContainsKey(id))
                        _idToAccount[id] = account;

                    if (currentParent == null)
                    {
                        currentParent = node;
                    }
                    else
                    {
                        if (!_parentToChildren.ContainsKey(currentParent.Account.Id))
                            _parentToChildren[currentParent.Account.Id] = new List<AccountNode>();

                        if (!_parentToChildren[currentParent.Account.Id].Any(c => c.Account.Id == id))
                            _parentToChildren[currentParent.Account.Id].Add(node);

                        currentParent = node;
                    }
                }
            }
        }

        public AccountNode BuildFullTree(string rootId)
        {
            var visited = new HashSet<string>();
            return BuildRecursive(rootId, visited);
        }

        private AccountNode BuildRecursive(string id, HashSet<string> visited)
        {
            if (visited.Contains(id))
                return new AccountNode(_idToAccount.ContainsKey(id) ? _idToAccount[id] : new Account { Id = id, Caption = "(Unknown)" });

            visited.Add(id);

            var account = _idToAccount.ContainsKey(id) ? _idToAccount[id] : new Account { Id = id, Caption = "(Unknown)" };
            var node = new AccountNode(account);

            if (_parentToChildren.ContainsKey(id))
            {
                foreach (var child in _parentToChildren[id])
                {
                    node.Children.Add(BuildRecursive(child.Account.Id, visited));
                }
            }

            return node;
        }
    }
}
