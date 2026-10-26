## Overview

The `AWS-ACM-v3` store type represents **one AWS region inside one AWS account**. Certificate
aliases within the store are full ACM certificate ARNs, and the region plus the identity to operate
as are both carried by the store's `Client Machine` and `Store Path` fields.

Define a separate certificate store for every region you hold certificates in; a single store cannot
span regions.

#### Store Path and Client Machine

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

#### Which identity actually reaches ACM

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

## Requirements

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

## Global Store Type Section

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
