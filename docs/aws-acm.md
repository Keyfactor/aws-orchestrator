## AWS Certificate Manager

The AWS Certificate Manager (ACM) Certificate Store Type facilitates the management of SSL/TLS certificates within the AWS ecosystem through Keyfactor Command. This store type allows the addition, deletion, and inventory of certificates and their keys, streamlining the process of SSL/TLS certificate lifecycle management. The certificate store represents either a collection of certificates or a single certificate located on an AWS ACM platform.

This Certificate Store Type supports multiple authentication methods, including Okta OAuth, AWS IAM accounts, and default AWS SDK-based authentication, which can adapt depending on the environment where the orchestrator is running. For instance, on an EC2 instance, the orchestrator can leverage IAM roles to access ACM without requiring additional configuration for IAM or OAuth.

However, there are some caveats and limitations to be aware of. Reenrollment and discovery functionalities are not implemented or supported, and proper setup of roles and trust relationships in AWS and third-party identity providers are necessary for successful operation. Ensuring that these roles and permissions are correctly configured is crucial for seamless integration and operation.

Users should also note that the orchestrator's use of AWS SDK means it adheres to the authentication and security protocols supported by the SDK, providing flexibility and adaptability within the AWS environment. Nonetheless, understanding the specific requirements and setup for OAuth or IAM user authentication is essential to avoid any operational pitfalls.



### Supported Job Types

| Job Name | Supported |
| -------- | --------- |
| Inventory | ✅ |
| Management Add | ✅ |
| Management Remove | ✅ |
| Discovery |  |
| Create |  |
| Reenrollment |  |

## Requirements

### AWS Setup
Options for authenticating:
1. Okta or other OAuth configuration (refer to `AwsCerManO` below)
2. IAM User Auth configuration (refer to `AwsCerManA` below)
3. EC2 Role Auth or other default method supported by the [AWS SDK](https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html)

As one option for #3, to set up Role Auth for an EC2 instance, follow the steps below. Note, this applies specifically __when the orchestrator is running `ACM-AWS` inside of an EC2 instance__.
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
Private Keys | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store.
PFX Password Style | Default or Custom | "Default" - PFX password is randomly generated, "Custom" - PFX password may be specified when the enrollment job is created (Requires the *Allow Custom Password* application setting to be enabled.)

**Custom Fields:**

Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote
target server containing the certificate store to be managed

Name|Display Name|Type|Default Value|Depends On|Required|Description
---|---|---|---|---|---|---
UseOAuth | Use OAuth 2.0 Provider | boolean | False | N/A | Yes | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM
UseIAM | Use IAM User Auth | boolean | False | N/A | Yes | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM
OAuthScope | OAuth Scope | string | N/A | Use OAuth 2.0 Provider | No | This is the OAuth Scope needed for Okta OAuth, defined in Okta
OAuthGrantType | OAuth Grant Type | string | client_credentials | Use OAuth 2.0 Provider | No | In OAuth 2.0, the term “grant type” refers to the way an application gets an access token. In Okta this is `client_credentials`
OAuthUrl | OAuth URL | string | https://***/oauth2/default/v1/token | Use OAuth 2.0 Provider | No | The URL to request a token from your OAuth Provider. Fill this out with the correct URL.
OAuthAccountId | OAuth AWS Account Id | string | N/A | Use OAuth 2.0 Provider | No | The AWS account ID to use after getting an OAuth token to assume the associated Role.
IamAccountId | IAM AWS Account ID | string | N/A | Use IAM User Auth | No | The AWS account ID to use when assuming a role as the IAM User.


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
| Client Machine | AWS Role | This is the AWS Role that will be used for access. This role will be assumed and its permissions will apply to all actions taken by the orchestrator. |
| User Name | See Below | See Below |
| Password | See Below | See Below |
| Store Path | us-east-1,us-east-2,...,etc. | The AWS Region, or a comma-separated list of multiple regions, the store will operate in. |
| Use OAuth 2.0 Provider | Use an OAuth provider to authenticate with AWS | Set to true to enable OAuth usage and display additional OAuth fields |
| Use IAM User Auth | Use an IAM user's credentials to assume a role | Set to true to enable IAM user auth and the IAM Account ID field. |
| OAuth Scope | Look in OAuth provider for Scope | Displayed and required when using OAuth 2.0 Provider. OAuth scope setup in the Okta Application or other OAuth provider |
| OAuth Grant Type | client_credentials | Displayed and required when using OAuth 2.0 Provider. This may vary depending on Okta setup but will most likely be this value. |
| OAuth URL | https://***/oauth2/default/v1/token | Displayed and required when using OAuth 2.0 Provider. URL to request token from OAuth provider. Example given is for an Okta token. |
| OAuth AWS Account Id | AWS account ID number | Displayed and required when using OAuth 2.0 Provider. This account ID is used in conjunction with the OAuth token to assume a role (set in the Client Machine parameter) |
| IAM AWS Account Id | AWS account ID number | Displayed and required when using IAM User Auth. This account ID is used to assume a role (set in the Client Machine parameter) |

