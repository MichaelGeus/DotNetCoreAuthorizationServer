using System;
using Microsoft.AspNetCore.Identity;

namespace DotNetCoreAuthorizationServer.Data
{
    public class AppIdentityUser : IdentityUser<Guid>
    {
        public AppIdentityUser()
        {
            Id = Guid.NewGuid();
        }

        public AppIdentityUser(string userName) : this() { UserName = userName; }

        public string FirstName { get; set; }

        public string LastName { get; set; }
    }
}
