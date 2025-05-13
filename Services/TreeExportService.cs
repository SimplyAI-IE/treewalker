
using System;
using System.Collections.Generic;
using System.IO;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class TreeExportService
    {
        public void ExportFullTree(string rootId, Dictionary<string, List<AccountNode>> tree, string outputPath)
        {
            var visited = new HashSet<string>();
            using (var writer = new StreamWriter(outputPath))
            {
                WriteNodeRecursive(rootId, tree, writer, "", visited);
            }
        }

        private void WriteNodeRecursive(string parentId, Dictionary<string, List<AccountNode>> tree, StreamWriter writer, string indent, HashSet<string> visited)
        {
            if (!tree.ContainsKey(parentId) || visited.Contains(parentId))
                return;

            visited.Add(parentId);

            foreach (var child in tree[parentId])
            {
                var acc = child.Account;
                writer.WriteLine($"+{acc.Id}$ {acc.Caption}");
                WriteNodeRecursive(acc.Id, tree, writer, indent + "  ", visited);
            }
        }
    }
}
