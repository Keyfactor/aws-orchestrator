<h1 align="center" style="border-bottom: none">
    AWS Certificate Manager (ACM) Universal Orchestrator Extension
</h1>

<p align="center">
  <!-- Badges -->
<img src="https://img.shields.io/badge/integration_status-production-3D1973?style=flat-square" alt="Integration Status: production" />
<a href="https://github.com/Keyfactor/aws-orchestrator/releases"><img src="https://img.shields.io/github/v/release/Keyfactor/aws-orchestrator?style=flat-square" alt="Release" /></a>
<img src="https://img.shields.io/github/issues/Keyfactor/aws-orchestrator?style=flat-square" alt="Issues" />
<img src="https://img.shields.io/github/downloads/Keyfactor/aws-orchestrator/total?style=flat-square&label=downloads&color=28B905" alt="GitHub Downloads (all assets, all releases)" />
</p>

<p align="center">
  <!-- TOC -->
  <a href="#support">
    <b>Support</b>
  </a>
  ·
  <a href="#installation">
    <b>Installation</b>
  </a>
  ·
  <a href="#license">
    <b>License</b>
  </a>
  ·
  <a href="https://github.com/orgs/Keyfactor/repositories?q=orchestrator">
    <b>Related Integrations</b>
  </a>
</p>

## Overview

AWS Certificate Manager is a service that lets you easily provision, manage, and deploy public and private Secure Sockets Layer/Transport Layer Security (SSL/TLS) certificates for use with AWS services and your internal connected resources. SSL/TLS certificates are used to secure network communications and establish the identity of websites over the Internet as well as resources on private networks. AWS Certificate Manager removes the time-consuming manual process of purchasing, uploading, and renewing SSL/TLS certificates.  The orchestrator supports Okta OAth authentication, as well as AWS IAM accounts. The Okta Support allows authentication against a 3rd party identity provider in AWS.  From there you can get temporary credentials for a role that you setup in each AWS Account.
 
### Documentation

