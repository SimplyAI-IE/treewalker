namespace AccountTreeApp.Models
{
    public class Account
    {
        public string Id { get; set; } = string.Empty;
        public string Caption { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;
        public int Depth { get; set; }
    }
}
