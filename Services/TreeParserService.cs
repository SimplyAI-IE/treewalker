using System;
using System.Collections.Generic;
using System.IO;

namespace AccountTreeApp.Services
{
    public class TreeParserService
    {
        public Dictionary<string, List<string>> ParseTree(string path)
        {
            var lines = File.ReadAllLines(path);
            var parentStack = new Stack<string>();
            var tree = new Dictionary<string, List<string>>();
            var lastIndent = -1;

            foreach (var line in lines)
            {
                if (!line.TrimStart().StartsWith("+")) continue;

                int indent = line.TakeWhile(c => c == ' ').Count();
                string cleanLine = line.Trim();

                // Improved parsing: handles $ % # modifiers and ensures AccountId is clean
                string raw = cleanLine.Substring(1).Split(' ', 2)[0]; // +AcMyId$ → AcMyId$
                string accountId = raw.Substring(2).Trim('$', '%', '#');

                if (indent > lastIndent)
                {
                    if (parentStack.Count > 0)
                    {
                        var parent = parentStack.Peek();
                        if (!tree.ContainsKey(parent))
                            tree[parent] = new List<string>();
                        tree[parent].Add(accountId);
                    }
                }
                else
                {
                    while (parentStack.Count > indent / 3)
                        parentStack.Pop();

                    if (parentStack.Count > 0)
                    {
                        var parent = parentStack.Peek();
                        if (!tree.ContainsKey(parent))
                            tree[parent] = new List<string>();
                        tree[parent].Add(accountId);
                    }
                }

                parentStack.Push(accountId);
                lastIndent = indent;
            }

            return tree;
        }
    }
}
