// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\AccountProcessor.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq; // Required for Linq operations like FirstOrDefault
using System.Xml.Linq; // Required for XDocument, XElement
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

                var originalNode = allAccounts[originalId.ToLower()]; // Get the original node once

                for (int i = 0; i < xmlPaths.Count; i++)
                {
                    var currentXmlPath = xmlPaths[i];
                    string objectNameFromFile = "DefaultObjectName"; // Default if not found

                    try
                    {
                        // Step 1: Extract objectName from the current XML file.
                        // This is a more robust way to get the object name for the specific object
                        // being processed, especially if one file contains multiple objects (though
                        // the current logic implies one relevant object per file path in this loop).
                        var tempDoc = XDocument.Load(currentXmlPath);
                        var objectElement = tempDoc.Descendants("Object")
                                               .FirstOrDefault(obj => obj.Element("AccountID")?.Value.Trim().Equals(originalId, StringComparison.OrdinalIgnoreCase) ?? false);

                        if (objectElement != null)
                        {
                            objectNameFromFile = objectElement.Element("Name")?.Value.Trim() ?? objectNameFromFile;
                        }
                        else
                        {
                             // If the object matching originalId is not found, log or skip.
                             // This might happen if the XML structure is unexpected or already modified.
                             Console.WriteLine($"Warning: Could not find object with AccountID '{originalId}' in file '{Path.GetFileName(currentXmlPath)}' to extract object name.");
                             // Decide if you want to continue with a default name or skip this file/iteration
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Error reading object name from XML file {Path.GetFileName(currentXmlPath)}. Error: {ex.Message}");
                        // Continue with default or skip, depending on desired error handling
                    }


                    // Step 2: Clone the account node using the extracted objectName
                    // Ensure originalNode is correctly scoped and represents the account to be cloned
                    var clone = _cloner.CloneAccountNode(originalNode, objectNameFromFile, accountDefs);
                    
                    // The parent for the clone should be the parent of the original node.
                    var parent = originalNode.Parent;

                    // Step 3: Now update the XML file with the correct newId from the clone
                    // The objectName out parameter from UpdateXmlFiles might be redundant now if extracted already,
                    // but the method signature requires it.
                    string updatedObjectName; // To satisfy the out parameter
                    _cloner.UpdateXmlFiles(new List<string> { currentXmlPath }, originalId, clone.Account.Id, out updatedObjectName);

                    // Add the cloned node to the tree structure
                    if (parent != null)
                    {
                        parent.AddChild(clone); // Add clone as a child of the original node's parent
                    }
                    else
                    {
                        // Handle case where originalNode is a root node, if necessary
                        // For now, assume clones of non-root nodes are more common in this logic.
                        // If originalNode could be a root, you might need to add `clone` to a list of new roots,
                        // or handle this scenario according to application requirements.
                        Console.WriteLine($"Warning: Original account '{originalId}' has no parent. Clone '{clone.Account.Id}' will not be added to the primary tree structure in this iteration.");
                    }
                    
                    _cloner.RecordCreatedAccount(clone.Account.RawLine);

                    // For updatedTree.txt, record the relationship between the original node's line and the clone's line.
                    // If the clone is effectively replacing the original for this object, 
                    // the relationship to log could be original -> clone, or parent_of_original -> clone.
                    // The spec G.5 implies original -> clone:
                    // +CfInterestCollected$ Interest Collected
                    //    +AcPXA_InterestCollected$ Interest Collected
                    // This structure implies the original is treated as a "parent" in the log for the clone.
                    // However, in the actual tree, the clone becomes a sibling of the original (or original is removed/replaced).
                    // The current RecordUpdatedTreeRelationship seems to expect the *actual* parent's raw line.
                    // If the clone is added under originalNode.Parent, then parent.Account.RawLine would be more accurate for the tree.
                    // Let's stick to the spirit of G.5 from specification.txt by showing original as the "conceptual" parent in this specific log.
                    _cloner.RecordUpdatedTreeRelationship(originalNode.Account.RawLine, clone.Account.RawLine);


                    Console.WriteLine($"Cloned account {originalId} -> {clone.Account.Id} for file {Path.GetFileName(currentXmlPath)} (Object: {objectNameFromFile})");
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