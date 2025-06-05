## Overview

AWS Certificate Manager is a service that lets you easily provision, manage, and deploy public and private Secure Sockets Layer/Transport Layer Security (SSL/TLS)
certificates for use with AWS services and your internal connected resources.
SSL/TLS certificates are used to secure network communications and establish the identity of websites over the Internet as well as resources on private networks.
AWS Certificate Manager removes the time-consuming manual process of purchasing, uploading, and renewing SSL/TLS certificates.
The orchestrator supports OAuth OIDC authentication, as well as AWS IAM accounts, and various options provided by the AWS SDK such as EC2 instance credentials.
The OAuth OIDC support allows authentication against a 3rd party identity provider in AWS.
After initial authentication, temporary credentials are used by using the Assume Role functionality in AWS.

This integration also supports the reading of existing certificate ACM key/value pair tags during inventory and adding these tags when adding new certificates.
Modifying and adding ACM tags during certificate renewal, however, is NOT supported.
This is due to the fact that the AWS API does not allow for ACM tag modification when updating a certificate in one step.
This would need to be done in multiple steps, leading to the possibility of the certificate being left in an error state if any intermediate step were to fail.
However, while the modification/addition of ACM tags is not supported, all existing ACM tags WILL remain in place during renewal.
 
### Documentation

- [How AWS works in this extension (aws-auth-library)](https://github.com/Keyfactor/aws-auth-library)
- [AWS Region Codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)

## Requirements

### Migrate existing ACM stores to the new type (AWS Certificate Manager v3)

Field usage has changed in v3, notably:
* `ServerUsername` and `ServerPassword` are no longer used
  * Specific fields for IAM and OAuth are defined for credentials of those type
* `Store Path` only allows a __single__ AWS Region to be defined
  * The Entry Parameter for AWS Region is no longer used
* `Client Machine` requires the _full_ Role ARN to be used for Assume Role calls

As a result, previous Store Types are no longer supported, and Certificate Stores of those types need to be migrated to the v3 type.
Inventory jobs will need be to run after creating the new Certificate Stores to begin tracking those certificates again.
The deprecated Stores and Store Types can be deleted after they are no longer needed.

_Currently there is no provided migration utility to perform this programatically._

### Setting up AWS Authentication (Examples)

The following examples show potential configurations for Roles in AWS with different selected authentication methods.
Your configuration steps may differ depending on specific requirements of your use case.

> ![NOTE]
> Several different options are offered for authenticating with AWS.
> Documentation for how these options work is now located in the [aws-auth-library](https://github.com/Keyfactor/aws-auth-library) repository.

<details>
<summary>EC2 instance credentials using Default SDK and Assume Role</summary>

Select the `Use Default SDK Auth` option to allow the integration to load EC2 instance credentials.
If the EC2 Role assigned to the instance is intended as the Destination account identity to use with ACM, no additional Role needs to be configured.

If the EC2 Role assigned to the instance is only to be used initially, and a new Role ARN is designated as the Destination account in the `Client Machine` field,
then the `Assume new Role using Default SDK Auth` should also be selected.

### AWS Setup
_Note: In this scenario the AWS-ACM-v3 extension needs to be running inside of an EC2 instance._
1. Assign or note the existing IAM Role assigned to the EC2 instance running. [Found in EC2 here](docsource/images/ec2-instance-iam-role.gif).
2. Ensure a [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role. [Example](docsource/images/ec2-role-arn-trust-relationship.gif).
3. Verify the permissions match the requirements for accessing ACM.

</details>


<details>
<summary>OAuth OIDC Identity Provider (Okta example)</summary>

Select the `Use OAuth` option for a certificate store to use an OAuth Identity Provider.

### AWS Setup
1. A 3rd party [Identity Provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to [this](docsource/images/AWSIdentityProvider.gif) needs to be setup in AWS.
2. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) needs to be created to be used with your Identity Provider.
3. Ensure the [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role with the Identity Provider. [Example](docsource/images/AWSRole2.gif).
4. Verify the permissions match the requirements for accessing ACM.

### OKTA Setup
1. Ensure your Authorization Server Is Setup in OKTA.  Here is a [sample](docsource/images/OktaSampleAuthorizationServer.gif).
2. Ensure the appropriate scopes are setup in Okta.  Here is a [sample](docsource/images/OktaSampleAuthorizationServer-scopes.gif).
3. Setup an Okta App with similar settings to [this](docsource/images/OktaApp1.gif) and [this](docsource/images/OktaApp2.gif).

</details>


<details>
<summary>IAM User credentials to Assume Role</summary>

Select the `Use IAM` option for a certificate store to use an IAM User credential.

### AWS Setup
1. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) to Assume with your IAM User needs to be created.
2. Ensure a [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role. [Example](docsource/images/AssumeRoleTrust.gif).
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a [standard AWS IAM User](docsource/images/UserAccount.gif) with an Access Key credential type.
4. Verify the permissions match the requirements for accessing ACM.

</details>
