using OPOService.Models;

namespace OPOService.GraphQL
{
    public partial class UserData
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Username { get; set; }
        public string PhoneNumber { get; set; }
    }
}
