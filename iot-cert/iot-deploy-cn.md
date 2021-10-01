## Authorization-based-on-IoT-Cert
本章节将介绍利用 AWS IoT 证书实现访问授权的部署过程，可以从 [这里](iot-cert-cn.md) 查看方案的基本原理介绍。

### 方案部署
该方法将模拟一名最终用户，以 IoT 证书作为自己的用户凭证，完成验证后可以访问 Amazon S3 存储桶中以自己名字命名的目录（读写权限），但无法对其它目录拥有写权限。在 AWS 全球各个区域（含中国大陆地区的两个区域）均可以使用本方法进行部署。

####创建用户载体####

根据本文“概念对应关系”章节中的内容，首先需要在 AWS IoT 服务中创建出“公司”、“最终用户”、“用户授权组”的对应的载体，即：ThingType、Thing 和 ThingGroup，以及用户用来作为自身凭据的 x.509 证书。

**1. 创建 ThingType（公司）**

```
aws iot create-thing-type \

--thing-type-name "AWS" \

--thing-type-properties "thingTypeDescription=testing, searchableAttributes=login,emailAddress,assumeRole"
```

**2. 创建 Thing（最终用户）**

```
aws iot create-thing --cli-input-json file://user-liuwp001-s3.json
```

其中，文件 user-liuwp001-s3.json 用于描述 Thing 的相关信息，示例如下：

```json
{
  “thingName”: “lwp001-s3”,
  “thingTypeName”: “AWS”,
  "attributes": {
    "JobRole": "PSA",
    "Company": "AWS",
    "FamilyName": "L",
"GivenName": "WP",
"emailAddress": "xxxxxxxx",
    "assumeRole": "S3User"
  }
}
```

其中的 thingName 就是用户在应用系统中的用户名，也是后续 S3 存储桶中用户目录的目录名。而属性信息其实就是用户自身的注册信息，它们将会以 Thing 属性的形式被保留在 AWS IoT 服务中。

**3. 创建 ThingGroup（授权组）**

```
aws iot create-thing-group --cli-input-json file://s3-access.json
```

其中，文件 s3-access.json 用于描述该 ThingGroup 的相关信息，示例如下：

```json
{
    "thingGroupName": "s3-access",
    "thingGroupProperties": {
        "thingGroupDescription": "This group is for s3 access based on different user.",
        "attributePayload": {
            "attributes": {
                "securityLevel": "High"
            },
            "merge": true
        }
    }
}
```

**4. 为 Thing 创建证书**

执行以下命令时，THING_NAME 、证书保存的目录都可以根据自己的需要进行设定。

```
THING_NAME=user-lwp001-s3

aws iot create-keys-and-certificate --set-as-active \
    --public-key-outfile ~/iot-cert/$THING_NAME/$THING_NAME.public.key \
    --private-key-outfile ~/iot-cert/$THING_NAME/$THING_NAME.private.key \
    --certificate-pem-outfile ~/iot-cert/$THING_NAME/$THING_NAME.certificate.pem
```

上述命令成功执行后会返回所生成证书的 ARN，执行以下命令将证书关联到之前创建的 Thing 上：

```
aws iot attach-thing-principal --thing-name $THING_NAME --principal <YOUR_CERT_ARN>
```

生成的证书文件需要交付到最终用户手中。

####授权设置####

根据前文“权限设计”中的介绍，本方案中有两处权限需要设置：AWS IAM Role 和 AWS IoT。

**1. 在 AWS IAM 中进行权限设置：** 

此处创建的 IAM Role 需要被 AWS IoT 的 credentials 服务所调用。因此，这个 Role 的信任实体应当设置为 credentials.iot.amazonaws.com。执行以下命令创建 IAM Role，请注意将 \<YOUR\_ROLE\_NAME> 根据需要进行替换。

```
aws iam create-role --role-name <YOUR_ROLE_NAME> --assume-role-policy-document file://trustpolicy.json
```

其中 trustpolicy.json 的内容示例如下：

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "Service": "credentials.iot.amazonaws.com"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

Role 创建出来后需要和 IAM 策略相关联，以便授予对应权限。本文中最终用户通过 IoT 证书所获得的权限是访问专门的 S3 存储桶（只读），仅对以自己名字命名的目录拥有读写权限。通过以下方式创建用于描述 IAM 策略的 json 文件，在 IAM 策略中接受 credentials-iot:ThingName 变量。

使用过程中注意将 \<YOUR\_BUCKET> 和 \<BUCKET\_PREFIX> 根据需要进行替换。

```
cat >s3-access-policy.json <<eof
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Effect": "Allow",
            "Action": "s3:ListBucket",
            "Resource": "arn:aws-cn:s3:::<YOUR_BUCKET>",
            "Condition": {
                "StringLike": {
                    "s3:prefix": [
                        "<BUCKET_PREFIX>/${credentials-iot:ThingName}/*",
                        "<BUCKET_PREFIX>/${credentials-iot:ThingName}/"
                    ]
                }
            }
        },
        {
            "Effect": "Allow",
            "Action": "s3:GetObject",
            "Resource": "arn:aws-cn:s3::: <YOUR_BUCKET>/*"
        },
        {
            "Effect": "Allow",
            "Action": "s3:*",
            "Resource": [
                "arn:aws-cn:s3::: <YOUR_BUCKET>/<BUCKET_PREFIX>/${credentials-iot:ThingName}",
                "arn:aws-cn:s3::: <YOUR_BUCKET>/<BUCKET_PREFIX>/${credentials-iot:ThingName}/*"
            ]
        }
    ]
}
eof
```

