
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

        public void UpdateXmlFiles(List<string> xmlPaths, string originalId, string newId, out string objectName)
        {
            objectName = "";
            foreach (var path in xmlPaths)
            {
                var doc = XDocument.Load(path);
                var changed = false;

                foreach (var obj in doc.Descendants("Object"))
                {
                    var accElem = obj.Element("AccountID");
                    var nameElem = obj.Element("Name");

                    if (accElem != null && accElem.Value.Trim().Equals(originalId, StringComparison.OrdinalIgnoreCase))
                    {
                        accElem.Value = newId;
                        changed = true;
                        if (nameElem != null)
                            objectName = nameElem.Value.Trim();
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
                .Select(w => char.ToUpper(w[0])));

            string defLine = definitions.TryGetValue(original.Account.Id, out var def) 
                ? def.RawLine.Trim() 
                : original.Account.RawLine.Trim();  // fallback if definition is missing

            string suffix = defLine.Substring(3);  // skip '+Cf'
            string rawLine = $"+Ac{initials}_{suffix}";
            string id = rawLine.Substring(1).Split('$')[0].Trim();

            return new AccountNode(new Account
            {
                Id = id,
                Caption = def?.Caption ?? original.Account.Caption,
                RawLine = rawLine,
                Depth = original.Account.Depth
            });
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
                "   " + childRawLine.Trim()
            };
            File.AppendAllLines("updatedTree.txt", lines);
        }
    }
}
