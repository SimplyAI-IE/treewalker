
// C:\Users\Jason Cooke\Desktop\AccountTreeApp\AccountTreeApp\Services\ObjectMatcherService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace AccountTreeApp.Services
{
    public class ObjectMatcherService
    {
        public Dictionary<string, List<string>> MatchObjectsByAccountId(string databaseDir)
        {
            var accountToObjects = new Dictionary<string, List<string>>();

            foreach (var file in Directory.GetFiles(databaseDir, "*.xmo"))
            {
                var doc = XDocument.Load(file);
                var objects = doc.Descendants("Object");

                foreach (var obj in objects)
                {
                    var accountIdElement = obj.Element("AccountID");
                    if (accountIdElement == null) continue;

                    string accountId = accountIdElement.Value.Trim();
                    accountId = accountId.ToLower();
                    if (!accountToObjects.ContainsKey(accountId))
                        accountToObjects[accountId] = new List<string>();

                    accountToObjects[accountId.ToLower()].Add(file);
                }
            }

            return accountToObjects;
        }
    }
}
