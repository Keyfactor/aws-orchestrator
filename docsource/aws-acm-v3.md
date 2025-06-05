## Overview

The AWS Certificate Manager v3 Store Type provides enhanced authentication options for managing certificates in ACM.
Each defined Certificate Store of this type targes a specific AWS Region with a specific Destination account in mind.
Therefore each Certificate Store instance is intended to represent a single Role's certificates in a single AWS Region.

Some authentication configurations do not adhere strictly to this, so when using the various methods offered in the Default SDK auth option,
a full understanding of how permissions work in AWS is recommended.
In most scenarios using the Default SDK option, the Assume Role flag should also be set to avoid confusion, and use the Role ARN in the `Client Machine` field as the Destination account.

## Requirements

Configuring authentication with AWS requires understanding how the authentication flow works.
Depending on the intended authentication method, the required configuration in AWS may require one or more Roles with the correct permissions.

The intended Destination account, usually the Role ARN specified in the `Client Machine` field, which is the final identity used to perform the actual work in AWS ACM, needs the following permissions:

~~~
Inventory required actions:

    "acm:ListCertificates",
    "acm:GetCertificate",
    "acm:ListTagsForCertificate"

 Management required actions:

    "acm:DeleteCertificate",
    "acm:DescribeCertificate",
    "acm:ImportCertificate"
~~~

## Global Store Type Section

The latest version of the Store Type supporting ACM (AWS Certificate Manager) is `AWS-ACM-v3`.
Previous store types are no longer supported and should be migrated to the new Store Type definition.
When migrating to the `AWS-ACM-v3` type please note that field usage has changed and does not map over directly.

> [!WARNING]
> When creating Certificate Stores, all available Secret type fields need to have a value set for them, even if that is "No Value".
> Failing to set these Secret fields, even when not in use, causes errors that may require database access to fix.
