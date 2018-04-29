using System;
using Microsoft.AspNetCore.Identity;

namespace DotNetCoreAuthorizationServer.Data
{
    public class AppIdentityRole : IdentityRole<Guid>
    {
        public AppIdentityRole()
        {
            Id = Guid.NewGuid();
        }
        public AppIdentityRole(string name) : this() { Name = name; }
    }
}
