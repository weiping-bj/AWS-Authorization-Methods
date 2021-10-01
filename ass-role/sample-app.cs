using System;
using System.Threading.Tasks;
using System.Threading;

using Amazon.S3;
using Amazon.S3.Model;
using aws.credentials;
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;
using auth.tokens;
using Amazon.Runtime;

namespace S3CreateAndList
{
    class Program
    {
        // Main method
        static async Task Main(string[] args)
        {



            var AWSCrentialsManager = new AWSCrentialsManager(new AuthTokenValidator(), new AuthPolicyGenerater(), "arn:aws-cn:iam::<ACCOUNT_ID>:role/<ROLE_NAME>", "passwordXDCS");
            var stsresponse = await AWSCrentialsManager.AssumeRole(new UserToken());



            AmazonS3Client amazonS3Client = new AmazonS3Client(new SessionAWSCredentials(stsresponse.Credentials.AccessKeyId, stsresponse.Credentials.SecretAccessKey, stsresponse.Credentials.SessionToken));

            var listResponse = await amazonS3Client.ListObjectsAsync("renyzbucket");

            Console.WriteLine($"Number of buckets: {listResponse.S3Objects.Count}");
            foreach (S3Object b in listResponse.S3Objects)
            {
                Console.WriteLine(b.BucketName);
            }
            listResponse = await amazonS3Client.ListObjectsAsync("renyz-ml-model");

            Console.WriteLine($"Number of buckets: {listResponse.S3Objects.Count}");
            foreach (S3Object b in listResponse.S3Objects)
            {
                Console.WriteLine(b.BucketName);
            }
        }
    }
}
