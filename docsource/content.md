## Overview

AWS Certificate Manager is a service that lets you easily provision, manage, and deploy public and private Secure Sockets Layer/Transport Layer Security (SSL/TLS) certificates for use with AWS services and your internal connected resources. SSL/TLS certificates are used to secure network communications and establish the identity of websites over the Internet as well as resources on private networks. AWS Certificate Manager removes the time-consuming manual process of purchasing, uploading, and renewing SSL/TLS certificates.  The orchestrator supports Okta OAth authentication, as well as AWS IAM accounts. The Okta Support allows authentication against a 3rd party identity provider in AWS.  From there you can get temporary credentials for a role that you setup in each AWS Account.
 
### Documentation

- [Cert Manager API](https://docs.aws.amazon.com/acm/latest/userguide/sdk.html)
- [Aws Region Codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)


## Requirements

Depending on your choice of authentication providers, choose the appropriate section:
<details>
<summary>AWS Certificate Manager <code>AWS-ACM</code></summary>

### AWS Setup
Options for authenticating:
1. Okta or other OAuth configuration (refer to `AwsCerManO` below)
2. IAM User Auth configuration (refer to `AwsCerManA` below)
3. EC2 Role Auth or other default method supported by the [AWS SDK](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html)

As one option for #3, to set up Role Auth for an EC2 instance, follow the steps below. Note, this applies specifically __when the orchestrator is running `ACM-AWS` inside of an EC2 instance__. When the option to assume an EC2 role is selected, the Account ID and Role will be assumed using the default credentials supplied in the EC2 instance via the AWS SDK.
1. Assign or note the existing IAM Role assigned to the EC2 instance running
2. Make sure that role has access to ACM
3. When configuring the `AWS-ACM` store, do not select either IAM or OAuth methods in the store's settings. This will make it use the AWS SDK to lookup EC2 credentials.

</details>

<details>
<summary>[Deprecated] AWS Certificate Manager with Okta Auth Configuration <code>AwsCerManO</code></summary>

### AWS Setup
1. A 3rd party [identity provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to [this](docsource/images/AWSIdentityProvider.gif) needs to be setup in AWS for each account.
2. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) similar to [this](docsource/images/AWSRole1.gif) needs Added for each AWS account.
3. Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like [this](docsource/images/AWSRole2.gif).

### OKTA Setup
1. Ensure your Authorization Server Is Setup in OKTA.  Here is a [sample](docsource/images/OktaSampleAuthorizationServer.gif).
2. Ensure the appropriate scopes are setup in Okta.  Here is a [sample](docsource/images/OktaSampleAuthorizationServer-scopes.gif).
3. Setup an Okta App with similar settings to [this](docsource/images/OktaApp1.gif) and [this](docsource/images/OktaApp2.gif).

</details>

<details>
<summary>[Deprecated] AWS Certificate Manager with IAM Auth Configuration <code>AwsCerManA</code></summary>

### AWS Setup
1. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for the permissions you want to grant, see [sample](docsource/images/AWSRole1.gif).
2. A [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should look like something like [this](docsource/images/AssumeRoleTrust.gif).
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a [standard AWS IAM User](docsource/images/UserAccount.gif) with an Access Key credential type.

</details>

