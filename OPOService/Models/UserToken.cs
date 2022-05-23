namespace OPOService.Models
{
    public record UserToken
     (
         string? Token,
         string? Expired,
         string? Message
     );
}