- [Cert Manager API](https://docs.aws.amazon.com/acm/latest/userguide/sdk.html)
- [Aws Region Codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)



## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The AWS Certificate Manager (ACM) Universal Orchestrator extension If you have a support issue, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the AWS Certificate Manager (ACM) Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


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
1. A 3rd party [identity provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to [this](images/AWSIdentityProvider.gif) needs to be setup in AWS for each account.
2. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) similar to [this](images/AWSRole1.gif) needs Added for each AWS account.
3. Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like [this](images/AWSRole2.gif).

### OKTA Setup
1. Ensure your Authorization Server Is Setup in OKTA.  Here is a [sample](images/OktaSampleAuthorizationServer.gif).
2. Ensure the appropriate scopes are setup in Okta.  Here is a [sample](images/OktaSampleAuthorizationServer-scopes.gif).
3. Setup an Okta App with similar settings to [this](images/OktaApp1.gif) and [this](images/OktaApp2.gif).

</details>

<details>
<summary>[Deprecated] AWS Certificate Manager with IAM Auth Configuration <code>AwsCerManA</code></summary>

### AWS Setup
1. An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for the permissions you want to grant, see [sample](images/AWSRole1.gif).
2. A [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should look like something like [this](images/AssumeRoleTrust.gif).
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a [standard AWS IAM User](images/UserAccount.gif) with an Access Key credential type.

</details>


## Create the AWS-ACM Certificate Store Type

To use the AWS Certificate Manager (ACM) Universal Orchestrator extension, you **must** create the AWS-ACM Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.



* **Create AWS-ACM using kfutil**:

    ```shell
    # AWS Certificate Manager
    kfutil store-types create AWS-ACM
    ```

* **Create AWS-ACM manually in the Command UI**:
    <details><summary>Create AWS-ACM manually in the Command UI</summary>

    Create a store type called `AWS-ACM` with the attributes in the tables below:

    #### Basic Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Name | AWS Certificate Manager | Display name for the store type (may be customized) |
    | Short Name | AWS-ACM | Short display name for the store type |
    | Capability | AWS-ACM | Store type name orchestrator will register with. Check the box to allow entry of value |
    | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
    | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
    | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
    | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
    | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
    | Needs Server | ✅ Checked | Determines if a target server name is required when creating store |
    | Blueprint Allowed | ✅ Checked | Determines if store type may be included in an Orchestrator blueprint |
    | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
    | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
    | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

    The Basic tab should look like this:

    ![AWS-ACM Basic Tab](docsource/images/AWS-ACM-basic-store-type-dialog.png)

    #### Advanced Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
    | Private Key Handling | Required | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
    | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

    The Advanced tab should look like this:

    ![AWS-ACM Advanced Tab](docsource/images/AWS-ACM-advanced-store-type-dialog.png)

    > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

    #### Custom Fields Tab
    Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

    | Name | Display Name | Description | Type | Default Value/Options | Required |
    | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
    | UseEC2AssumeRole | Assume new Account / Role in EC2 | A switch to enable the store to assume a new Account ID and Role when using EC2 credentials | Bool | false | ✅ Checked |
    | UseOAuth | Use OAuth 2.0 Provider | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM | Bool | false | ✅ Checked |
    | UseIAM | Use IAM User Auth | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM | Bool | false | ✅ Checked |
    | EC2AssumeRole | AWS Role to Assume (EC2) | The AWS Role to assume using the EC2 instance credentials | String |  | 🔲 Unchecked |
    | OAuthScope | OAuth Scope | This is the OAuth Scope needed for Okta OAuth, defined in Okta | String |  | 🔲 Unchecked |
    | OAuthGrantType | OAuth Grant Type | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` | String | client_credentials | 🔲 Unchecked |
    | OAuthUrl | OAuth Url | An optional parameter sts:ExternalId to pass with Assume Role calls | String | https://***/oauth2/default/v1/token | 🔲 Unchecked |
    | IAMAssumeRole | AWS Role to Assume (IAM) | The AWS Role to assume as the IAM User. | String |  | 🔲 Unchecked |
    | OAuthAssumeRole | AWS Role to Assume (OAuth) | The AWS Role to assume after getting an OAuth token. | String |  | 🔲 Unchecked |
    | ExternalId | sts:ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls | String |  | 🔲 Unchecked |
    | ServerUsername | Server Username | The AWS Access Key for an IAM User or Client ID for OAuth. Depends on Auth method in use. | Secret |  | 🔲 Unchecked |
    | ServerPassword | Server Password | The AWS Access Secret for an IAM User or Client Secret for OAuth. Depends on Auth method in use. | Secret |  | 🔲 Unchecked |

    The Custom Fields tab should look like this:

    ![AWS-ACM Custom Fields Tab](docsource/images/AWS-ACM-custom-fields-store-type-dialog.png)



    #### Entry Parameters Tab

    | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
    | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
    | AWS Region | AWS Region | When adding, this is the Region that the Certificate will be added to | String |  | 🔲 Unchecked | ✅ Checked | 🔲 Unchecked | 🔲 Unchecked |
    | ACM Tags | ACM Tags | The optional ACM tags that should be assigned to the certificate.  Multiple name/value pairs may be entered in the format of `Name1=Value1,Name2=Value2,...,NameN=ValueN` | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

    The Entry Parameters tab should look like this:

    ![AWS-ACM Entry Parameters Tab](docsource/images/AWS-ACM-entry-parameters-store-type-dialog.png)



    </details>

## Installation

1. **Download the latest AWS Certificate Manager (ACM) Universal Orchestrator extension from GitHub.** 

    Navigate to the [AWS Certificate Manager (ACM) Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/aws-orchestrator/releases/latest). Refer to the compatibility matrix below to determine whether the `net6.0` or `net8.0` asset should be downloaded. Then, click the corresponding asset to download the zip archive.
    | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `aws-orchestrator` .NET version to download |
    | --------- | ----------- | ----------- | ----------- |
    | Older than `11.0.0` | | | `net6.0` |
    | Between `11.0.0` and `11.5.1` (inclusive) | `net6.0` | | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `Disable` | `net6.0` | 
    | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` | 
    | `11.6` _and_ newer | `net8.0` | | `net8.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net6.0`.

2. **Locate the Universal Orchestrator extensions directory.**

    * **Default on Windows** - `C:\Program Files\Keyfactor\Keyfactor Orchestrator\extensions`
    * **Default on Linux** - `/opt/keyfactor/orchestrator/extensions`
    
3. **Create a new directory for the AWS Certificate Manager (ACM) Universal Orchestrator extension inside the extensions directory.**
        
    Create a new directory called `aws-orchestrator`.
    > The directory name does not need to match any names used elsewhere; it just has to be unique within the extensions directory.

4. **Copy the contents of the downloaded and unzipped assemblies from __step 2__ to the `aws-orchestrator` directory.**

5. **Restart the Universal Orchestrator service.**

    Refer to [Starting/Restarting the Universal Orchestrator service](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/StarttheService.htm).


6. **(optional) PAM Integration** 

    The AWS Certificate Manager (ACM) Universal Orchestrator extension is compatible with all supported Keyfactor PAM extensions to resolve PAM-eligible secrets. PAM extensions running on Universal Orchestrators enable secure retrieval of secrets from a connected PAM provider.

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension, and follow the associated instructions to install it on the Universal Orchestrator (remote).


> The above installation steps can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores



* **Manually with the Command UI**

    <details><summary>Create Certificate Stores manually in the UI</summary>

    1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

        Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

    2. **Add a Certificate Store.**

        Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.
        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "AWS Certificate Manager" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine |  |
        | Store Path |  |
        | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM` certificates. Specifically, one with the `AWS-ACM` capability. |
        | UseEC2AssumeRole | A switch to enable the store to assume a new Account ID and Role when using EC2 credentials |
        | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM |
        | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM |
        | EC2AssumeRole | The AWS Role to assume using the EC2 instance credentials |
        | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
        | OAuthGrantType | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` |
        | OAuthUrl | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | IAMAssumeRole | The AWS Role to assume as the IAM User. |
        | OAuthAssumeRole | The AWS Role to assume after getting an OAuth token. |
        | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | ServerUsername | The AWS Access Key for an IAM User or Client ID for OAuth. Depends on Auth method in use. |
        | ServerPassword | The AWS Access Secret for an IAM User or Client Secret for OAuth. Depends on Auth method in use. |


        

        <details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

        If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.
        | Attribute | Description |
        | --------- | ----------- |
        | ServerUsername | The AWS Access Key for an IAM User or Client ID for OAuth. Depends on Auth method in use. |
        | ServerPassword | The AWS Access Secret for an IAM User or Client Secret for OAuth. Depends on Auth method in use. |


        Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.

        > Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself. 
        </details>
        

    </details>

* **Using kfutil**
    
    <details><summary>Create Certificate Stores with kfutil</summary>
    
    1. **Generate a CSV template for the AWS-ACM certificate store**

        ```shell
        kfutil stores import generate-template --store-type-name AWS-ACM --outpath AWS-ACM.csv
        ```
    2. **Populate the generated CSV file**

        Open the CSV file, and reference the table below to populate parameters for each **Attribute**.
        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "AWS Certificate Manager" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine |  |
        | Store Path |  |
        | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM` certificates. Specifically, one with the `AWS-ACM` capability. |
        | UseEC2AssumeRole | A switch to enable the store to assume a new Account ID and Role when using EC2 credentials |
        | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM |
        | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM |
        | EC2AssumeRole | The AWS Role to assume using the EC2 instance credentials |
        | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
        | OAuthGrantType | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` |
        | OAuthUrl | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | IAMAssumeRole | The AWS Role to assume as the IAM User. |
        | OAuthAssumeRole | The AWS Role to assume after getting an OAuth token. |
        | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | ServerUsername | The AWS Access Key for an IAM User or Client ID for OAuth. Depends on Auth method in use. |
        | ServerPassword | The AWS Access Secret for an IAM User or Client Secret for OAuth. Depends on Auth method in use. |


        

        <details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

        If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.
        | Attribute | Description |
        | --------- | ----------- |
        | ServerUsername | The AWS Access Key for an IAM User or Client ID for OAuth. Depends on Auth method in use. |
        | ServerPassword | The AWS Access Secret for an IAM User or Client Secret for OAuth. Depends on Auth method in use. |


        > Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself. 
        </details>
        

    3. **Import the CSV file to create the certificate stores** 

        ```shell
        kfutil stores import csv --store-type-name AWS-ACM --file AWS-ACM.csv
        ```
    </details>

> The content in this section can be supplimented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).





## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).