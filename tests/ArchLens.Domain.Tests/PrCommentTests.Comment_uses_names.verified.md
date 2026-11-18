<!-- archlens-pr-check -->
## ArchLens: architecture changes

Elements: +1 −0 ~1 · Relationships: +1 −1 ~0

- 🟢 added container 'Session Cache'
- 🟡 modified container 'Mobile Storefront': name: 'Mobile App' → 'Mobile Storefront'; technology: 'Kotlin / Android' → 'Kotlin Multiplatform'
- 🟢 added relationship 'API Gateway' → 'Session Cache' (Caches sessions in)
- 🔴 removed relationship 'Notification Service' → 'Mobile Storefront' (Sends push notifications to)

### Affected views: 1 / 5

**Containers: ShopFlow**
- added element 'Session Cache'
- modified element 'Mobile Storefront': name, technology
- added relationship 'API Gateway' → 'Session Cache'
- removed relationship 'Notification Service' → 'Mobile Storefront'

![Containers: ShopFlow](https://img/container-shop.svg)

### ❌ Deployment check (R1): differences: 1
- service 'worker' is missing from the model
