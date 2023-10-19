*** 
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

### Assumptions:
- In order for the Certificates and Keys to renew or reenroll correctly, they need to derive of the <alias> which is passed into the any agent.  The <alias> drives the files and object creation and is essentially how we are able to relate them to each other.

### Not Implemented/Supported
- Reenrollment, Management, Discovery

## **Installation**
Depending on your choice of authentication providers, choose the appropriate configuration section
<details>
<summary>AWS Certificate Manager with Okta Auth Configuration</summary>
&nbsp;&nbsp;
<details>
<summary>AWS Setup</summary>

>### Identity Provider Setup

>A 3rd party [identity provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to the one below needs to be setup in AWS for each account.
>![image.png](/Images/AWSIdentityProvider.gif)

>### AWS Role Setup

>An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for each AWS account.
![image.png](/Images/AWSRole1.gif)

>### Trust Relationship

>Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like below:
>![image.png](/Images/AWSRole2.gif)

## OKTA Setup

>### Okta API - Settings

>Ensure your Authorization Server Is Setup in OKTA.  Here is a sample below:
>![image.png](/Images/OktaSampleAuthorizationServer.gif)

>### Okta API - Scopes

>Ensure the appropriate scopes are setup in Okta.  Here is a sample below:
>![image.png](/Images/OktaSampleAuthorizationServer-scopes.gif)

>### Okta App

>Setup an Okta App with similar settings to the screens below:
>![image.png](/Images/OktaApp1.gif)
>![image.png](/Images/OktaApp2.gif)

</details>
<details>
<summary>Cert Store Type and Cert Store Setup</summary>
<details>
## Cert Store Type Settings

Cert Store Types Settings - Basic
---------------
| Section | Settings |
| ----------- | ----------- |
| Details | Name="Custom Name", Short Name="AWSCerManO" |
| Supported Job Types | Inventory, Add, Remove |
| General Settings | Needs Server, Blueprint Allowed |
| Password Settings | Supports Entry Password |

![image.png](/Images/CertStoreType-Basic-Okta.gif)

Cert Store Types Settings - Advanced
---------------
| Section | Settings |
| ----------- | ----------- |
| Store Path Type | Freeform |
| Other Settings | Supports Custom Alias=Optional, Private Key Handling=Optional, PFX Password Style=Default|

![image.png](/Images/CertStoreType-Advanced.gif)

Cert Store Types Settings - Custom Fields
---------------
| Name | Display Name | Required | Type | Description |
| ----------- | ----------- | ----------- | ----------- | ----------- |
| scope | Okta OAuth Scope | True| string | This is the OAuth Scope needed for Okta OAuth
| grant_type | Okta OAuth Grant Type | True | string | In OAuth 2.0, the term “grant type” refers to the way an application gets an access token
| awsrole | AWS Assume Identity Role | True | string | This role has to be created in AWS IAM so you can assume an identity and get temp credentials
| awsregions | AWS Regions | True | string | This will be the list of regions for the account the store iterates through when doing inventory.

![image.png](/Images/CertStoreType-CustomFields-Okta.gif)

Cert Store Types Settings - Entry Params
---------------
| Name | Display Name | Type | Default Value | Multiple Choice Questions | Required When |
| ----------- | ----------- | ----------- | ----------- | ----------- | ----------- |
| AWS Region | AWS Region | Multiple Choice | us-east-1 | us-east-1,us-east-2... | Adding an Entry, Reenrolling Entry |

![image.png](/Images/CertStoreType-EntryParams.gif)

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
| 4 | AWS Assume Identity Role | Whatever Role is setup in AWS | Role must allow a third identity provider in AWS with AWS Cert Manager full access. |
| 5 | AWS Regions | us-east-1,us-east-2... | List of AWS Regions you want to inventory for the account above. |
| 6 | Store Password | No Password Needed for this | Set to no password needed. |

![image.png](/Images/CertStore2.gif)


</details>



	<summary>AWS Certificate Manager with IAM Auth Configuration</summary>
NOTE FOR IAM AUTH:

AWS does not support programmatic access for AWS SSO accounts. The account used here must be a standard AWS IAM User with an Access Key credential type.
![image.png](/Images/UserAccount.gif)


Cert Store Type Settings
===============

Cert Store Types Settings - Basic
---------------
| Section | Settings |
| ----------- | ----------- |
| Details | Name="Custom Name", Short Name="AWSCerManA" |
| Supported Job Types | Inventory, Add, Remove |
| General Settings | Needs Server, Blueprint Allowed |
| Password Settings | Supports Entry Password |

![image.png](/Images/CertStoreType-Basic-IAM.gif)

Cert Store Types Settings - Advanced
---------------
| Section | Settings |
| ----------- | ----------- |
| Store Path Type | Freeform |
| Other Settings | Supports Custom Alias=Optional, Private Key Handling=Optional, PFX Password Style=Default|

![image.png](/Images/CertStoreType-Advanced.gif)

Cert Store Types Settings - Custom Fields
---------------
| Name | Display Name | Required | Type | Description |
| ----------- | ----------- | ----------- | ----------- | ----------- |
| awsrole | AWS Assume Identity Role | True | string | This role has to be created in AWS IAM so you can assume an identity and get temp credentials
| awsregions | AWS Regions | True | string | This will be the list of regions for the account the store iterates through when doing inventory.

![image.png](/Images/CertStoreType-CustomFields-IAM.gif)

Cert Store Types Settings - Entry Params
---------------
| Name | Display Name | Type | Default Value | Multiple Choice Questions | Required When |
| ----------- | ----------- | ----------- | ----------- | ----------- | ----------- |
| AWS Region | AWS Region | Multiple Choice | us-east-1 | us-east-1,us-east-2... | Adding an Entry, Reenrolling Entry |

![image.png](/Images/CertStoreType-EntryParams.gif)

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

![image.png](/Images/CertStore-IAM.gif)

</details>
AWS Setup
===============

AWS Role Setup
---------------
An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for the permissions you want to grant.
![image.png](/Images/AWSRole1.gif)

Trust Relationship
---------------
Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like below, where AssumeRoleTest is the account whose access key/secret you are using:
![image.png](/Images/AssumeRoleTrust.gif)