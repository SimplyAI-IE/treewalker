
using System.Collections.Generic;

namespace AccountTreeApp.Models
{
    public class AccountNode
    {
        public Account Account { get; set; }
        public List<AccountNode> Children { get; set; } = new List<AccountNode>();
        public AccountNode? Parent { get; set; }

        public AccountNode(Account account)
        {
            Account = account;
        }

        public void AddChild(AccountNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }
    }
}
