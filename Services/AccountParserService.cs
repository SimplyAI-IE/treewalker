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
                if (string.IsNullOrWhiteSpace(line) || !(line.StartsWith("+") || line.StartsWith("-")))
                    continue;

                string trimmed = line.Trim();
                string direction = trimmed.Substring(0, 1);           // + or -
                string afterSign = trimmed.Substring(1);

                // Locate where the first space (caption) starts
                int captionStart = afterSign.IndexOf(' ');
                string definitionPart = captionStart > -1 ? afterSign.Substring(0, captionStart) : afterSign;
                string caption = captionStart > -1 ? afterSign.Substring(captionStart).Trim() : "";

                // Extract type (first 2 chars), rest is ID+modifiers
                string accountType = definitionPart.Substring(0, 2);
                string idPlusMods = definitionPart.Substring(2);

                // Extract modifiers (all characters from the first modifier symbol onward)
                int modStart = idPlusMods.IndexOfAny(new[] { '$', '%', '#' });
                string accountId = modStart >= 0 ? idPlusMods.Substring(0, modStart) : idPlusMods;
                string modifiers = modStart >= 0 ? idPlusMods.Substring(modStart) : "";

                var account = new Account
                {
                    Id = definitionPart,
                    Caption = caption,
                    RawLine = trimmed,
                    Direction = direction,
                    AccountType = accountType,
                    AccountId = accountId,
                    Modifiers = modifiers
                };

                accounts[account.Id.ToLower()] = account;
            }

            return accounts;
        }
    }
}
