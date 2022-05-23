namespace OPOService.GraphQL
{
    public record TransferInput
    (
        string Username,
        string PhoneNumber,
        string Amount
    );
}
