
# AccountTreeApp

AccountTreeApp is a C# command-line tool that:

- Parses account definitions and hierarchical structures from `.xxa` files
- Builds a tree based on a given account root
- Matches object usage in `.xmo` files
- Resolves duplicate usage by cloning accounts with unique identifiers
- Updates XML, logs all changes

## How to Run

```
dotnet run --project AccountTreeApp.csproj <AccountID> <AccountsDir> <DatabaseDir>
```

Example:

```
dotnet run --project AccountTreeApp.csproj AcFFRLInterestReceived "SampleData/Test COA" "_extracted"
```

Use F5 in VS Code to automatically extract `.zip` test data to `_extracted/`.

---

## Outputs

- **createdAccounts.txt**: newly generated account definitions (clones)
- **updatedTree.txt**: parent-child relationships (original → clone)
- **AccountsWithObjectOutput.txt**: matched `.xmo` files with object account references
- **.xmo XML files**: updated `<AccountID>` fields

---

## Folder Structure

- `/docs/` – specifications and reference examples
- `/SampleData/` – input `.xxa` and `.xmo` files for testing
- `createdAccounts.txt`, `updatedTree.txt` – output logs

---

## Developer Notes

- Clone logic derives from object name initials (e.g., "Project X Alpha" → `PXA`)
- Tree is built fully from all children recursively starting at the root
- Updated files are cleaned and rewritten automatically
