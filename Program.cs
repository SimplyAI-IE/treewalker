
using AccountTreeApp.Services;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: AccountTreeApp <TreePath> <AccountsPath> <DatabasePath>");
            return;
        }

        string treePath = args[0];
        string accountsPath = args[1];
        string databasePath = args[2];

        var processor = new AccountProcessor();
        processor.Run(treePath, accountsPath, databasePath);
    }
}
