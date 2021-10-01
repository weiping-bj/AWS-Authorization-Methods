namespace aws.credentials
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Amazon.Runtime.Internal;
    using Amazon.SecurityToken;
    using Amazon.SecurityToken.Model;
    public class UserToken
    {
        public string token
        {
            get; set;
        }
    }
    public class UserInfo
    {
        public string Username { get; set; }
        public string TenantId { get; set; }

    }
    public interface PolicyGenerator
    {
        public String gen(UserInfo userInfo);
    }
    public interface TokenValidator
    {
        UserInfo userInfo(UserToken token);

    }
    public class UserTokenException : Exception
    {
        public UserTokenException(String message) : base(message)
        {

        }

    }

    public class AWSCrentialsManager
    {
        private TokenValidator tokenValidator;
        public AmazonSecurityTokenServiceClient stsClient { get; set; }
        public string ExternalId { get; set; }
        public PolicyGenerator policyGenerator { get; set; }
        public string RoleArn { get; set; }
        public int DurationSeconds { get; set; }
        public AWSCrentialsManager(TokenValidator tokenValidator, PolicyGenerator policyGenerator, String RoleArn, string ExternalId)
        {
            stsClient = new AmazonSecurityTokenServiceClient();
            this.tokenValidator = tokenValidator;
            this.policyGenerator = policyGenerator;
            this.DurationSeconds = 900;
            this.RoleArn = RoleArn;
            this.ExternalId = ExternalId;

        }
        public async Task<AssumeRoleResponse> AssumeRole(UserToken token)
        {
            UserInfo userInfo = tokenValidator.userInfo(token);
            if (userInfo == null)
            {
                throw new UserTokenException("user token validate fail");
            }
            AssumeRoleRequest request = new AssumeRoleRequest();
            request.RoleArn = RoleArn;
            request.DurationSeconds = DurationSeconds;
            request.ExternalId = ExternalId;
            request.RoleSessionName = userInfo.TenantId + "--" + userInfo.Username;
            request.Tags = new List<Tag> { new Tag() { Key = "tenantid", Value = userInfo.TenantId }, new Tag() { Key = "username", Value = userInfo.Username } };
            request.Policy = policyGenerator.gen(userInfo);
            return await stsClient.AssumeRoleAsync(request);
        }

    }
}