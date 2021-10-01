using System.IO;
using aws.credentials;

namespace auth.tokens
{
    public class AuthTokenValidator : TokenValidator
    {
        public UserInfo userInfo(UserToken token)
        {
            return new UserInfo() { TenantId = "t001", Username = "authuser02" };
        }
    }
    public class AuthPolicyGenerater : PolicyGenerator
    {
        public string gen(UserInfo userInfo)
        {
            return File.ReadAllText("sessionpolicy.json");
        }
    }

}
