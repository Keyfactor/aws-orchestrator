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

This integration also supports the reading of existing certificate ACM key/value pair tags during inventory and adding these tags when adding new certificates.  Modifying and adding ACM tags during certificate renewal, however, is NOT supported.  This is due to the fact that the AWS API does not allow for ACM tag modification when updating a certificate in one step.  This would need to be done in multiple steps, leading to the possibility of the certificate being left in an error state if any intermediate step were to fail.  However, while the modification/addition of ACM tags is not supported, all existing ACM tags WILL remain in place during renewal.
 
### Documentation

- [Cert Manager API](https://docs.aws.amazon.com/acm/latest/userguide/sdk.html)
- [Aws Region Codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)



### AWS-ACM-v3
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Overview is a required section

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support
The AWS Certificate Manager (ACM) Universal Orchestrator extension If you have a support issue, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com. 
 
> To report a problem or suggest a new feature, use the **[Issues](../../issues)** tab. If you want to contribute actual bug fixes or proposed enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the AWS Certificate Manager (ACM) Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.


### Setting up AWS Authentication

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

### AWS Certificate Manager v3 Requirements
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Requirements is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info




## AWS-ACM-v3 Certificate Store Type

To use the AWS Certificate Manager (ACM) Universal Orchestrator extension, you **must** create the AWS-ACM-v3 Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.


TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info



### Supported Operations

| Operation    | Is Supported                                                                                                           |
|--------------|------------------------------------------------------------------------------------------------------------------------|
| Add          | ✅ Checked        |
| Remove       | ✅ Checked     |
| Discovery    | 🔲 Unchecked  |
| Reenrollment | 🔲 Unchecked |
| Create       | 🔲 Unchecked     |

### Creation Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to created certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)

#### Using online definition from GitHub:
This will reach out to GitHub and pull the latest store-type definition
```shell
# AWS Certificate Manager v3
kfutil store-types create AWS-ACM-v3
```

#### Offline creation using integration-manifest file:
If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
in your offline environment.
```shell
kfutil store-types create --from-file integration-manifest.json
```

### Manual Creation
If you do not wish to use the `kfutil` CLI then certificate store types can be creating in the web UI as described below.

* **Create AWS-ACM-v3 manually in the Command UI**:
    <details><summary>Create AWS-ACM-v3 manually in the Command UI</summary>

    Create a store type called `AWS-ACM-v3` with the attributes in the tables below:

    #### Basic Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Name | AWS Certificate Manager v3 | Display name for the store type (may be customized) |
    | Short Name | AWS-ACM-v3 | Short display name for the store type |
    | Capability | AWS-ACM-v3 | Store type name orchestrator will register with. Check the box to allow entry of value |
    | Supports Add | ✅ Checked | Check the box. Indicates that the Store Type supports Management Add |
    | Supports Remove | ✅ Checked | Check the box. Indicates that the Store Type supports Management Remove |
    | Supports Discovery | 🔲 Unchecked |  Indicates that the Store Type supports Discovery |
    | Supports Reenrollment | 🔲 Unchecked |  Indicates that the Store Type supports Reenrollment |
    | Supports Create | 🔲 Unchecked |  Indicates that the Store Type supports store creation |
    | Needs Server | 🔲 Unchecked | Determines if a target server name is required when creating store |
    | Blueprint Allowed | ✅ Checked | Determines if store type may be included in an Orchestrator blueprint |
    | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
    | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
    | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

    The Basic tab should look like this:

    ![AWS-ACM-v3 Basic Tab](docsource/images/AWS-ACM-v3-basic-store-type-dialog.png)

    #### Advanced Tab
    | Attribute | Value | Description |
    | --------- | ----- | ----- |
    | Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
    | Private Key Handling | Required | This determines if Keyfactor can send the private key associated with a certificate to the store. Required because IIS certificates without private keys would be invalid. |
    | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

    The Advanced tab should look like this:

    ![AWS-ACM-v3 Advanced Tab](docsource/images/AWS-ACM-v3-advanced-store-type-dialog.png)

    > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

    #### Custom Fields Tab
    Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

    | Name | Display Name | Description | Type | Default Value/Options | Required |
    | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
    | UseDefaultSdkAuth | Use Default SDK Auth | A switch to enable the store to use Default SDK credentials | Bool | false | ✅ Checked |
    | DefaultSdkAssumeRole | Assume new Role using Default SDK Auth | A switch to enable the store to assume a new Role when using Default SDK credentials | Bool | false | 🔲 Unchecked |
    | UseOAuth | Use OAuth 2.0 Provider | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM | Bool | false | ✅ Checked |
    | OAuthScope | OAuth Scope | This is the OAuth Scope needed for Okta OAuth, defined in Okta | String |  | 🔲 Unchecked |
    | OAuthGrantType | OAuth Grant Type | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` | String | client_credentials | 🔲 Unchecked |
    | OAuthUrl | OAuth Url | An optional parameter sts:ExternalId to pass with Assume Role calls | String | https://***/oauth2/default/v1/token | 🔲 Unchecked |
    | OAuthClientId | OAuth Client ID | The Client ID for OAuth. | Secret |  | 🔲 Unchecked |
    | OAuthClientSecret | OAuth Client Secret | The Client Secret for OAuth. | Secret |  | 🔲 Unchecked |
    | UseIAM | Use IAM User Auth | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM | Bool | false | ✅ Checked |
    | IamUserAccessKey | IAM User Access Key | The AWS Access Key for an IAM User | Secret |  | 🔲 Unchecked |
    | IamUserAccessSecret | IAM User Access Secret | The AWS Access Secret for an IAM User. | Secret |  | 🔲 Unchecked |
    | ExternalId | sts:ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls | String |  | 🔲 Unchecked |

    The Custom Fields tab should look like this:

    ![AWS-ACM-v3 Custom Fields Tab](docsource/images/AWS-ACM-v3-custom-fields-store-type-dialog.png)

    #### Entry Parameters Tab

    | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
    | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
    | ACM Tags | ACM Tags | The optional ACM tags that should be assigned to the certificate.  Multiple name/value pairs may be entered in the format of `Name1=Value1,Name2=Value2,...,NameN=ValueN` | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

    The Entry Parameters tab should look like this:

    ![AWS-ACM-v3 Entry Parameters Tab](docsource/images/AWS-ACM-v3-entry-parameters-store-type-dialog.png)





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


> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).



## Defining Certificate Stores


TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info

TODO Certificate Store Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


### Store Creation

* **Manually with the Command UI**

    <details><summary>Create Certificate Stores manually in the UI</summary>

    1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

        Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

    2. **Add a Certificate Store.**

        Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "AWS Certificate Manager v3" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine | This is a full AWS ARN specifying a Role. This is the Role that will be assumed in any Auth scenario performing Assume Role. This will dictate what certificates are usable by the orchestrator. A preceeding [profile] name should be included if a Credential Profile is to be used in Default Sdk Auth. |
        | Store Path | A single specified AWS Region the store will operate in. Additional regions should get their own store defined. |
        | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM-v3` certificates. Specifically, one with the `AWS-ACM-v3` capability. |
        | UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
        | DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
        | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM |
        | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
        | OAuthGrantType | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` |
        | OAuthUrl | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | OAuthClientId | The Client ID for OAuth. |
        | OAuthClientSecret | The Client Secret for OAuth. |
        | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM |
        | IamUserAccessKey | The AWS Access Key for an IAM User |
        | IamUserAccessSecret | The AWS Access Secret for an IAM User. |
        | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
    </details>


* **Using kfutil**
    
    <details><summary>Create Certificate Stores with kfutil</summary>
    
    1. **Generate a CSV template for the AWS-ACM-v3 certificate store**

        ```shell
        kfutil stores import generate-template --store-type-name AWS-ACM-v3 --outpath AWS-ACM-v3.csv
        ```
    2. **Populate the generated CSV file**

        Open the CSV file, and reference the table below to populate parameters for each **Attribute**.

        | Attribute | Description |
        | --------- | ----------- |
        | Category | Select "AWS Certificate Manager v3" or the customized certificate store name from the previous step. |
        | Container | Optional container to associate certificate store with. |
        | Client Machine | This is a full AWS ARN specifying a Role. This is the Role that will be assumed in any Auth scenario performing Assume Role. This will dictate what certificates are usable by the orchestrator. A preceeding [profile] name should be included if a Credential Profile is to be used in Default Sdk Auth. |
        | Store Path | A single specified AWS Region the store will operate in. Additional regions should get their own store defined. |
        | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM-v3` certificates. Specifically, one with the `AWS-ACM-v3` capability. |
        | UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
        | DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
        | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS ACM |
        | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
        | OAuthGrantType | In OAuth 2.0, the term �grant type� refers to the way an application gets an access token. In Okta this is `client_credentials` |
        | OAuthUrl | An optional parameter sts:ExternalId to pass with Assume Role calls |
        | OAuthClientId | The Client ID for OAuth. |
        | OAuthClientSecret | The Client Secret for OAuth. |
        | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS ACM |
        | IamUserAccessKey | The AWS Access Key for an IAM User |
        | IamUserAccessSecret | The AWS Access Secret for an IAM User. |
        | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
    3. **Import the CSV file to create the certificate stores**

        ```shell
        kfutil stores import csv --store-type-name AWS-ACM-v3 --file AWS-ACM-v3.csv
        ```

* **PAM Provider Eligible Fields**
    <details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

    If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

    | Attribute | Description |
    | --------- | ----------- |
    | OAuthClientId | The Client ID for OAuth. |
    | OAuthClientSecret | The Client Secret for OAuth. |
    | IamUserAccessKey | The AWS Access Key for an IAM User |
    | IamUserAccessSecret | The AWS Access Secret for an IAM User. |

    Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.

    > Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.
    </details>


> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).


## Discovering Certificate Stores with the Discovery Job

### AWS Certificate Manager v3 Discovery Job
TODO Global Store Type Section is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info


TODO Discovery Job Configuration is an optional section. If this section doesn't seem necessary on initial glance, please delete it. Refer to the docs on [Confluence](https://keyfactor.atlassian.net/wiki/x/SAAyHg) for more info




## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).