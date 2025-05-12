// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\AccountProcessor.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Added for FirstOrDefault
using System.Xml.Linq; // Added for XDocument
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
                if (node != null && node.Account != null && !string.IsNullOrEmpty(node.Account.Id))
                {
                    allAccounts[node.Account.Id.ToLower()] = node;
                }
                if (node != null) // Null check for node before accessing Children
                {
                    foreach (var child in node.Children)
                        queue.Enqueue(child);
                }
            }

            foreach (var match in matches)
            {
                string originalId = match.Key;
                List<string> xmlPaths = match.Value;

                if (!allAccounts.ContainsKey(originalId.ToLower())) // Check if original account exists
                {
                    Console.WriteLine($"Warning: Original account ID '{originalId}' not found in parsed accounts. Skipping cloning for this ID.");
                    continue;
                }

                // Only proceed with cloning if there are multiple XML files referencing the same account.
                // If only one XML file references the account, no cloning is needed according to typical interpretation of "uniqueness requirement".
                // The spec says "If multiple objects output to the same account..." implying cloning is for >1.
                if (xmlPaths.Count <= 1) 
                    continue;


                for (int i = 0; i < xmlPaths.Count; i++)
                {
                    var originalNode = allAccounts[originalId.ToLower()];
                    // It's crucial that originalNode.Parent is correctly populated earlier in the tree building.
                    // If it's not, this logic might need adjustment or parent lookup.
                    var parent = originalNode.Parent;
                    string currentXmlPath = xmlPaths[i];

                    // 1. Extract objectName from the current XML path for the originalId
                    string objectName = "DefaultObjectName"; // Fallback
                    try
                    {
                        var doc = XDocument.Load(currentXmlPath);
                        var objectElement = doc.Descendants("Object")
                            .FirstOrDefault(obj => obj.Element("AccountID")?.Value.Trim().Equals(originalId, StringComparison.OrdinalIgnoreCase) == true);

                        if (objectElement != null)
                        {
                            objectName = objectElement.Element("Name")?.Value.Trim() ?? objectName;
                        }
                        else
                        {
                            Console.WriteLine($"Warning: Could not find object with AccountID {originalId} in {Path.GetFileName(currentXmlPath)} for object name extraction. Using fallback name: {objectName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading object name from {Path.GetFileName(currentXmlPath)}: {ex.Message}. Using fallback name: {objectName}");
                    }

                    // 2. Clone the account node using the extracted objectName
                    var clone = _cloner.CloneAccountNode(originalNode, objectName, accountDefs);

                    // 3. Update the XML file with the new clone.Account.Id
                    // This assumes AccountClonerService.UpdateXmlFiles has been modified to:
                    // public void UpdateXmlFiles(List<string> xmlPaths, string originalId, string newId)
                    _cloner.UpdateXmlFiles(new List<string> { currentXmlPath }, originalId, clone.Account.Id);
                    
                    // Add the cloned node to the tree structure.
                    // If 'parent' is null (e.g. originalNode was a root or parent not found),
                    // the clone might need to be added to a list of new roots or handled differently.
                    parent?.AddChild(clone); 
                    // If parent is null, we might need to add 'clone' to the 'accountTrees' or a similar top-level collection
                    // For now, assuming parent exists for accounts that are cloned based on typical tree structures.


                    _cloner.RecordCreatedAccount(clone.Account.RawLine);

                    // Record the relationship: original as parent, clone as child in the updatedTree.txt
                    // This matches the specification "updatedTree.txt ... Format: +CfOriginal... \n   +AcPXA_New..."
                    // where the original *becomes* a parent to its own clone in the context of this output.
                    _cloner.RecordUpdatedTreeRelationship(originalNode.Account.RawLine, clone.Account.RawLine);


                    Console.WriteLine($"Cloned account {originalId} -> {clone.Account.Id} for file {Path.GetFileName(currentXmlPath)} and updated XML.");
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