基于该内容创建 IAM 策略，并关联到之前创建的 IAM Role 上：

```
# 创建 IAM Policy，并记录结果中的 ARN
aws iam create-policy --policy-name sobey-cctv-reporter --policy-document file://s3-access-policy.json

aws iam attach-role-policy --role-name <ROLE_NAME> --policy-arn <POLICY_ARN>
```

**2. 在 AWS IoT 中进行权限设置：**

AWS IoT 不能直接使用 IAM Role，需要先在 AWS IoT 服务中为这个 IAM Role 创建别名：

```
aws iot create-role-alias --role-alias <ROLE_ALIAS> --role-arn <IAM_ROLE_ARN> --credential-duration-seconds 3600
```

\<ROLE\_ALIAS>可根据需要自行定义，\<IAM\_ROLE\_ARN> 为之前所创建的 IAM Role 的 ARN。

接下来，需要为 IoT 的证书关联一个 IoT 策略，以允许该证书通过刚刚创建的别名使用 IAM Role。创建策略命令如下：

```
aws iot create-policy --policy-name <Policy_NAME> --policy-document file:// <Policy_NAME>.json
 ```
 
 其中 <Policy_NAME>.json 内容如下：
 
 ```json
 {
  "Version": "2012-10-17",
  "Statement": {
    "Effect": "Allow",
    "Action": "iot:AssumeRoleWithCertificate",
    "Resource": "<ALIAS_ARN>",
    "Condition": {
      "StringEquals": {
        "iot:Connection.Thing.Attributes[assumeRole]": "S3User",
        "iot:Connection.Thing.ThingTypeName": "AWS"
      },
      "Bool": {
        "iot:Connection.Thing.IsAttached": "true"
      }
    }
  }
}
```

策略中增加了 Condition 字段，以减少发生用户元数据错填、加错 ThingGroup 时错误的获取了相关权限的情况。

将创建好的 IoT 策略关联给之前创建的 ThingGroup，这样加入到 ThingGroup 中的所有 Thing 都会获得调用 IAM Role 的权限（被调用的 Role 通过 IoT 的角色别名进行了限制）：

```
aws iot attach-policy --policy-name <POLICY_NAME> --target <THINGGROUP_ARN>
```

接下来只要将之前创建的 Thing 加入到这个 ThingGroup 中即可：

```
aws iot add-thing-to-thing-group --thing-name <YOUR_THING_NAME> --thing-group-name <GROUP_NAME>
```

####验证####
最终用户在获得 IoT 证书后，可以通过该证书访问 AWS IoT Credentials Provider，通过 Credentials Provider 访问 AWS STS 服务以换取临时 credentials。

首先需要获得自己的 AWS 账号内 IoT Credentials Provider 的 endpoint：

```
aws iot describe-endpoint --endpoint-type iot:CredentialProvider
```

返回结果的格式应为：xxxxxxxxx.credentials.iot.cn.<REGION>.amazonaws.com.cn

通过标准的 curl 命令即可访问上述 endpoint，命令中需包含 Thing 证书、Thing 私钥、用于验证 AWS IoT Core 服务器身份的根 CA 证书。根 CA 证书的获取可参考 [官方文档](https://docs.amazonaws.cn/en_us/iot/latest/developerguide/server-authentication.html#server-authentication-certs)。

请求临时 credentials 命令如下：

```
curl –cert <THING_CERT> --key <THING_PRIVATE_KEY> -H "x-amzn-iot-thingname: <THING_NAME>" –cacert <AWS_ROOT_CA> https://<CREDENTIALS_ENDPOINT>/role-aliases/<ALIAS_NAME>/credentials > temptoken
```

在 temptoken 文件中可以找到所需的 access key id, secret access key。如安装了 jq 工具，也可通过如下方式设置环境变量：

```
export AWS_ACCESS_KEY_ID=$(jq -r ".credentials.accessKeyId" temptoken)

export AWS_SECRET_ACCESS_KEY=$(jq -r ".credentials.secretAccessKey" temptoken)

export AWS_SESSION_TOKEN=$(jq -r ".credentials.sessionToken" temptoken)
```
 
设置完成后，即可通过 awscli 或 SDK 工具操作 s3 存储桶中的对应目录。

####检索####
实际应用过程中可能存在大量用户（Thing）的情况，为了快速查找相关用户，可以利用 AWS IoT 提供的检索功能。

激活 Fleet Indexing 功能：

```
aws iot update-indexing-configuration \

--thing-indexing-configuration thingIndexingMode=REGISTRY_AND_SHADOW
```
 
根据需要检索，例如查找 Company 为 AWS、assumeRole 为 S3User 的用户信息（即 Thing 信息）：

```
aws iot search-index \

--query-string "attributes.Company:AWS AND attributes.assumeRole:S3User"
```

[【查看 方案原理】](iot-cert-cn.md) 

[【返回 README】](../README.md)