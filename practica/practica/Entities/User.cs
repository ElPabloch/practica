using System.ComponentModel.DataAnnotations;

public class User
{
    public required string Id { get; set; }
    public required string Role { get; set; }
    public required string Email { get; set; }
    public required string NickName { get; set; }
    public required string Password { get; set; }
    public int PositiveFeedback { get; set; }
    public int NegativeFeedback { get; set; }
}
