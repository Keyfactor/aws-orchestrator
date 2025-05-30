## **Configuration**

**Overview**

AWS Certificate Manager is a service that lets you easily provision, manage, and deploy public and private Secure Sockets Layer/Transport Layer Security (SSL/TLS) certificates for use with AWS services and your internal connected resources. SSL/TLS certificates are used to secure network communications and establish the identity of websites over the Internet as well as resources on private networks. AWS Certificate Manager removes the time-consuming manual process of purchasing, uploading, and renewing SSL/TLS certificates.  The orchestrator supports Okta OAth authentication, as well as AWS IAM accounts. The Okta Support allows authentication against a 3rd party identity provider in AWS.  From there you can get temporary credentials for a role that you setup in each AWS Account. 

### Documentation

- [Cert Manager API](https://docs.aws.amazon.com/acm/latest/userguide/sdk.html)
- [Aws Region Codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)

### Supported Functionality
- Add/Delete/Replace Root Certificates
- Add/Delete/Replace Certificates with Public and Private Keys
- Inventory Root Certificates
- Inventory Certificates with Public and Private Keys

### Not Implemented/Supported
- Reenrollment, Management, Discovery

## **Installation**
Depending on your choice of authentication providers, choose the appropriate configuration section
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

<details>
<summary><code>AWS-ACM</code> Cert Store Type and Cert Store Setup</summary>

Cert Store Type Settings
===============
**Basic Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Name | AWS Certificate Manager | Display name for the store type (may be customized)
Short Name| AWS-ACM | Short display name for the store type
Custom Capability | N/A | Store type name orchestrator will register with. Check the box to allow entry of value
Supported Job Types | Inventory, Add, Remove | Job types the extension supports
Needs Server | Checked | Determines if a target server name is required when creating store
Blueprint Allowed | Checked | Determines if store type may be included in an Orchestrator blueprint
Uses PowerShell | Unchecked | Determines if underlying implementation is PowerShell
Requires Store Password	| Unchecked | Determines if a store password is required when configuring an individual store.
Supports Entry Password	| Unchecked | Determines if an individual entry within a store can have a password.


**Advanced Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Store Path Type	| Freeform | Determines what restrictions are applied to the store path field when configuring a new store.
Store Path Value | N/A | This is reserved for the AWS Account Id when setting up the store.
Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias.
Private Keys | Required | This determines if Keyfactor can send the private key associated with a certificate to the store.
PFX Password Style | Default or Custom | "Default" - PFX password is randomly generated, "Custom" - PFX password may be specified when the enrollment job is created (Requires the *Allow Custom Password* application setting to be enabled.)

**Custom Fields:**

Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote
target server containing the certificate store to be managed

Name|Display Name|Type|Default Value|Depends On|Required|Description
---|---|---|---|---|---|---
UseEC2AssumeRole | Assume new Account / Role in EC2 | boolean | False | N/A | Yes | A switch to enable the store to assume a new Account ID and Role when using EC2 credentials
UseOAuth | Use OAuth 2.0 Provider | boolean | False | N/A | Yes | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM
UseIAM | Use IAM User Auth | boolean | False | N/A | Yes | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM
EC2AssumeRole | AWS Role to Assume (EC2) | string | N/A | UseEC2AssumeRole | The AWS Role to assume using the EC2 instance credentials
OAuthScope | OAuth Scope | string | N/A | Use OAuth 2.0 Provider | No | This is the OAuth Scope needed for Okta OAuth, defined in Okta
OAuthGrantType | OAuth Grant Type | string | client_credentials | Use OAuth 2.0 Provider | No | In OAuth 2.0, the term “grant type” refers to the way an application gets an access token. In Okta this is `client_credentials`
OAuthUrl | OAuth URL | string | https://***/oauth2/default/v1/token | Use OAuth 2.0 Provider | No | The URL to request a token from your OAuth Provider. Fill this out with the correct URL.
OAuthAssumeRole | AWS Role to Assume (OAuth) | string | N/A | Use OAuth 2.0 Provider | No | The AWS Role to assume after getting an OAuth token.
IAMAssumeRole | AWS Role to Assume (IAM) | string | N/A | Use IAM User Auth | No | The AWS Role to assume as the IAM User.
ExternalId | sts:ExternalId | string | N/A | N/A | No | An optional parameter sts:ExternalId to pass with Assume Role calls


**Entry Parameters:**

Entry parameters are inventoried and maintained for each entry within a certificate store.
They are typically used to support binding of a certificate to a resource.

While `AWS Region` can be set to multiple choice as noted below, you will need to list all regions you want available for adding certificates.
You can instead make this a String type in order to allow the region to be specified later without knowing all valid regions now.

Name|Display Name| Type|Default Value|Required When|Description
---|---|---|---|---|---
AWS Region | AWS Region | Multiple Choice | us-east-1 | Adding | When adding, this is the Region that the Certificate will be added to.



Cert Store Settings
===============
| Name | Value | Description |
| ----------- | ----------- | ----------- |
| Client Machine | AWS Account ID | This is the AWS Account ID that will be used for access. This will dictate what certificates are usable by the orchestrator. Note: this does not have any effect on EC2 inferred credentials, which are limited to a specific role/account. |
| User Name | See Below | See Below |
| Password | See Below | See Below |
| Store Path | us-east-1,us-east-2,...,etc. | The AWS Region, or a comma-separated list of multiple regions, the store will operate in. |
| Assume new Account / Role in EC2 | Use the EC2 credentials to make an Assume Role request to change Role and Account ID in use | Set to true to use EC2 credentials and assume a different role than the default EC2 role. |
| Use OAuth 2.0 Provider | Use an OAuth provider to authenticate with AWS | Set to true to enable OAuth usage and display additional OAuth fields |
| Use IAM User Auth | Use an IAM user's credentials to assume a role | Set to true to enable IAM user auth and the IAM Account ID field. |
| AWS Role to Assume (EC2) | AWS Role | Displayed and required when using Assume new Account / Role in EC2. This Role is assumed using the EC2 default credentials. |
| OAuth Scope | Look in OAuth provider for Scope | Displayed and required when using OAuth 2.0 Provider. OAuth scope setup in the Okta Application or other OAuth provider |
| OAuth Grant Type | client_credentials | Displayed and required when using OAuth 2.0 Provider. This may vary depending on Okta setup but will most likely be this value. |
| OAuth URL | https://***/oauth2/default/v1/token | Displayed and required when using OAuth 2.0 Provider. URL to request token from OAuth provider. Example given is for an Okta token. |
| AWS Role to Assume (OAuth) | AWS Role | Displayed and required when using OAuth 2.0 Provider. This Role is assumed after getting an OAuth token. |
| AWS Role to Assume (IAM) | AWS Role | Displayed and required when using IAM User Auth. This Role is assumed with the IAM credentials. |
| sts:ExternalId | Configured AWS External ID | An optional field to enter an External ID value set in AWS, which may be required to make Assume Role calls. |

The User Name and Password fields are used differently based on the auth method you intend to use. The three options for auth are IAM User, OAuth, or default auth.

| Auth Method | Field | Value |
| - | - | - |
| IAM User | User Name | Set to the IAM User's AWS `Access Key` |
| IAM User | Password | Set to the IAM User's AWS `Access Secret` |
| OAuth 2.0 | User Name | Set to the OAuth `Client ID` |
| OAuth 2.0 | Password | Set to the OAuth `Client Secret` |
| Default EC2 (SDK) | User Name | No Value |
| Default EC2 (SDK) | Password | No Value |

</details>
</details>

<details>
<summary>[Deprecated] AWS Certificate Manager with Okta Auth Configuration <code>AwsCerManO</code></summary>

### AWS Setup
1. A 3rd party [identity provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to [this](/Images/AWSIdentityProvider.gif) needs to be setup in AWS for each account.
2. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) similar to [this](/Images/AWSRole1.gif) needs Added for each AWS account.
3. Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like [this](/Images/AWSRole2.gif).

### OKTA Setup
1. Ensure your Authorization Server Is Setup in OKTA.  Here is a [sample](/Images/OktaSampleAuthorizationServer.gif).
2. Ensure the appropriate scopes are setup in Okta.  Here is a [sample](/Images/OktaSampleAuthorizationServer-scopes.gif).
3. Setup an Okta App with similar settings to [this](/Images/OktaApp1.gif) and [this](/Images/OktaApp2.gif).


<details>
<summary><code>AwsCerManO</code> Cert Store Type and Cert Store Setup</summary>

Cert Store Type Settings
===============
**Basic Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Name | Any Custom Name | Display name for the store type (may be customized)
Short Name| AWSCerManO | Short display name for the store type
Custom Capability | N/A | Store type name orchestrator will register with. Check the box to allow entry of value
Supported Job Types | Inventory, Add, Remove | Job types the extension supports
Needs Server | Checked | Determines if a target server name is required when creating store
Blueprint Allowed | Checked | Determines if store type may be included in an Orchestrator blueprint
Uses PowerShell | Unchecked | Determines if underlying implementation is PowerShell
Requires Store Password	| Unchecked | Determines if a store password is required when configuring an individual store.
Supports Entry Password	| Unchecked | Determines if an individual entry within a store can have a password.


**Advanced Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Store Path Type	| Freeform | Determines what restrictions are applied to the store path field when configuring a new store.
Store Path Value | N/A | This is reserved for the AWS Account Id when setting up the store.
Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias.
Private Keys | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store.
PFX Password Style | Default or Custom | "Default" - PFX password is randomly generated, "Custom" - PFX password may be specified when the enrollment job is created (Requires the *Allow Custom Password* application setting to be enabled.)

**Custom Fields:**

Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote
target server containing the certificate store to be managed

Name|Display Name|Type|Default Value / Options|Required|Description
---|---|---|---|---|---
scope | Okta OAuth Scope | string | N/A | Yes | This is the OAuth Scope needed for Okta OAuth, defined in Okta
grant_type | Okta OAuth Grant Type | string | N/A | Yes | In OAuth 2.0, the term “grant type” refers to the way an application gets an access token. In Okta this is `client_credentials`
oauthpath | OKTA OAuth Path | string | /oauth2/default/v1/token | Yes | In path to the OAuth Server.  It will Default to the Default Server.  If you use something outside of the Default, change this.
awsrole | AWS Assume Identity Role | string | N/A | Yes | This role has to be created in AWS IAM so you can assume an identity and get temp credentials
awsregions | AWS Regions | string | N/A | Yes | This will be the list of regions for the account the store iterates through when doing inventory.


**Entry Parameters:**

Entry parameters are inventoried and maintained for each entry within a certificate store.
They are typically used to support binding of a certificate to a resource.

Name|Display Name| Type|Default Value|Required When|Description
---|---|---|---|---|---
AWS Region | AWS Region | Multiple Choice | us-east-1 | Adding | When enrolling, this is the Region that the Certificate will be enrolled to.



Cert Store Settings
===============
| Number | Name | Value | Description |
| ----------- | ----------- | ----------- | ----------- |
| 0 | Client Machine | URL for Okta Application | This is the application setup in Okta with Key and Secret |
| 0 | User Name | Okta Key | Obtained from the Okta application |
| 0 | Password | Okta Secret | Obtained from the Okta application |
| 1 | Store Path | AWS Account Number | Unique account number obtained from AWS |
| 2 | Okta OAuth Scope | Look in Okta Setup for Scope | OAuth scope setup in the Okta Application |
| 3 | Okta OAuth Grant Type | client_credentials | This may vary depending on Okta setup but will most likely be this value. |
| 4 | OKTA OAuth Path | oauthpath | In path to the OAuth Server.  It will Default to the Default Server.  If you use something outside of the Default, change this. |
| 5 | AWS Assume Identity Role | Whatever Role is setup in AWS | Role must allow a third identity provider in AWS with AWS Cert Manager full access. |
| 6 | AWS Regions | us-east-1,us-east-2... | List of AWS Regions you want to inventory for the account above. |
| 7 | Store Password | No Password Needed for this | Set to no password needed. |



</details>
</details>

<details>
	<summary>[Deprecated] AWS Certificate Manager with IAM Auth Configuration <code>AwsCerManA</code></summary>

### AWS Setup
1. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for the permissions you want to grant, see [sample](/Images/AWSRole1.gif).
2. A [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should look like something like [this](/Images/AssumeRoleTrust.gif).
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a [standard AWS IAM User](/Images/UserAccount.gif) with an Access Key credential type.


<details>
<summary><code>AwsCerManA</code> Cert Store Type and Cert Store Setup</summary>

Cert Store Type Settings
===============
**Basic Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Name | Any Custom Name | Display name for the store type (may be customized)
Short Name| AWSCerManA | Short display name for the store type
Custom Capability | N/A | Store type name orchestrator will register with. Check the box to allow entry of value
Supported Job Types | Inventory, Add, Remove | Job types the extension supports
Needs Server | Checked | Determines if a target server name is required when creating store
Blueprint Allowed | Checked | Determines if store type may be included in an Orchestrator blueprint
Uses PowerShell | Unchecked | Determines if underlying implementation is PowerShell
Requires Store Password	| Unchecked | Determines if a store password is required when configuring an individual store.
Supports Entry Password	| Unchecked | Determines if an individual entry within a store can have a password.

**Advanced Settings:**

CONFIG ELEMENT | VALUE | DESCRIPTION
--|--|--
Store Path Type	| Freeform | Determines what restrictions are applied to the store path field when configuring a new store.
Store Path Value | N/A | This is reserved for the AWS Account Id when setting up the store.
Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias.
Private Keys | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store.
PFX Password Style | Default or Custom | "Default" - PFX password is randomly generated, "Custom" - PFX password may be specified when the enrollment job is created (Requires the *Allow Custom Password* application setting to be enabled.)


**Custom Fields:**

Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote
target server containing the certificate store to be managed

Name|Display Name|Type|Default Value / Options|Required|Description
---|---|---|---|---|---
awsrole | AWS Assume Identity Role | string | N/A | Yes | This role has to be created in AWS IAM so you can assume an identity and get temp credentials
awsregions | AWS Regions | string | N/A | Yes | This will be the list of regions for the account the store iterates through when doing inventory.


**Entry Parameters:**

Entry parameters are inventoried and maintained for each entry within a certificate store.
They are typically used to support binding of a certificate to a resource.

Name|Display Name| Type|Default Value|Required When|Description
---|---|---|---|---|---
AWS Region | AWS Region | Multiple Choice | us-east-1 | Adding | When enrolling, this is the Region that the Certificate will be enrolled to.


Cert Store Settings
===============
| Number | Name | Value | Description |
| ----------- | ----------- | ----------- | ----------- |
| 0 | Client Machine | Custom | Value is not used, choose any identifier |
| 1 | Store Path | AWS Account Number | Unique account number obtained from AWS |
| 2 | AWS Assume Identity Role | Whatever Role is setup in AWS | Role must allow a third identity provider in AWS with AWS Cert Manager full access. |
| 3 | AWS Regions | us-east-1,us-east-2... | List of AWS Regions you want to inventory for the account above. |
| 4 | User Name | IAM Access Key | Obtained from AWS |
| 5 | Password | IAM Access Secret | Obtained from the AWS |


</details>
</details>
