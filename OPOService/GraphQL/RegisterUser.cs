namespace OPOService.GraphQL
{
    public record RegisterUser
    (
        string FullName,
        string Email,
        string Username,
        string Password,
        string PhoneNumber
    );
}
