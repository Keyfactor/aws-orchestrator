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

The AWS Certificate Manager (ACM) Universal Orchestrator extension manages SSL/TLS certificates
held in [AWS Certificate Manager](https://docs.aws.amazon.com/acm/latest/userguide/acm-overview.html)
from Keyfactor Command. It supports **Inventory**, **Management** (add / renew / remove) and
**cross-account Discovery**.

Each certificate store instance maps to **exactly one AWS region in exactly one AWS account**.
Certificate aliases within a store are full ACM certificate ARNs, for example
`arn:aws:acm:us-east-1:123456789012:certificate/12345678-1234-1234-1234-123456789012`.

Authentication is handled by the shared [aws-auth-library](https://github.com/Keyfactor/aws-auth-library),
so the extension supports the AWS Default SDK credential chain (including EC2 instance roles and
named credential profiles), IAM User access keys, and OAuth 2.0 / OIDC federation against a third-party
identity provider. The Default SDK and IAM User methods can optionally finish by performing an
**Assume Role** into the account that actually holds the certificates; the OAuth method always
assumes a role, via `sts:AssumeRoleWithWebIdentity`.

> [!IMPORTANT]
> **`Store Path` accepts two formats.**
>
> | Format | Example | Where the Role ARN comes from |
> |---|---|---|
> | Region only (legacy) | `us-east-1` | the `Client Machine` field |
> | `<roleArn>\|<region>` (self-contained) | `arn:aws:iam::123456789012:role/KeyfactorACMDiscoveryRole\|us-east-1` | the `Store Path` itself; `Client Machine` is informational only |
>
> Stores created by cross-account Discovery always use the self-contained form. Both formats are
> supported indefinitely — existing region-only stores do not need to be changed.

> [!WARNING]
> **Migrating from `AWS-ACM`, `AwsCerManO`, or `AwsCerManA`:** those store types are **not**
> compatible with `AWS-ACM-v3` and must be recreated. See
> [Migrating from an older ACM store type](#migrating-from-an-older-acm-store-type).

### What this extension does and does not do

| Capability | Supported | Notes |
|---|---|---|
| Inventory certificates | ✅ | Includes certificates in `ISSUED`, `INACTIVE`, `EXPIRED` and `REVOKED` states |
| Add (import) a certificate | ✅ | PFX is split into certificate, private key and chain, and re-encoded as PEM for `ImportCertificate` |
| Renew / replace a certificate | ✅ | **Only for certificates whose ACM `Type` is `IMPORTED`** |
| Remove a certificate | ✅ | **Only for certificates whose ACM `Type` is `IMPORTED`** |
| Cross-account Discovery | ✅ | Enumerates accounts via AWS Organizations and probes each region |
| Read existing ACM tags | ✅ | Surfaced on the `ACM Tags` entry parameter during Inventory |
| Set ACM tags on a **new** import | ✅ | `Name1=Value1,Name2=Value2` format |
| Change ACM tags on **renewal** | ❌ | AWS does not accept tags when reimporting a certificate; see [ACM tags](#acm-tags) |
| Reenrollment / on-device key generation | ❌ | ACM does not expose a CSR-generation API for imported certificates |

> [!NOTE]
> **Amazon-issued, Private CA and ACME certificates are inventory-only.** Their ACM `Type` is not
> `IMPORTED`, so ACM rejects any attempt to replace or delete them through this API path. The
> Inventory job marks them `PrivateKeyEntry = false` so Command does not offer a renewal that is
> guaranteed to fail. See [Certificate types and renewal](#certificate-types-and-renewal).

### Documentation

- [How AWS authentication works in this extension (aws-auth-library)](https://github.com/Keyfactor/aws-auth-library)
- [AWS Region codes](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/Concepts.RegionsAndAvailabilityZones.html)
- [AWS Certificate Manager user guide](https://docs.aws.amazon.com/acm/latest/userguide/acm-overview.html)
- [ACM service quotas and API request rates](https://docs.aws.amazon.com/acm/latest/userguide/acm-limits.html)

## Compatibility

This integration is compatible with Keyfactor Universal Orchestrator version 10.1 and later.

## Support

The AWS Certificate Manager (ACM) Universal Orchestrator extension is supported by Keyfactor. If you require support for any issues or have feature request, please open a support ticket by either contacting your Keyfactor representative or via the Keyfactor Support Portal at https://support.keyfactor.com.

> If you want to contribute bug fixes or additional enhancements, use the **[Pull requests](../../pulls)** tab.

## Requirements & Prerequisites

Before installing the AWS Certificate Manager (ACM) Universal Orchestrator extension, we recommend that you install [kfutil](https://github.com/Keyfactor/kfutil). Kfutil is a command-line tool that simplifies the process of creating store types, installing extensions, and instantiating certificate stores in Keyfactor Command.

### Migrating from an older ACM store type

Field usage changed in v3 and does **not** map across directly:

| Older store types (`AWS-ACM`, `AwsCerManO`, `AwsCerManA`) | `AWS-ACM-v3` |
|---|---|
| `ServerUsername` / `ServerPassword` carried credentials | No longer used. Dedicated IAM and OAuth fields replace them |
| AWS Region supplied as an Entry Parameter | Region is part of `Store Path`; the Entry Parameter is gone |
| `Client Machine` held a host or short name | `Client Machine` holds the **full Role ARN** used for Assume Role calls |
| One store could span regions | One store = **one region**; define a separate store per region |

Migration steps:

1. Create the `AWS-ACM-v3` store type (once per Command instance).
2. Recreate each certificate store against the new type.
3. Run an Inventory job on each new store to begin tracking those certificates again.
4. Delete the deprecated stores and store types once nothing depends on them.

There is no automated migration utility.

### Key concept — the destination identity

> **Whichever identity the extension ends up operating as — the assumed Role ARN, or the
> *originating* identity when no Assume Role is performed — is the *destination identity*, and it
> is what must hold the ACM permissions** (see
> [Example IAM policies](#example-iam-policies-for-the-destination-identity)).
>
> For every Assume-Role method the **originating** identity additionally needs `sts:AssumeRole` on
> the target role, and the target role's **trust policy** must allow that originating principal
> (see [Example trust policies](#example-trust-policies)).

Getting this distinction wrong is the most common cause of `AccessDenied` from a store that looks
correctly configured. Work out which identity is the destination *first*, then attach the ACM
permissions there.

### Common requirements (all methods)

- **Region** — supplied through `Store Path`, either as the whole value (`us-east-1`) or as the
  part after the pipe (`<roleArn>|us-east-1`). Must be the region the certificates live in.
- **Client Machine** — the full Role ARN to assume, e.g.
  `arn:aws:iam::123456789012:role/Keyfactor-ACM-Role`. Prefix with `[profile-name]` to select a
  named credential profile under Default SDK auth. Informational only for stores that use the
  self-contained `<roleArn>|<region>` store path.
- **ACM permissions on the destination identity** — see
  [Example IAM policies](#example-iam-policies-for-the-destination-identity).
- **One store per region per account** — additional regions need their own stores.

> [!WARNING]
> When creating certificate stores, **every `Secret`-type field must be given a value**, even if
> that value is "No Value". The secret fields on this store type are `OAuthClientId`,
> `OAuthClientSecret`, `IAMUserAccessKey` and `IAMUserAccessSecret`. Leaving an unused secret field
> empty causes errors that may require database access to correct.

### Method configuration

Choose **one** method per certificate store and set the auth toggles you are **not** using to
`false`.

#### Default SDK (no assume)

- `UseDefaultSdkAuth` = `true`, `DefaultSdkAssumeRole` = `false`
- `Client Machine` is ignored for credential resolution

The orchestrator host's **ambient AWS identity** is the destination and must hold the ACM
permissions directly. The AWS SDK resolves it in the usual order: `AWS_*` environment variables,
the `[default]` profile in the shared credentials file, then the EC2 instance role.

#### Default SDK + Assume Role

- `UseDefaultSdkAuth` = `true`, `DefaultSdkAssumeRole` = `true`
- `Client Machine` = destination Role ARN

The ambient identity is the **originating** account and performs `sts:AssumeRole` into the
`Client Machine` role. The ambient identity needs `sts:AssumeRole`; the destination role's trust
policy must allow it and the destination role holds the ACM permissions.

> This is the recommended Default SDK configuration. Without the assume flag it is easy to lose
> track of which identity is actually reaching ACM.

#### Credential Profile (± Assume Role)

Prefix `Client Machine` with a profile name, e.g.
`[myprofile]arn:aws:iam::123456789012:role/Keyfactor-ACM-Role`, and set `UseDefaultSdkAuth` = `true`.
The named profile must exist in the shared AWS credentials file **on the orchestrator host**.

- **No assume** (`DefaultSdkAssumeRole` = `false`): the profile identity is the destination and needs
  the ACM permissions directly.
- **With assume** (`DefaultSdkAssumeRole` = `true`): the profile is the originating identity and
  assumes the Role ARN that follows the prefix.

#### IAM User (Access Key + Secret)

- `UseIAM` = `true`
- `IAMUserAccessKey` (e.g. `AKIAIOSFODNN7EXAMPLE`)
- `IAMUserAccessSecret` (e.g. `wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY`)
- `Client Machine` = destination Role ARN to assume

**AWS setup:** the IAM user needs `sts:AssumeRole` on the destination role; that role's trust policy
must allow the IAM user (see [Assume-Role trust](#assume-role--iam-user-default-sdk--assume-or-credential-profile--assume));
the role holds the [ACM permissions](#example-iam-policies-for-the-destination-identity).

> AWS does not support programmatic access for AWS SSO accounts. This must be a standard IAM User
> with an Access Key credential type.

#### IAM User + ExternalId

As **IAM User**, plus set `ExternalId`. The destination role's trust policy must include a matching
`sts:ExternalId` condition (see [Assume Role with ExternalId](#assume-role-with-externalid)). Use
this when the role owner requires a shared secret to prevent the
[confused-deputy problem](https://docs.aws.amazon.com/IAM/latest/UserGuide/confused-deputy.html).

#### OAuth 2.0 (OIDC federation)

- `UseOAuth` = `true`
- `OAuthClientId`, `OAuthClientSecret`
- `OAuthUrl` — the identity provider's **token endpoint**, e.g.
  `https://your-tenant.okta.com/oauth2/default/v1/token`
- `OAuthScope`, `OAuthGrantType` (`client_credentials`)
- `Client Machine` = destination Role ARN (a federated web-identity role)

The extension requests an OAuth token and calls `sts:AssumeRoleWithWebIdentity` on the destination
role.

**AWS setup:** create an IAM **OIDC identity provider** for the token issuer, and set the destination
role's trust policy to allow `sts:AssumeRoleWithWebIdentity` for that provider with the appropriate
`aud` (and optionally `sub`) conditions — see [OAuth web-identity trust](#oauth--web-identity-trust).

> [!NOTE]
> OAuth provider constraints: the client id/secret must be accepted as an HTTP **Basic** auth header
> (not a POST body); **DPoP must be disabled** on the app; and the token lifetime must be
> **≤ 12 hours**, because STS caps `AssumeRoleWithWebIdentity` `DurationSeconds` at 43200 and the
> session duration is derived from the token's `expires_in`.

### Setting up AWS Authentication (Examples)

> [!NOTE]
> Several different options are offered for authenticating with AWS. Documentation for how these
> options work is maintained in the [aws-auth-library](https://github.com/Keyfactor/aws-auth-library)
> repository.

The following examples show potential configurations for Roles in AWS with different selected
authentication methods. Your configuration steps may differ depending on the specific requirements
of your use case. In every case the **destination identity** must hold the ACM permissions, and
every Assume-Role method additionally requires the originating identity to have `sts:AssumeRole` and
the target role's trust policy to allow that principal.

<details>
<summary>EC2 instance credentials using Default SDK and Assume Role</summary>

Select the `Use Default SDK Auth` option to allow the integration to load EC2 instance credentials.

If the EC2 Role assigned to the instance is itself the destination identity to use with ACM, no
additional Role needs to be configured. If it is only to be used initially, and a separate Role ARN
is designated as the destination in the `Client Machine` field, also select
`Assume new Role using Default SDK Auth`.

**AWS Setup**

_Note: in this scenario the extension must be running inside an EC2 instance._

1. Assign or note the existing IAM Role assigned to the EC2 instance.
   [Found in EC2 here](docsource/images/ec2-instance-iam-role.gif).
2. If Assume Role is used, ensure a
   [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html)
   on the destination role allows the instance role to assume it.
   [Example](docsource/images/ec2-role-arn-trust-relationship.gif).
3. Verify the destination role's permissions match
   [the ACM requirements](#example-iam-policies-for-the-destination-identity).

</details>

<details>
<summary>OAuth OIDC Identity Provider (Okta example)</summary>

Select the `Use OAuth` option for a certificate store to use an OAuth Identity Provider.

**AWS Setup**

1. A third-party
   [Identity Provider](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_providers_create_oidc.html)
   similar to [this](docsource/images/AWSIdentityProvider.gif) needs to be set up in AWS.
2. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html)
   needs to be created for use with your Identity Provider.
   [Example](docsource/images/AWSRole1.gif).
3. Ensure the
   [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html)
   is set up for that role with the Identity Provider, allowing `sts:AssumeRoleWithWebIdentity` with
   the appropriate `aud` (and optionally `sub`) conditions.
   [Example](docsource/images/AWSRole2.gif), and see
   [OAuth web-identity trust](#oauth--web-identity-trust).
4. Verify the permissions match
   [the ACM requirements](#example-iam-policies-for-the-destination-identity).

**Okta Setup**

1. Ensure your Authorization Server is set up in Okta. Here is a
   [sample](docsource/images/OktaSampleAuthorizationServer.gif).
2. Ensure the appropriate scopes are set up in Okta. Here is a
   [sample](docsource/images/OktaSampleAuthorizationServer-scopes.gif).
3. Set up an Okta App with similar settings to [this](docsource/images/OktaApp1.gif) and
   [this](docsource/images/OktaApp2.gif). The app must send the client id/secret as a **Basic auth
   header**, have **DPoP disabled**, and issue tokens with a lifetime **≤ 12 hours**.

</details>

<details>
<summary>IAM User credentials to Assume Role</summary>

Select the `Use IAM` option for a certificate store to use an IAM User credential.

**AWS Setup**

1. An [AWS Role](https://docs.aws.amazon.com/IAM/latest/UserGuide/id_roles_create_for-user.html) to
   assume with your IAM User needs to be created, and set as `Client Machine`.
2. Ensure a
   [Trust Relationship](https://docs.aws.amazon.com/directoryservice/latest/admin-guide/edit_trust.html)
   is set up for that role that allows the IAM User.
   [Example](docsource/images/AssumeRoleTrust.gif). If using the ExternalId modifier, add a matching
   `sts:ExternalId` condition.
3. AWS does not support programmatic access for AWS SSO accounts. The account used here must be a
   [standard AWS IAM User](docsource/images/UserAccount.gif) with an Access Key credential type.
4. Verify the permissions match
   [the ACM requirements](#example-iam-policies-for-the-destination-identity).

</details>

---

### Example IAM policies for the destination identity

These permissions belong on the **destination identity** (the assumed Role ARN, or the
ambient/profile identity when no Assume Role is performed). The following examples are intended as
**copy/adapt templates** — scope `Resource` down to specific certificate ARNs where your policy
standards require it.

#### Example 1: Inventory only (read-only)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AcmInventory",
      "Effect": "Allow",
      "Action": [
        "acm:ListCertificates",
        "acm:GetCertificate",
        "acm:ListTagsForCertificate"
      ],
      "Resource": "*"
    }
  ]
}
```

`acm:ListCertificates` does not operate on a single resource and must be granted on `*`. The other
two actions can be scoped to `arn:aws:acm:<region>:<account-id>:certificate/*`.

#### Example 2: Inventory + Management (add, renew, remove)

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "AcmList",
      "Effect": "Allow",
      "Action": "acm:ListCertificates",
      "Resource": "*"
    },
    {
      "Sid": "AcmInventoryAndManage",
      "Effect": "Allow",
      "Action": [
        "acm:GetCertificate",
        "acm:ListTagsForCertificate",
        "acm:DescribeCertificate",
        "acm:ImportCertificate",
        "acm:DeleteCertificate"
      ],
      "Resource": "arn:aws:acm:<region>:<account-id>:certificate/*"
    },
    {
      "Sid": "AcmTagOnImport",
      "Effect": "Allow",
      "Action": "acm:AddTagsToCertificate",
      "Resource": "arn:aws:acm:<region>:<account-id>:certificate/*"
    }
  ]
}
```

> [!NOTE]
> `acm:ImportCertificate` covers both new imports and renewals — a renewal is an `ImportCertificate`
> call that supplies the existing `CertificateArn`.
>
> Tags supplied on an import are governed by `acm:ImportCertificate` itself: AWS documents the
> `aws:RequestTag/${TagKey}` and `aws:TagKeys` condition keys on that action and lists no dependent
> action for it. The `AcmTagOnImport` statement above is therefore belt-and-braces — include it if
> your policy standards prefer the tagging action to be explicit, or if the same role also needs to
> tag certificates outside of an import. Omit it otherwise.

#### Example 3: Cross-account Discovery

Discovery uses two different identities. The **management identity** enumerates the organization:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "EnumerateOrgAccounts",
      "Effect": "Allow",
      "Action": "organizations:ListAccounts",
      "Resource": "*"
    },
    {
      "Sid": "AssumeDiscoveryRoleInMembers",
      "Effect": "Allow",
      "Action": "sts:AssumeRole",
      "Resource": "arn:aws:iam::*:role/KeyfactorACMDiscoveryRole"
    }
  ]
}
```

The **per-account discovery role** (`KeyfactorACMDiscoveryRole` by default) needs only enough to
answer "does this region hold any certificates?":

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Sid": "ProbeRegionsForCertificates",
      "Effect": "Allow",
      "Action": [
        "acm:ListCertificates",
        "ec2:DescribeRegions"
      ],
      "Resource": "*"
    }
  ]
}
```

`ec2:DescribeRegions` is only used when the region list is left blank or set to `*`, to narrow the
scan to the regions each account actually has enabled. It can be omitted if you always supply an
explicit region list. See [Cross-account Discovery](#cross-account-discovery).

---

### Example trust policies

Every Assume-Role method requires a **trust policy** on the destination role that allows the
originating principal to assume it. (Default SDK *no-assume* and Credential Profile *no-assume* need
no trust policy — the identity is used directly.)

#### Assume Role — IAM User, Default SDK + Assume, or Credential Profile + Assume

`<<ORIGINATING PRINCIPAL ARN>>` is the IAM user ARN (IAM User method) or the ambient/profile identity
ARN (Default SDK / Credential Profile + Assume). For an EC2 instance role this is the role ARN, not
the instance profile ARN.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": "<<ORIGINATING PRINCIPAL ARN>>" },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

#### Assume Role with ExternalId

Add a matching `sts:ExternalId` condition; the store's `ExternalId` field must equal it.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": "<<ORIGINATING PRINCIPAL ARN>>" },
      "Action": "sts:AssumeRole",
      "Condition": { "StringEquals": { "sts:ExternalId": "<<your-external-id>>" } }
    }
  ]
}
```

#### OAuth — web-identity trust

The destination role for the OAuth method must trust the OIDC provider you registered for your token
issuer. Condition on the token `aud` (audience) and optionally `sub` — for `client_credentials`,
`sub` is typically the client id.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "Federated": "arn:aws:iam::<account-id>:oidc-provider/<issuer-host>/<issuer-path>" },
      "Action": "sts:AssumeRoleWithWebIdentity",
      "Condition": {
        "StringEquals": {
          "<issuer-host>/<issuer-path>:aud": "<<audience>>",
          "<issuer-host>/<issuer-path>:sub": "<<client-id>>"
        }
      }
    }
  ]
}
```

#### Cross-account Discovery role trust

Each member account's `KeyfactorACMDiscoveryRole` must trust the management identity that performs
the assume.

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": { "AWS": "<<MANAGEMENT ROLE OR USER ARN>>" },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

---

### Cross-account Discovery

A Discovery job walks an AWS Organization and reports back one certificate store per
account-and-region that actually holds certificates. Discovered stores use the self-contained
`<roleArn>|<region>` store path.

**How it resolves accounts and regions**

1. Base credentials are resolved from the job's settings, then used to call
   `organizations:ListAccounts`.
2. For each active account, the job assumes `arn:aws:iam::<account-id>:role/<DiscoveryRoleName>`.
3. For each candidate region it calls `acm:ListCertificates`; a region with at least one certificate
   becomes a discovered store.

If `organizations:ListAccounts` is denied, the job logs a warning and **falls back to
single-account discovery** using the base credentials — it does not fail.

**Discovery dialog field mapping**

The standard Command Discovery dialog has no AWS-specific fields, so settings are carried on the
generic dialog fields:

| Dialog field | Setting | Default / meaning |
|---|---|---|
| **Directories to search** | Management-account Role ARN to assume | Falls back to `Client Machine` if blank |
| **Directories to ignore** | Account IDs to skip | Comma-separated |
| **File name patterns to match** | `DiscoveryRoleName` — the IAM role name assumed in each member account | `KeyfactorACMDiscoveryRole` |
| **File extensions** | Regions to scan, comma-separated | Blank or `*` = every region in the base region's partition |

> [!NOTE]
> Because the dialog carries no auth-method fields, an operator-launched Discovery job arrives with
> all auth toggles `false`. The job then **defaults to Default SDK auth**, and enables Assume Role
> automatically when "Directories to search" contains something that looks like an IAM role ARN
> (`arn:aws:iam:`, `arn:aws-us-gov:iam:` or `arn:aws-cn:iam:`).

**Region scanning behaviour**

- **Explicit list** (e.g. `us-east-1,us-east-2`) — used verbatim, no auto-detection.
- **Blank or `*`** — every region in the base region's partition, narrowed per account by
  `ec2:DescribeRegions` to the regions that account has actually enabled.

There is no hard-coded list of "common" regions. Scanning every region in a large organization is
one `ListCertificates` call per account per region, so prefer an explicit list when you know where
your certificates are.

---

### Operational behaviour and limitations

#### Certificate types and renewal

ACM reports a `Type` for every certificate. Only `IMPORTED` certificates can be replaced or deleted
through this extension:

| ACM `Type` | Origin | Inventory | Renew / Remove |
|---|---|---|---|
| `IMPORTED` | Uploaded by you or by Command | ✅ | ✅ |
| `AMAZON_ISSUED` | Issued by ACM's public CA | ✅ | ❌ |
| `PRIVATE` | Issued by an AWS Private CA | ✅ | ❌ |
| *(ACME origin)* | Issued through AWS's public ACME endpoint | ✅ | ❌ |

The Inventory job sets `PrivateKeyEntry` from this `Type`, so Command only offers renewal on
certificates where it can actually succeed.

> [!NOTE]
> **ACME-issued certificates have no subject DN.** AWS's public ACME CA issues SAN-only certificates
> with an empty subject, and ACM itself reports `Subject: ""`. Command derives `IssuedDN` / `IssuedCN`
> by parsing the uploaded certificate, so these appear with a blank common name and are findable
> only by SAN. This is a property of the certificates, not something the extension can correct.

#### ACM tags

The `ACM Tags` entry parameter uses comma-separated `Name1=Value1,Name2=Value2` pairs.

- **On a new import**, tags are applied with the certificate.
- **On a renewal**, tags are **not** applied. This is an AWS constraint, not an extension choice:
  the `ImportCertificate` API documents that tags cannot be applied when *reimporting* a certificate.
  Applying them would take additional calls that could leave the certificate in an inconsistent
  state if one of them failed, so the extension skips them.
- **Existing tags survive a renewal** — they are simply left alone.
- Inventory reads existing tags back onto the entry parameter.

#### Inventory request behaviour

ACM's `ListCertificates` filters are **inclusion** filters: anything not explicitly named is absent
from the result, with no error. The extension therefore requests every key algorithm and every key
pair origin that the AWS SDK defines, which is what makes SAN-less and ACME-origin certificates
visible. If a future AWS release adds a new algorithm or origin, certificates using it will be
silently missing until the extension is updated.

Inventory makes two additional API calls per certificate (`GetCertificate` and
`ListTagsForCertificate`). ACM documents a request rate of roughly 10/second for each, so a store
with several thousand certificates will take a corresponding amount of time; this is an ACM
constraint rather than an extension one.

#### Troubleshooting

| Symptom | Likely cause |
|---|---|
| `AccessDenied` from a seemingly correct store | ACM permissions are on the originating identity instead of the [destination identity](#key-concept--the-destination-identity) |
| Assume Role fails | Destination role's [trust policy](#example-trust-policies) does not allow the originating principal, or `ExternalId` does not match |
| Store errors that need database access to fix | A `Secret`-type field was left empty — set unused secret fields to "No Value" |
| Discovery finds no stores | `organizations:ListAccounts` denied (check the log for the fallback warning), or the per-account role name does not match `DiscoveryRoleName` |
| Renewal fails on a specific certificate | The certificate's ACM `Type` is not `IMPORTED` — see [Certificate types and renewal](#certificate-types-and-renewal) |
| OAuth token rejected by STS | Token lifetime exceeds 12 hours, DPoP is enabled, or the provider requires credentials in the POST body rather than a Basic header |

## AWS-ACM-v3 Certificate Store Type

To use the AWS Certificate Manager (ACM) Universal Orchestrator extension, you **must** create the AWS-ACM-v3 Certificate Store Type. This only needs to happen _once_ per Keyfactor Command instance.

The `AWS-ACM-v3` store type represents **one AWS region inside one AWS account**. Certificate
aliases within the store are full ACM certificate ARNs, and the region plus the identity to operate
as are both carried by the store's `Client Machine` and `Store Path` fields.

Define a separate certificate store for every region you hold certificates in; a single store cannot
span regions.

##### Store Path and Client Machine

`Store Path` accepts two formats, and which one you use decides where the Role ARN comes from:

| Format | Example `Store Path` | Role ARN comes from | `Client Machine` |
|---|---|---|---|
| Region only (legacy) | `us-east-1` | the `Client Machine` field | **Required** — full Role ARN |
| `<roleArn>\|<region>` | `arn:aws:iam::123456789012:role/Keyfactor-ACM-Role\|us-east-1` | the `Store Path` itself | Informational only |

Stores created by cross-account Discovery always use the second form. Both are supported
indefinitely — existing region-only stores do not need to be rewritten.

`Client Machine` may also carry a **credential profile prefix** when using Default SDK auth, e.g.
`[myprofile]arn:aws:iam::123456789012:role/Keyfactor-ACM-Role`. The named profile must exist in the
shared AWS credentials file on the orchestrator host.

##### Which identity actually reaches ACM

Every authentication method resolves to a single **destination identity** — the assumed Role ARN
when the store performs an Assume Role, or the originating identity (ambient SDK credentials or the
named profile) when it does not. **The destination identity is the one that needs the ACM
permissions.**

For every Assume-Role method the **originating** identity additionally needs `sts:AssumeRole` on the
target role, and that role's trust policy must allow the originating principal.

> [!TIP]
> When using Default SDK auth, also set `DefaultSdkAssumeRole` and put the destination Role ARN in
> `Client Machine`. Without it, the identity reaching ACM is whatever the SDK happened to resolve on
> the orchestrator host, which is easy to misconfigure and hard to diagnose.

#### Store type version and required field values

The latest Store Type supporting ACM (AWS Certificate Manager) is `AWS-ACM-v3`. The earlier
`AWS-ACM`, `AwsCerManO` and `AwsCerManA` types are no longer supported and must be migrated —
field usage changed and does **not** map across directly, so stores have to be recreated rather than
converted. See
[Migrating from an older ACM store type](#migrating-from-an-older-acm-store-type).

> [!WARNING]
> When creating Certificate Stores, **all available Secret type fields need to have a value set for
> them, even if that is "No Value"**. The secret fields on this store type are `OAuthClientId`,
> `OAuthClientSecret`, `IAMUserAccessKey` and `IAMUserAccessSecret`. Failing to set these Secret
> fields, even when not in use, causes errors that may require database access to fix.

> [!NOTE]
> `Supports Reenrollment` is unchecked and cannot be enabled: ACM provides no CSR-generation API for
> imported certificates, so there is no on-device key generation path to implement.

#### AWS Certificate Manager v3 Requirements

Grant the following ACM actions to the **destination identity**. Copy/adapt JSON policy documents —
including the separate policies needed for cross-account Discovery — are in
[Example IAM policies for the destination identity](#example-iam-policies-for-the-destination-identity),
and matching trust policies are in [Example trust policies](#example-trust-policies).

| Job type | Required actions | Notes |
|---|---|---|
| **Inventory** | `acm:ListCertificates`<br>`acm:GetCertificate`<br>`acm:ListTagsForCertificate` | `ListCertificates` cannot be scoped to a resource and must be granted on `*` |
| **Management** (add / renew / remove) | `acm:DescribeCertificate`<br>`acm:ImportCertificate`<br>`acm:DeleteCertificate` | Renewal is an `ImportCertificate` call supplying the existing ARN, so no extra action is needed |
| **Management** with the `ACM Tags` entry parameter | *(none beyond `acm:ImportCertificate`)* | Tags on import are governed by `acm:ImportCertificate`, which carries the `aws:RequestTag`/`aws:TagKeys` condition keys. Add `acm:AddTagsToCertificate` only if your policy standards require it to be explicit |
| **Discovery** (management identity) | `organizations:ListAccounts`<br>`sts:AssumeRole` | If `ListAccounts` is denied, Discovery logs a warning and falls back to single-account discovery |
| **Discovery** (per-account role) | `acm:ListCertificates`<br>`ec2:DescribeRegions` | `DescribeRegions` is only used when the region list is blank or `*` |

A store that only needs Inventory does not need the Management actions; grant the narrowest set the
store's jobs actually use.

#### Supported Operations

| Operation    | Is Supported |
|--------------|--------------|
| Add          | ✅ Checked |
| Remove       | ✅ Checked |
| Discovery    | ✅ Checked |
| Reenrollment | 🔲 Unchecked |
| Create       | 🔲 Unchecked |

#### Store Type Creation

##### Using kfutil:
`kfutil` is a custom CLI for the Keyfactor Command API and can be used to create certificate store types.
For more information on [kfutil](https://github.com/Keyfactor/kfutil) check out the [docs](https://github.com/Keyfactor/kfutil?tab=readme-ov-file#quickstart)

   <details><summary>Click to expand AWS-ACM-v3 kfutil details</summary>

   ##### Using online definition from GitHub:
   This will reach out to GitHub and pull the latest store-type definition
   ```shell
   # AWS Certificate Manager v3
   kfutil store-types create AWS-ACM-v3
   ```

   ##### Offline creation using integration-manifest file:
   If required, it is possible to create store types from the [integration-manifest.json](./integration-manifest.json) included in this repo.
   You would first download the [integration-manifest.json](./integration-manifest.json) and then run the following command
   in your offline environment.
   ```shell
   kfutil store-types create --from-file integration-manifest.json
   ```
   </details>

#### Manual Creation
Below are instructions on how to create the AWS-ACM-v3 store type manually in
the Keyfactor Command Portal

   <details><summary>Click to expand manual AWS-ACM-v3 details</summary>

   Create a store type called `AWS-ACM-v3` with the attributes in the tables below:

   ##### Basic Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Name | AWS Certificate Manager v3 | Display name for the store type (may be customized) |
   | Short Name | AWS-ACM-v3 | Short display name for the store type |
   | Capability | AWS-ACM-v3 | Store type name orchestrator will register with. Check the box to allow entry of value |
   | Supports Add | ✅ Checked | Indicates that the Store Type supports Management Add |
   | Supports Remove | ✅ Checked | Indicates that the Store Type supports Management Remove |
   | Supports Discovery | ✅ Checked | Indicates that the Store Type supports Discovery |
   | Supports Reenrollment | 🔲 Unchecked | Indicates that the Store Type supports Reenrollment |
   | Supports Create | 🔲 Unchecked | Indicates that the Store Type supports store creation |
   | Needs Server | 🔲 Unchecked | Determines if a target server name is required when creating store |
   | Blueprint Allowed | ✅ Checked | Determines if store type may be included in an Orchestrator blueprint |
   | Uses PowerShell | 🔲 Unchecked | Determines if underlying implementation is PowerShell |
   | Requires Store Password | 🔲 Unchecked | Enables users to optionally specify a store password when defining a Certificate Store. |
   | Supports Entry Password | 🔲 Unchecked | Determines if an individual entry within a store can have a password. |

   The Basic tab should look like this:

   ![AWS-ACM-v3 Basic Tab](docsource/images/AWS-ACM-v3-basic-store-type-dialog.svg)

   ##### Advanced Tab
   | Attribute | Value | Description |
   | --------- | ----- | ----- |
   | Supports Custom Alias | Optional | Determines if an individual entry within a store can have a custom Alias. |
   | Private Key Handling | Required | This determines if Keyfactor can send the private key associated with a certificate to the store. |
   | PFX Password Style | Default | 'Default' - PFX password is randomly generated, 'Custom' - PFX password may be specified when the enrollment job is created (Requires the Allow Custom Password application setting to be enabled.) |

   The Advanced tab should look like this:

   ![AWS-ACM-v3 Advanced Tab](docsource/images/AWS-ACM-v3-advanced-store-type-dialog.svg)

   > For Keyfactor **Command versions 24.4 and later**, a Certificate Format dropdown is available with PFX and PEM options. Ensure that **PFX** is selected, as this determines the format of new and renewed certificates sent to the Orchestrator during a Management job. Currently, all Keyfactor-supported Orchestrator extensions support only PFX.

   ##### Custom Fields Tab
   Custom fields operate at the certificate store level and are used to control how the orchestrator connects to the remote target server containing the certificate store to be managed. The following custom fields should be added to the store type:

   | Name | Display Name | Description | Type | Default Value/Options | Required |
   | ---- | ------------ | ---- | --------------------- | -------- | ----------- |
   | UseDefaultSdkAuth | Use Default SDK Auth | A switch to enable the store to use Default SDK credentials | Bool | false | ✅ Checked |
   | DefaultSdkAssumeRole | Assume new Role using Default SDK Auth | A switch to enable the store to assume a new Role when using Default SDK credentials | Bool | false | 🔲 Unchecked |
   | UseOAuth | Use OAuth 2.0 Provider | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS | Bool | false | ✅ Checked |
   | OAuthScope | OAuth Scope | This is the OAuth Scope needed for Okta OAuth, defined in Okta | String |  | 🔲 Unchecked |
   | OAuthGrantType | OAuth Grant Type | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` | String | client_credentials | 🔲 Unchecked |
   | OAuthUrl | OAuth Url | The token endpoint URL of the OAuth 2.0 / OIDC identity provider, for example https://your-tenant.okta.com/oauth2/default/v1/token | String | https://***/oauth2/default/v1/token | 🔲 Unchecked |
   | OAuthClientId | OAuth Client ID | The Client ID for OAuth. | Secret |  | 🔲 Unchecked |
   | OAuthClientSecret | OAuth Client Secret | The Client Secret for OAuth. | Secret |  | 🔲 Unchecked |
   | UseIAM | Use IAM User Auth | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS | Bool | false | ✅ Checked |
   | IAMUserAccessKey | IAM User Access Key | The AWS Access Key for an IAM User | Secret |  | 🔲 Unchecked |
   | IAMUserAccessSecret | IAM User Access Secret | The AWS Access Secret for an IAM User. | Secret |  | 🔲 Unchecked |
   | ExternalId | sts:ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls | String |  | 🔲 Unchecked |
   | DiscoveryRoleName | Discovery Role Name | The IAM role name that exists in each target account. Used during cross-account Discovery to construct the Role ARN for cross-account access (arn:aws:iam::<account-id>:role/<this-value>). During a Discovery job this is supplied via the 'File name patterns to match' dialog field; this store property documents and defaults the value. | String | KeyfactorACMDiscoveryRole | 🔲 Unchecked |

   The Custom Fields tab should look like this:

   ![AWS-ACM-v3 Custom Fields Tab](docsource/images/AWS-ACM-v3-custom-fields-store-type-dialog.svg)

   ###### Use Default SDK Auth
   A switch to enable the store to use Default SDK credentials

   ![AWS-ACM-v3 Custom Field - UseDefaultSdkAuth](docsource/images/AWS-ACM-v3-custom-field-UseDefaultSdkAuth-dialog.svg)
   ![AWS-ACM-v3 Custom Field - UseDefaultSdkAuth](docsource/images/AWS-ACM-v3-custom-field-UseDefaultSdkAuth-validation-options-dialog.svg)


   ###### Assume new Role using Default SDK Auth
   A switch to enable the store to assume a new Role when using Default SDK credentials

   ![AWS-ACM-v3 Custom Field - DefaultSdkAssumeRole](docsource/images/AWS-ACM-v3-custom-field-DefaultSdkAssumeRole-dialog.svg)
   ![AWS-ACM-v3 Custom Field - DefaultSdkAssumeRole](docsource/images/AWS-ACM-v3-custom-field-DefaultSdkAssumeRole-validation-options-dialog.svg)


   ###### Use OAuth 2.0 Provider
   A switch to enable the store to use an OAuth provider workflow to authenticate with AWS

   ![AWS-ACM-v3 Custom Field - UseOAuth](docsource/images/AWS-ACM-v3-custom-field-UseOAuth-dialog.svg)
   ![AWS-ACM-v3 Custom Field - UseOAuth](docsource/images/AWS-ACM-v3-custom-field-UseOAuth-validation-options-dialog.svg)


   ###### OAuth Scope
   This is the OAuth Scope needed for Okta OAuth, defined in Okta

   ![AWS-ACM-v3 Custom Field - OAuthScope](docsource/images/AWS-ACM-v3-custom-field-OAuthScope-dialog.svg)
   ![AWS-ACM-v3 Custom Field - OAuthScope](docsource/images/AWS-ACM-v3-custom-field-OAuthScope-validation-options-dialog.svg)


   ###### OAuth Grant Type
   In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials`

   ![AWS-ACM-v3 Custom Field - OAuthGrantType](docsource/images/AWS-ACM-v3-custom-field-OAuthGrantType-dialog.svg)
   ![AWS-ACM-v3 Custom Field - OAuthGrantType](docsource/images/AWS-ACM-v3-custom-field-OAuthGrantType-validation-options-dialog.svg)


   ###### OAuth Url
   The token endpoint URL of the OAuth 2.0 / OIDC identity provider, for example https://your-tenant.okta.com/oauth2/default/v1/token

   ![AWS-ACM-v3 Custom Field - OAuthUrl](docsource/images/AWS-ACM-v3-custom-field-OAuthUrl-dialog.svg)
   ![AWS-ACM-v3 Custom Field - OAuthUrl](docsource/images/AWS-ACM-v3-custom-field-OAuthUrl-validation-options-dialog.svg)


   ###### OAuth Client ID
   The Client ID for OAuth.

   ![AWS-ACM-v3 Custom Field - OAuthClientId](docsource/images/AWS-ACM-v3-custom-field-OAuthClientId-dialog.svg)
   ![AWS-ACM-v3 Custom Field - OAuthClientId](docsource/images/AWS-ACM-v3-custom-field-OAuthClientId-validation-options-dialog.svg)


   ###### OAuth Client Secret
   The Client Secret for OAuth.

   ![AWS-ACM-v3 Custom Field - OAuthClientSecret](docsource/images/AWS-ACM-v3-custom-field-OAuthClientSecret-dialog.svg)
   ![AWS-ACM-v3 Custom Field - OAuthClientSecret](docsource/images/AWS-ACM-v3-custom-field-OAuthClientSecret-validation-options-dialog.svg)


   ###### Use IAM User Auth
   A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS

   ![AWS-ACM-v3 Custom Field - UseIAM](docsource/images/AWS-ACM-v3-custom-field-UseIAM-dialog.svg)
   ![AWS-ACM-v3 Custom Field - UseIAM](docsource/images/AWS-ACM-v3-custom-field-UseIAM-validation-options-dialog.svg)


   ###### IAM User Access Key
   The AWS Access Key for an IAM User

   ![AWS-ACM-v3 Custom Field - IAMUserAccessKey](docsource/images/AWS-ACM-v3-custom-field-IAMUserAccessKey-dialog.svg)
   ![AWS-ACM-v3 Custom Field - IAMUserAccessKey](docsource/images/AWS-ACM-v3-custom-field-IAMUserAccessKey-validation-options-dialog.svg)


   ###### IAM User Access Secret
   The AWS Access Secret for an IAM User.

   ![AWS-ACM-v3 Custom Field - IAMUserAccessSecret](docsource/images/AWS-ACM-v3-custom-field-IAMUserAccessSecret-dialog.svg)
   ![AWS-ACM-v3 Custom Field - IAMUserAccessSecret](docsource/images/AWS-ACM-v3-custom-field-IAMUserAccessSecret-validation-options-dialog.svg)


   ###### sts:ExternalId
   An optional parameter sts:ExternalId to pass with Assume Role calls

   ![AWS-ACM-v3 Custom Field - ExternalId](docsource/images/AWS-ACM-v3-custom-field-ExternalId-dialog.svg)
   ![AWS-ACM-v3 Custom Field - ExternalId](docsource/images/AWS-ACM-v3-custom-field-ExternalId-validation-options-dialog.svg)


   ###### Discovery Role Name
   The IAM role name that exists in each target account. Used during cross-account Discovery to construct the Role ARN for cross-account access (arn:aws:iam::<account-id>:role/<this-value>). During a Discovery job this is supplied via the 'File name patterns to match' dialog field; this store property documents and defaults the value.

   ![AWS-ACM-v3 Custom Field - DiscoveryRoleName](docsource/images/AWS-ACM-v3-custom-field-DiscoveryRoleName-dialog.svg)
   ![AWS-ACM-v3 Custom Field - DiscoveryRoleName](docsource/images/AWS-ACM-v3-custom-field-DiscoveryRoleName-validation-options-dialog.svg)


   ##### Entry Parameters Tab

   | Name | Display Name | Description | Type | Default Value | Entry has a private key | Adding an entry | Removing an entry | Reenrolling an entry |
   | ---- | ------------ | ---- | ------------- | ----------------------- | ---------------- | ----------------- | ------------------- | ----------- |
   | ACM Tags | ACM Tags | The optional ACM tags that should be assigned to the certificate.  Multiple name/value pairs may be entered in the format of `Name1=Value1,Name2=Value2,...,NameN=ValueN` | String |  | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked | 🔲 Unchecked |

   The Entry Parameters tab should look like this:

   ![AWS-ACM-v3 Entry Parameters Tab](docsource/images/AWS-ACM-v3-entry-parameters-store-type-dialog.svg)
   ##### ACM Tags
   The optional ACM tags that should be assigned to the certificate.  Multiple name/value pairs may be entered in the format of `Name1=Value1,Name2=Value2,...,NameN=ValueN`

   ![AWS-ACM-v3 Entry Parameter - ACM Tags](docsource/images/AWS-ACM-v3-entry-parameters-store-type-dialog-ACM Tags.svg)
   ![AWS-ACM-v3 Entry Parameter - ACM Tags](docsource/images/AWS-ACM-v3-entry-parameters-store-type-dialog-ACM Tags-validation-options.svg)


   </details>

## Installation

1. **Download the latest AWS Certificate Manager (ACM) Universal Orchestrator extension from GitHub.**

    Navigate to the [AWS Certificate Manager (ACM) Universal Orchestrator extension GitHub version page](https://github.com/Keyfactor/aws-orchestrator/releases/latest). Refer to the compatibility matrix below to determine which asset should be downloaded. Then, click the corresponding asset to download the zip archive.

   | Universal Orchestrator Version | Latest .NET version installed on the Universal Orchestrator server | `rollForward` condition in `Orchestrator.runtimeconfig.json` | `aws-orchestrator` .NET version to download |
   | --------- | ----------- | ----------- | ----------- |
   | Between `11.0.0` and `11.5.1` (inclusive) | `net8.0` | `LatestMajor` | `net8.0` |
   | `11.6` _and_ newer | `net8.0` | | `net8.0` |
   | `25.5` _and_ newer | `net10.0` | | `net10.0` |

    Unzip the archive containing extension assemblies to a known location.

    > **Note** If you don't see an asset with a corresponding .NET version, you should always assume that it was compiled for `net10.0`.

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

    To configure a PAM provider, [reference the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam) to select an extension and follow the associated instructions to install it on the Universal Orchestrator (remote).

> The above installation steps can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/InstallingAgents/NetCoreOrchestrator/CustomExtensions.htm?Highlight=extensions).

## Defining Certificate Stores

#### Store type version and required field values

The latest Store Type supporting ACM (AWS Certificate Manager) is `AWS-ACM-v3`. The earlier
`AWS-ACM`, `AwsCerManO` and `AwsCerManA` types are no longer supported and must be migrated —
field usage changed and does **not** map across directly, so stores have to be recreated rather than
converted. See
[Migrating from an older ACM store type](#migrating-from-an-older-acm-store-type).

> [!WARNING]
> When creating Certificate Stores, **all available Secret type fields need to have a value set for
> them, even if that is "No Value"**. The secret fields on this store type are `OAuthClientId`,
> `OAuthClientSecret`, `IAMUserAccessKey` and `IAMUserAccessSecret`. Failing to set these Secret
> fields, even when not in use, causes errors that may require database access to fix.

> [!NOTE]
> `Supports Reenrollment` is unchecked and cannot be enabled: ACM provides no CSR-generation API for
> imported certificates, so there is no on-device key generation path to implement.

### Store Creation

#### Manually with the Command UI

<details><summary>Click to expand details</summary>

1. **Navigate to the _Certificate Stores_ page in Keyfactor Command.**

    Log into Keyfactor Command, toggle the _Locations_ dropdown, and click _Certificate Stores_.

2. **Add a Certificate Store.**

    Click the Add button to add a new Certificate Store. Use the table below to populate the **Attributes** in the **Add** form.

   | Attribute | Description |
   | --------- | ----------- |
   | Category | Select "AWS Certificate Manager v3" or the customized certificate store name from the previous step. |
   | Container | Optional container to associate certificate store with. |
   | Client Machine | This is a full AWS ARN specifying a Role. This is the Role that will be assumed in any Auth scenario performing Assume Role. This will dictate what certificates are usable by the orchestrator. A preceding [profile] name should be included if a Credential Profile is to be used in Default Sdk Auth. |
   | Store Path | The AWS Region the store operates in (e.g. us-east-1). Stores created by cross-account Discovery instead use a self-contained path of the form '<roleArn>|<region>' (e.g. 'arn:aws:iam::123456789012:role/KeyfactorACMDiscoveryRole|us-east-1') which carries both the Role ARN to assume and the Region; for those stores the Client Machine field is informational only. Additional regions should get their own store defined. |
   | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM-v3` certificates. Specifically, one with the `AWS-ACM-v3` capability. |
   | UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
   | DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
   | UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS |
   | OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
   | OAuthGrantType | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` |
   | OAuthUrl | The token endpoint URL of the OAuth 2.0 / OIDC identity provider, for example https://your-tenant.okta.com/oauth2/default/v1/token |
   | OAuthClientId | The Client ID for OAuth. |
   | OAuthClientSecret | The Client Secret for OAuth. |
   | UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS |
   | IAMUserAccessKey | The AWS Access Key for an IAM User |
   | IAMUserAccessSecret | The AWS Access Secret for an IAM User. |
   | ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
   | DiscoveryRoleName | The IAM role name that exists in each target account. Used during cross-account Discovery to construct the Role ARN for cross-account access (arn:aws:iam::<account-id>:role/<this-value>). During a Discovery job this is supplied via the 'File name patterns to match' dialog field; this store property documents and defaults the value. |

</details>

#### Using kfutil CLI

<details><summary>Click to expand details</summary>

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
   | Client Machine | This is a full AWS ARN specifying a Role. This is the Role that will be assumed in any Auth scenario performing Assume Role. This will dictate what certificates are usable by the orchestrator. A preceding [profile] name should be included if a Credential Profile is to be used in Default Sdk Auth. |
   | Store Path | The AWS Region the store operates in (e.g. us-east-1). Stores created by cross-account Discovery instead use a self-contained path of the form '<roleArn>|<region>' (e.g. 'arn:aws:iam::123456789012:role/KeyfactorACMDiscoveryRole|us-east-1') which carries both the Role ARN to assume and the Region; for those stores the Client Machine field is informational only. Additional regions should get their own store defined. |
   | Orchestrator | Select an approved orchestrator capable of managing `AWS-ACM-v3` certificates. Specifically, one with the `AWS-ACM-v3` capability. |
   | Properties.UseDefaultSdkAuth | A switch to enable the store to use Default SDK credentials |
   | Properties.DefaultSdkAssumeRole | A switch to enable the store to assume a new Role when using Default SDK credentials |
   | Properties.UseOAuth | A switch to enable the store to use an OAuth provider workflow to authenticate with AWS |
   | Properties.OAuthScope | This is the OAuth Scope needed for Okta OAuth, defined in Okta |
   | Properties.OAuthGrantType | In OAuth 2.0, the term 'grant type' refers to the way an application gets an access token. In Okta this is `client_credentials` |
   | Properties.OAuthUrl | The token endpoint URL of the OAuth 2.0 / OIDC identity provider, for example https://your-tenant.okta.com/oauth2/default/v1/token |
   | Properties.OAuthClientId | The Client ID for OAuth. |
   | Properties.OAuthClientSecret | The Client Secret for OAuth. |
   | Properties.UseIAM | A switch to enable the store to use IAM User auth to assume a role when authenticating with AWS |
   | Properties.IAMUserAccessKey | The AWS Access Key for an IAM User |
   | Properties.IAMUserAccessSecret | The AWS Access Secret for an IAM User. |
   | Properties.ExternalId | An optional parameter sts:ExternalId to pass with Assume Role calls |
   | Properties.DiscoveryRoleName | The IAM role name that exists in each target account. Used during cross-account Discovery to construct the Role ARN for cross-account access (arn:aws:iam::<account-id>:role/<this-value>). During a Discovery job this is supplied via the 'File name patterns to match' dialog field; this store property documents and defaults the value. |

3. **Import the CSV file to create the certificate stores**

    ```shell
    kfutil stores import csv --store-type-name AWS-ACM-v3 --file AWS-ACM-v3.csv
    ```

</details>

#### PAM Provider Eligible Fields
<details><summary>Attributes eligible for retrieval by a PAM Provider on the Universal Orchestrator</summary>

If a PAM provider was installed _on the Universal Orchestrator_ in the [Installation](#Installation) section, the following parameters can be configured for retrieval _on the Universal Orchestrator_.

   | Attribute | Description |
   | --------- | ----------- |
   | OAuthClientId | The Client ID for OAuth. |
   | OAuthClientSecret | The Client Secret for OAuth. |
   | IAMUserAccessKey | The AWS Access Key for an IAM User |
   | IAMUserAccessSecret | The AWS Access Secret for an IAM User. |

Please refer to the **Universal Orchestrator (remote)** usage section ([PAM providers on the Keyfactor Integration Catalog](https://keyfactor.github.io/integrations-catalog/content/pam)) for your selected PAM provider for instructions on how to load attributes orchestrator-side.
> Any secret can be rendered by a PAM provider _installed on the Keyfactor Command server_. The above parameters are specific to attributes that can be fetched by an installed PAM provider running on the Universal Orchestrator server itself.

</details>

> The content in this section can be supplemented by the [official Command documentation](https://software.keyfactor.com/Core-OnPrem/Current/Content/ReferenceGuide/Certificate%20Stores.htm?Highlight=certificate%20store).


## License

Apache License 2.0, see [LICENSE](LICENSE).

## Related Integrations

See all [Keyfactor Universal Orchestrator extensions](https://github.com/orgs/Keyfactor/repositories?q=orchestrator).