The User Name and Password fields are used differently based on the auth method you intend to use. The three options for auth are IAM User, OAuth, or default auth.

| Auth Method | Field | Value |
| - | - | - |
| IAM User | User Name | Set to the IAM User's AWS `Access Key` |
| IAM User | Password | Set to the IAM User's AWS `Access Secret` |
| OAuth 2.0 | User Name | Set to the OAuth `Client ID` |
| OAuth 2.0 | Password | Set to the OAuth `Client Secret` |
| Default (SDK) | User Name | No Value |
| Default (SDK) | Password | No Value |

</details>
</details>

<details>
<summary>[Deprecated] AWS Certificate Manager with Okta Auth Configuration <code>AwsCerManO</code></summary>



## Certificate Store Type Configuration

The recommended method for creating the `AWS-ACM` Certificate Store Type is to use [kfutil](https://github.com/Keyfactor/kfutil). After installing, use the following command to create the `` Certificate Store Type:

```shell
kfutil store-types create AWS-ACM
```

<details><summary>AWS-ACM</summary>

Create a store type called `AWS-ACM` with the attributes in the tables below:

### Basic Tab
| Attribute | Value | Description |
| --------- | ----- | ----- |
| Name | AWS Certificate Manager | Display name for the store type (may be customized) |
| Short Name | AWS-ACM | Short display name for the store type |
| Capability | AWS-ACM | Store type name orchestrator will register with. Check the box to allow entry of value |
| Supported Job Types (check the box for each) | Add, Discovery, Remove | Job types the extension supports |
| Supports Add | ✅ | Check the box. Indicates that the Store Type supports Management Add |
| Supports Remove | ✅ | Check the box. Indicates that the Store Type supports Management Remove |
| Supports Discovery |  |  Indicates that the Store Type supports Discovery |
| Supports Reenrollment |  |  Indicates that the Store Type supports Reenrollment |
| Supports Create |  |  Indicates that the Store Type supports store creation |
| Needs Server | ✅ | Determines if a target server name is required when creating store |
| Blueprint Allowed | ✅ | Determines if store type may be included in an Orchestrator blueprint |
| Uses PowerShell |  | Determines if underlying implementation is PowerShell |
| Requires Store Password |  | Determines if a store password is required when configuring an individual store. |
| Supports Entry Password |  | Determines if an individual entry within a store can have a password. |

The Basic tab should look like this:

![AWS-ACM Basic Tab](../docsource/images/AWS-ACM-basic-store-type-dialog.png)

### Advanced Tab
| Attribute | Value | Description |
| --------- | ----- | ----- |
| Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
| Private Key Handling | Optional | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
| PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

The Advanced tab should look like this:

![AWS-ACM Advanced Tab](../docsource/images/AWS-ACM-advanced-store-type-dialog.png)

### Custom Fields Tab
Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

| Name | Display Name | Type | Default Value/Options | Required | Description |
| ---- | ------------ | ---- | --------------------- | -------- | ----------- |


The Custom Fields tab should look like this:

![AWS-ACM Custom Fields Tab](../docsource/images/AWS-ACM-custom-fields-store-type-dialog.png)



</details>

## Certificate Store Configuration

After creating the `AWS-ACM` Certificate Store Type and installing the AWS Certificate Manager (ACM) Universal Orchestrator extension, you can create new [Certificate Stores](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store) to manage certificates in the remote platform.

The following table describes the required and optional fields for the `AWS-ACM` certificate store type.

| Attribute | Description | Attribute is PAM Eligible |
| --------- | ----------- | ------------------------- |
| Category | Select "AWS Certificate Manager" or the customized certificate store name from the previous step. | |
| Container | Optional container to associate certificate store with. | |
| Client Machine | The AWS Role that will be used for access, which will be assumed to apply its permissions to all orchestrator actions. Example: 'arn:aws:iam::123456789012:role/ACMAccess' | |
| Store Path | The AWS Region, or a comma-separated list of multiple regions, where the store will operate. Example: 'us-east-1,us-west-2' | |
| Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM` certificates. Specifically, one with the `AWS-ACM` capability. | |

* **Using kfutil**

    ```shell
    # Generate a CSV template for the AzureApp certificate store
    kfutil stores import generate-template --store-type-name AWS-ACM --outpath AWS-ACM.csv

    # Open the CSV file and fill in the required fields for each certificate store.

    # Import the CSV file to create the certificate stores
    kfutil stores import csv --store-type-name AWS-ACM --file AWS-ACM.csv
    ```

* **Manually with the Command UI**: In Keyfactor Command, navigate to Certificate Stores from the Locations Menu. Click the Add button to create a new Certificate Store using the attributes in the table above.