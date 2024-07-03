## Overview

AWS Certificate Manager (ACM) is a service that allows you to easily provision, manage, and deploy public and private Secure Sockets Layer/Transport Layer Security (SSL/TLS) certificates for use with AWS services and your internal connected resources. These certificates are essential for securing network communications and establishing the identity of internet-facing websites as well as private network resources. ACM simplifies the otherwise time-consuming process of purchasing, uploading, and renewing SSL/TLS certificates.

The AWS Certificate Manager (ACM) Universal Orchestrator extension enables remote management of cryptographic certificates within ACM. This extension uses an abstraction called Certificate Stores. A defined Certificate Store in this context represents a collection or a single instance of SSL/TLS certificates located on an ACM platform. The store can include root certificates, intermediate certificates, and certificates with associated public and private keys, which are managed through various job types such as Inventory, Add, and Remove.

