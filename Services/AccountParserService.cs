
// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\AccountParserService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class AccountParserService
    {
        public Dictionary<string, Account> ParseDefinitions(string filePath)
        {
            var accounts = new Dictionary<string, Account>(StringComparer.OrdinalIgnoreCase);

            foreach (var line in File.ReadAllLines(filePath))
            {
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("+")) continue;

                string trimmed = line.Substring(1);
                var parts = trimmed.Split('$');
                string id = parts[0].Trim();
                string caption = parts.Length > 1 ? parts[1].Trim() : "";

                accounts[id.ToLower()] = new Account
                {
                    Id = id,
                    Caption = caption,
                    RawLine = line.Trim(),
                    Depth = 0
                };
            }

            return accounts;
        }

        public List<AccountNode> ParseTreeFiles(string directory)
        {
            var rootNodes = new List<AccountNode>();
            var accountStack = new Stack<(int Depth, AccountNode Node)>();

            foreach (var file in Directory.GetFiles(directory, "*.xxa"))
            {
                if (Path.GetFileName(file).Equals("Accounts.xxa", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    int depth = line.TakeWhile(Char.IsWhiteSpace).Count();
                    string trimmed = line.TrimStart();

                    if (trimmed.StartsWith("+"))
                        trimmed = trimmed.Substring(1);

                    string[] parts = trimmed.Split(new[] { ' ', '|' }, 2, StringSplitOptions.RemoveEmptyEntries);
                    string id = parts[0].Trim();
                    string caption = parts.Length > 1 ? parts[1].Trim() : "";

                    var account = new Account
                    {
                        Id = id,
                        Caption = caption,
                        RawLine = line.Trim(),
                        Depth = depth
                    };

                    var node = new AccountNode(account);

                    while (accountStack.Count > 0 && accountStack.Peek().Depth >= depth)
                        accountStack.Pop();

                    if (accountStack.Count == 0)
                    {
                        rootNodes.Add(node);
                    }
                    else
                    {
                        accountStack.Peek().Node.AddChild(node);
                    }

                    accountStack.Push((depth, node));
                }
            }

            return rootNodes;
        }
    }
}
