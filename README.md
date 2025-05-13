# AccountTreeApp

AccountTreeApp is a C# command-line tool that:
- Parses accounts from `.xxa` (hierarchical definitions)
- Scans `.xmo` objects for equations that output to accounts
- Clones shared accounts using per-object initials
- Detects and respects cash-flow triplicates (`Co+Ap+Cf`, `Rv+Ar+Cf`)
- Updates `<AccountID>` in `.xmo` files
- Outputs an updated account tree and log

---

## How It Works

### Account Definitions (`Accounts.xxa`)
- Each account starts with `+` or `-`
- Format: `[Type][AccountId][Modifiers] [Caption]`
- Only the **first definition** of each `[Type][AccountId]` is valid — others are ignored

### Conflict Rules
- Objects must not share output accounts
- When multiple objects output to the same `[Type][AccountId]`, it is cloned as:
+Ac<Initials>_<AccountId> [Modifiers] [Caption]

markdown
Copy
Edit

### Triplicate Rules
- Triplicate logic applies **only if all 3 parts exist**:
- `Co+Ap+Cf` → Cost triplicate
- `Rv+Ar+Cf` → Income triplicate
- Tree output includes only: `Cf`, `Co`, `Rv` as parents

---

## Usage

```bash
dotnet run --project AccountTreeApp.csproj <TreeFile> <AccountsDir> <DatabaseDir>
Example:

bash
Copy
Edit
dotnet run --project AccountTreeApp.csproj tree.txt AccountsDir _extracted
Outputs
createdAccounts.txt – all cloned accounts

updatedTree.txt – tree of original → clones

processing.log – log of all replacements and decisions

Updated .xmo files with <AccountID> rewritten

Test Data Format
Each .xmo file contains one <Object> with <Name> and many <AccountID> tags

All <AccountID>s use [Type][AccountId] (no modifiers)

Tree Structure Example
text
Copy
Edit
+CfInterestCollected$ Interest Collected
   +AcOBJ_InterestCollected$ Interest Collected
+RvInterestCollected$ Interest Collected
   +AcOBJ_InterestCollected$ Interest Collected
Triplicate Decision Logic
A triplicate is valid if:

All required [Type][AccountId] combinations exist as first definitions

If any required part is a duplicate, the triplicate is invalid

