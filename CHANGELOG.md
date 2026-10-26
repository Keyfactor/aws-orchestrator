3.1.1
* Bug Fix - **Inventory** and **Discovery** now return ACME-issued certificates
  * `ListCertificates` omits certificates with a `CertificateKeyPairOrigin` of `ACME` unless the origins are requested explicitly, so ACME-issued certificates were silently absent from inventory. Both jobs now request all origins the AWS SDK defines (`AWS_MANAGED`, `CUSTOMER_PROVIDED`, `ACME`)
  * Previously a region containing only ACME-issued certificates appeared empty to Discovery and no certificate store was created for it
  * ACME-issued certificates are inventoried for visibility only. Like Amazon-issued and Private CA-issued certificates they are not user-imported, so Management renewal/replace still fails by design - the failure message now names the certificate's `Type` and key pair origin instead of only "Amazon-issued or Private CA-issued"
* Bug Fix - Added `RSA_3072` to the Inventory key type filter. `Includes.KeyTypes` is an inclusion filter, so RSA-3072 certificates were silently absent from inventory
* Bug Fix - **Inventory** no longer reports every entry as a private key entry
  * `PrivateKeyEntry` was hard-coded to `true`, so Command offered renewal/replace on certificates ACM holds no private key for - Amazon-issued, Private CA-issued, and ACME-issued - and the Management job then failed at run time by design
  * `PrivateKeyEntry` is now derived from the certificate's `Type` and is set only for user-imported (`IMPORTED`) certificates. `Type` is returned by `ListCertificates`, so this adds no API calls
* Bug Fix - **Inventory** no longer depends on the PEM it receives ending in a newline
  * The PEM footer was only stripped when a newline followed it, so a certificate body returned without a trailing newline would have been sent to Command with literal `-----END CERTIFICATE-----` text inside the Base64 value. ACM always sends the newline, so this never occurred in practice
* Bug Fix - **Management** alias-to-ARN matching is now case-insensitive throughout
  * The `arn:aws:acm:` prefix test was case-insensitive but the `:certificate/` resource test was not, so a fully uppercased ARN was not recognised as an existing certificate and would have been imported as a new certificate instead of replaced in place, leaving a duplicate in ACM. ACM returns lowercase ARNs, so this only affected a hand-entered alias
* Bug Fix - The **Discovery** region probe now applies the same `Includes.KeyTypes` filter as **Inventory**
  * The probe requested the ACME key pair origins but no key types, and `Includes.KeyTypes` is an inclusion filter, so a region whose certificates all lacked a SAN looked empty to Discovery and no certificate store was created for it - the same defect as the ACME-origin bug above, in the other filter
* Security - **Management** no longer writes the certificate contents to the log
  * `JobCertificate.Contents` (the base64 PFX, which carries the private key) was logged at Trace level on every Add job. Only its length is logged now
* Inventory now requests 1000 certificates per `ListCertificates` page instead of 100 (1000 is the documented maximum), reducing round trips on large stores
* Inventory no longer serializes ACM responses to JSON for Trace logging when Trace logging is disabled. The per-certificate dumps ran on every certificate regardless of log level
* Removed a redundant unfiltered `ListCertificates` call in Inventory that fed only a log line
* Added Debug logging of the filters applied by Inventory, the key pair origins and certificate types found per batch, and the filter set when a region returns no certificates
* Documentation rewrite - `docsource/content.md` and `docsource/aws-acm-v3.md` now cover the destination-identity model explicitly, give exact field values per authentication method, and include copy/adapt IAM and trust policy examples for inventory-only, inventory + management, and cross-account Discovery
  * Added sections on cross-account Discovery job configuration (dialog field mapping and region scanning), certificate types and renewal eligibility, ACM tag behaviour on renewal, `ListCertificates` inclusion-filter behaviour, and troubleshooting
* Bug Fix - Corrected the `OAuthUrl` store type property description in `integration-manifest.json`, which had been copy-pasted from `ExternalId` and described an `sts:ExternalId` parameter instead of the OAuth token endpoint URL

3.1.0
* Added support for cross-account **Discovery**
  * Discovery enumerates AWS Organizations accounts via `organizations:ListAccounts`, assumes a per-account IAM role (configurable via the new `DiscoveryRoleName` property; default `KeyfactorACMDiscoveryRole`), and scans each account's enabled regions for ACM certificates
  * Discovered certificate stores use a self-contained `Store Path` of the form `<roleArn>|<region>` that carries both the Role ARN to assume and the AWS Region in a single field; legacy region-only `Store Path` values (with the Role ARN in `Client Machine`) continue to work
  * Added the `DiscoveryRoleName` store type property
* Discovery, Inventory, and Management jobs now return descriptive success, warning, and failure messages to Keyfactor Command that identify the exact store (and, for Discovery, the accounts and regions scanned) instead of directing the user to the logs

3.0.2
* Bug Fix - On Management jobs, do not send ACM tags if the certificate is being renewed/replaced

3.0.1
* Fixed a bug where memory streams closed early before submitting certificates to ACM in Management Add jobs
* Fixed a bug where ACM tags would be "set" even if none where entered, preventing a certificate from being added without tags

3.0.0
* Upgrade to AWS SDK v4
  * All interactions with AWS now target the Region specified in `Store Path` with no "default" Region considered
* Support for full Role ARN as a Destination account identity. This enables usage in environments with non-standard ARNs
* Support for credential profiles when using Default SDK auth using `[profilename]` prefix with the Role ARN in `Client Machine` field
* Updated documentation for new store type `AWS-ACM-v3`
  * Documentation on AWS authentication specifics moved to [aws-auth-library](https://github.com/Keyfactor/aws-auth-library)
* Updated naming scheme of project files, namespaces, and binaries
* Removed `AWS-ACM` store type
  * Major feature changes in 3.0 require a new store type definition
* Removed `AwsCerManO` and `AwsCerManA` types
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
