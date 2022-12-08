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

Cert Store Type Settings
===============

Cert Store Types Settings - Basic
---------------
| Section | Settings |
| ----------- | ----------- |
| Details | Name="Custom Name", Short Name="AWSCerManO" |
| Supported Job Types | Inventory, Add, Remove |
| General Settings | Needs Server, Blueprint Allowed |
| Password Settings | Supports Entry Password |

![image.png](/Images/CertStoreType-Basic.gif)

Cert Store Types Settings - Advanced
---------------
| Section | Settings |
| ----------- | ----------- |
| Store Path Type | Freeform |
| Other Settings | Supports Custom Alias=Optional, Private Key Handling=Optional, PFX Password Style=Default|

![image.png](/Images/CertStoreType-Advanced.gif)

Cert Store Types Settings - Custom Fields
---------------
| Name | Display Name | Required | Type | Value | Description |
| ----------- | ----------- | ----------- | ----------- | ----------- | ----------- |
| auth_type | Authentication Type | True | Multiple Choice | Okta OAuth,AWS IAM | This specifies whether to use Okta auth or IAM auth
| scope | Okta OAuth Scope | True (Okta only) | string | | This is the OAuth Scope needed for Okta OAuth
| grant_type | Okta OAuth Grant Type | True (Okta only) | string | | In OAuth 2.0, the term “grant type” refers to the way an application gets an access token
| awsrole | AWS Assume Identity Role | True | string | | This role has to be created in AWS IAM so you can assume an identity and get temp credentials
| awsregions | AWS Regions | True | string | | This will be the list of regions for the account the store iterates through when doing inventory.

![image.png](/Images/CertStoreType-CustomFields.gif)

Cert Store Types Settings - Entry Params
---------------
| Name | Display Name | Type | Default Value | Multiple Choice Questions | Required When |
| ----------- | ----------- | ----------- | ----------- | ----------- | ----------- |
| AWS Region | AWS Region | Multiple Choice | us-east1 | us-east1,us-east2... | Adding an Entry, Reenrolling Entry |

![image.png](/Images/CertStoreType-EntryParams.gif)

Cert Store Settings (Okta Auth)
===============
| Number | Name | Value | Description |
| ----------- | ----------- | ----------- | ----------- |
| 0 | Client Machine | URL for Okta Application | This is the application setup in Okta with Key and Secret |
| 0 | User Name | Okta Key | Obtained from the Okta application |
| 0 | Password | Okta Secret | Obtained from the Okta application |
| 1 | Store Path | AWS Account Number | Unique account number obtained from AWS |
| 2 | Authentication Type | Auth type to use | Select Okta OAth |
| 3 | Okta OAuth Scope | Look in Okta Setup for Scope | OAuth scope setup in the Okta Application |
| 4 | Okta OAuth Grant Type | client_credentials | This may vary depending on Okta setup but will most likely be this value. |
| 5 | AWS Assume Identity Role | Whatever Role is setup in AWS | Role must allow a third identity provider in AWS with AWS Cert Manager full access. |
| 6 | AWS Regions | us-east1,us-east2... | List of AWS Regions you want to inventory for the account above. |
| 7 | Store Password | No Password Needed for this | Set to no password needed. |

![image.png](/Images/CertStore2.gif)

Cert Store Settings (IAM Auth)
===============
| Number | Name | Value | Description |
| ----------- | ----------- | ----------- | ----------- |
| 0 | Client Machine | None | IAM auth does not use |
| 0 | User Name | IAM Account Key | Obtained from AWS |
| 0 | Password | IAM Account Secret | Obtained from the AWS |
| 1 | Store Path | AWS Account Number | Unique account number obtained from AWS |
| 2 | Authentication Type | Auth type to use | Select AWS IAM |
| 3 | AWS Assume Identity Role | Whatever Role is setup in AWS | Role must allow a third identity provider in AWS with AWS Cert Manager full access. |
| 4 | AWS Regions | us-east1,us-east2... | List of AWS Regions you want to inventory for the account above. |
| 5 | Store Password | No Password Needed for this | Set to no password needed. |

![image.png](/Images/CertStore2.gif)

AWS Setup
===============
Identity Provider Setup
---------------
A 3rd party [identity provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html) similar to the one below needs to be setup in AWS for each account.
![image.png](/Images/AWSIdentityProvider.gif)

AWS Role Setup
---------------
An Aws [Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) Needs Added for each AWS account.
![image.png](/Images/AWSRole1.gif)

Trust Relationship
---------------
Ensure the [trust relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html) is setup for that role.  Should  look like below:
![image.png](/Images/AWSRole2.gif)

OKTA Setup
===============
Okta API - Settings
---------------
Ensure your Authorization Server Is Setup in OKTA.  Here is a sample below:
![image.png](/Images/OktaSampleAuthorizationServer.gif)

Okta API - Scopes
---------------
Ensure the appropriate scopes are setup in Okta.  Here is a sample below:
![image.png](/Images/OktaSampleAuthorizationServer-scopes.gif)

Okta App
---------------
Setup an Okta App with similar settings to the screens below:
![image.png](/Images/OktaApp1.gif)
![image.png](/Images/OktaApp2.gif)




