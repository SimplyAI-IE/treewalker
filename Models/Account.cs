namespace AccountTreeApp.Models
{
    public class Account
    {
        public string Id { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;
        public int Depth { get; set; }

        public string Direction { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public string Modifiers { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
    }
}
