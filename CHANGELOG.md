3.0.0
* Remove `AwsCerManO` and `AwsCerManA` types
  * These `DEPRECATED` Store Types are no longer supported

2.2.1
* Updated or removed package dependencies with a signing vulnerability

2.2.0
* Add entry parameter for ACM tags
* Modify to produce .net6/8 dual builds
* Modify README to use doctool

2.1.0
* Allow EC2 default credentials to also run the Assume Role command
* Add sts:ExtenalId parameter option to Assume Role calls (not applicable when using OAuth)

2.0.2
* Return parity to original AWS store type organization - differentiating based on AWS Account ID

2.0.1
* Remove logging of sensitive data
* Update Private Key to required for certificates in this store in docs and store definition

2.0.0
* Consolidate all AWS auth types under one Store Type: `AWS-ACM`
* Continues to provide backwards support for previous Store Types `AwsCerManO` and `AwsCerManA`
  * This support will be removed in a future version, it is now considered `DEPRECATED`
* Support choosing auth type:
  * OAuth Provider
  * AWS IAM User
  * Inferred credentials present on an EC2 instance running the orchestrator
	* The valid sources for credentials received in this manner can be found here: 
	  https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/creds-assign.html
* PAM Provider support for the following fields:
  * `ServerUsername`
  * `ServerPassword`
  * These fields are supported on all Store Types (including backwards support): `AWS-ACM`, `AwsCerMan0` and `AwsCerManA`

1.2.0
* Added OTKA Auth Path to support Authentication Servers outside of the default server.

1.1.0
* Added AWS IAM Authentication support with Roles

1.0.0
* Convert to Universal Orchestrator Framework
* Added OKTA Authentication Support
