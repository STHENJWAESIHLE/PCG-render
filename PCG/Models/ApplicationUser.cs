using Microsoft.AspNetCore.Identity;

namespace PCG.Models;

public class ApplicationUser : IdentityUser
{
    public string? DisplayName { get; set; }
}
