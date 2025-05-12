
using AccountTreeApp.Services;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.WriteLine("Usage: AccountTreeApp <AccountID> <AccountsDir> <DatabaseDir>");
            return;
        }

        string accountId = args[0];
        string accountsDir = args[1];
        string databaseDir = args[2];

        var app = new AccountProcessor();
        app.Run(accountId, accountsDir, databaseDir);
    }
}
