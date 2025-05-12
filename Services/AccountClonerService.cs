// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\AccountClonerService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using AccountTreeApp.Models;

namespace AccountTreeApp.Services
{
    public class AccountClonerService
    {
        private HashSet<string> writtenIds = new();

        // Updated method signature: removed 'out string objectName'
        public void UpdateXmlFiles(List<string> xmlPaths, string originalId, string newId)
        {
            // objectName = ""; // This line is removed

            foreach (var path in xmlPaths)
            {
                var doc = XDocument.Load(path);
                var changed = false;

                foreach (var obj in doc.Descendants("Object"))
                {
                    var accElem = obj.Element("AccountID");
                    // var nameElem = obj.Element("Name"); // No longer needed to find the name here

                    var existingId = accElem?.Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(existingId) &&
                        existingId.Equals(originalId, StringComparison.OrdinalIgnoreCase))
                    {
                        accElem.Value = newId;
                        changed = true;
                    }
                    else
                    {
                        Console.WriteLine($"Skipped update in {path}: ID '{existingId}' ≠ '{originalId}'");
                        // The following lines for objectName are removed as it's handled in AccountProcessor:
                        // if (nameElem != null)
                        //    objectName = nameElem.Value.Trim(); 
                    }
                }

                if (changed)
                    doc.Save(path);
            }
        }

        public AccountNode CloneAccountNode(AccountNode original, string objectName, Dictionary<string, Account> definitions)
        {
            string initials = string.Concat(objectName
                .Split(new[] { ' ', '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Length > 0 ? char.ToUpper(w[0]) : ' ') // Added w.Length check for safety
                .Where(c => c != ' ')); // Ensure no spaces if a word was empty or just an underscore

            // Ensure initials has a fallback if objectName was empty or produced no initials
            if (string.IsNullOrEmpty(initials))
            {
                initials = "XX"; // Or some other default
                Console.WriteLine($"Warning: Generated empty initials for objectName '{objectName}'. Using fallback '{initials}'.");
            }


            string defLine = definitions.TryGetValue(original.Account.Id, out var def) 
                ? def.RawLine.Trim() 
                : original.Account.RawLine.Trim();

            // Ensure defLine has at least 3 characters before Substring
            string suffix = "";
            if (defLine.Length >= 3)
            {
                suffix = defLine.Substring(3); // skip '+Cf' or similar prefix
            }
            else
            {
                // Handle case where defLine is too short, perhaps it's an already cloned ID or malformed
                // For now, let's assume it might be the original caption part or an ID without typical prefix
                suffix = defLine; // Use the whole defLine as suffix or consider a more robust parsing
                Console.WriteLine($"Warning: Definition line '{defLine}' for original account '{original.Account.Id}' is shorter than 3 characters. This might affect new ID generation.");
            }
            
            string newIdPart = $"Ac{initials}_{suffix.Split('$')[0].Trim()}";
            // Remove any characters that are not suitable for an AccountID if necessary
            // For example, if suffix contained spaces or other special characters before a '$'

            string rawLine = $"+{newIdPart}";
            if (suffix.Contains("$"))
            {
                rawLine += $"${suffix.Substring(suffix.IndexOf('$') + 1).Trim()}";
            }
            else if (!string.IsNullOrWhiteSpace(def?.Caption) && defLine == original.Account.RawLine.Trim())
            {
                // If we used original.Account.RawLine and it didn't have a caption,
                // but the definition (def) does, append the definition's caption.
                 rawLine += $"$ {def.Caption}";
            }
             else if (!string.IsNullOrWhiteSpace(original.Account.Caption))
            {
                 rawLine += $"$ {original.Account.Caption}";
            }
            // else, no caption to append


            string id = newIdPart; // ID is the part before any caption marker

            return new AccountNode(new Account
            {
                Id = id,
                Caption = ExtractCaptionFromRawLine(rawLine) ?? original.Account.Caption, // Use a helper to get caption
                RawLine = rawLine,
                Depth = original.Account.Depth // Cloned account initially has the same depth as its original
            });
        }

        private string ExtractCaptionFromRawLine(string rawLine)
        {
            var parts = rawLine.Split(new[] { '$' }, 2);
            if (parts.Length > 1)
            {
                return parts[1].Trim();
            }
            return null;
        }


        public void RecordCreatedAccount(string newRawLine)
        {
            if (!writtenIds.Contains(newRawLine))
            {
                File.AppendAllLines("createdAccounts.txt", new[] { newRawLine });
                writtenIds.Add(newRawLine);
            }
        }

        public void RecordUpdatedTreeRelationship(string parentRawLine, string childRawLine)
        {
            var lines = new[]
            {
                parentRawLine.Trim(),
                "   " + childRawLine.Trim() // Child is indented
            };
            File.AppendAllLines("updatedTree.txt", lines);
        }
    }
}