# GFH Media Intelligence Platform (MIP.Aws)

## Approximate Deployment & Licensing Cost Estimate (BHD)

| Field | Value |
|-------|--------|
| **Product** | MIP.Aws — GFH Media Intelligence Platform |
| **Project path** | `D:\MIPaws` |
| **AWS region (reference)** | `eu-north-1` (Europe — Stockholm) |
| **Document version** | 1.0 |
| **Date** | June 2026 |
| **Classification** | Internal — GFH / Almoayyed Computers |
| **Estimate type** | Indicative monthly run-rate + annual licensing notes |

---

## 1. Purpose

This document provides **approximate** deployment and licensing costs for operating MIP.Aws on Amazon Web Services, expressed in **Bahraini Dinar (BHD)** for GFH budget and approval discussions.

Figures are **indicative only**. Actual AWS invoices depend on usage, data transfer, PDF storage growth, AI call volume, and AWS price changes. Obtain a formal quote using the [AWS Pricing Calculator](https://calculator.aws/) before procurement.

---

## 2. Currency and assumptions

### 2.1 Exchange rate

| Item | Value |
|------|--------|
| Peg (Central Bank of Bahrain) | **1 USD = 0.376 BHD** |
| Inverse | 1 BHD ≈ 2.6596 USD |
| Rounding | BHD amounts rounded to **2 decimal places** |

All USD figures in this document are converted at **0.376 BHD per USD** unless stated otherwise.

### 2.2 General assumptions

| Assumption | Detail |
|------------|--------|
| Deployment model | Terraform defaults in `infra/terraform/` (see [AWS_COST_CONTROL.md](./AWS_COST_CONTROL.md)) |
| Uptime | **24×7** monthly run (730 hours) unless noted |
| NAT Gateway | **Disabled** (dev cost saving) |
| Worker service | **0 tasks** in dev estimate (downloads run on API container) |
| PDF storage | Grows over time; scenarios included below |
| Bedrock AI | Enabled with light-to-moderate recovery usage |
| SES | Sandbox or low-volume status emails in dev; production access for operations mail |
| SQL Server edition | **Express** (dev) / **Web or Standard** (production recommendation) |
| PressReader | **Separate commercial license** — not an AWS line item |

---

## 3. Cost summary (BHD per month)

| Scenario | USD / month (approx.) | BHD / month (approx.) | BHD / year (approx.) |
|----------|----------------------:|----------------------:|---------------------:|
| **A — Dev / test (Terraform defaults)** | 260 – 320 | **98 – 120** | **1,176 – 1,440** |
| **B — Dev with heavier PDF + AI usage** | 350 – 450 | **132 – 169** | **1,584 – 2,028** |
| **C — Production (single-AZ, moderate scale)** | 850 – 1,200 | **319 – 451** | **3,828 – 5,412** |
| **D — Production (HA: Multi-AZ, NAT, 2× API)** | 1,400 – 2,000 | **526 – 752** | **6,312 – 9,024** |

**Non-AWS licensing (PressReader, etc.):** see Section 6 — typically **additional** to the above.

---

## 4. Detailed AWS cost breakdown

### 4.1 Scenario A — Dev / test (recommended starting point)

Matches current Terraform defaults: `db.t3.small` SQL Server Express, 20 GB storage, ECS API **0.25 vCPU / 1 GB**, 1 API task, no NAT, 14-day logs.

| # | Service | Configuration | USD / month | BHD / month |
|---|---------|---------------|------------:|------------:|
| 1 | **Amazon RDS** | SQL Server **Express**, `db.t3.small`, Single-AZ, 20 GB gp3, license-included | 165 – 210 | 62.04 – 78.96 |
| 2 | **Amazon ECS Fargate** | API: 0.25 vCPU, 1 GB RAM, 1 task × 730 h | 10 – 14 | 3.76 – 5.26 |
| 3 | **Application Load Balancer** | 1 ALB, low LCU traffic | 18 – 24 | 6.77 – 9.02 |
| 4 | **Amazon S3** | ~20–50 GB PDFs + versioning (year 1, early) | 2 – 8 | 0.75 – 3.01 |
| 5 | **Amazon ECR** | ~2–3 GB container images | 1 – 3 | 0.38 – 1.13 |
| 6 | **CloudWatch Logs** | 14-day retention, moderate API/Hangfire logs | 5 – 12 | 1.88 – 4.51 |
| 7 | **AWS Secrets Manager** | 2–3 secrets | 1 – 2 | 0.38 – 0.75 |
| 8 | **Amazon SES** | Status emails, sandbox / low volume | 0 – 2 | 0.00 – 0.75 |
| 9 | **Amazon Bedrock** | Nova Lite, light recovery (~50–200 calls/month) | 3 – 15 | 1.13 – 5.64 |
| 10 | **Data transfer** | Egress to internet (portal downloads, ALB) | 5 – 20 | 1.88 – 7.52 |
| | **Subtotal (AWS)** | | **210 – 310** | **78.96 – 116.56** |
| | **Contingency (~15%)** | Price variance, CPU credits, spikes | 40 – 50 | 15.04 – 18.80 |
| | **Scenario A total** | | **260 – 320** | **98 – 120** |

> **Note:** RDS for SQL Server is the **largest single cost** in dev. Stopping RDS nights/weekends or scaling ECS to 0 when not testing can reduce spend materially (see Section 8).

---

### 4.2 Scenario B — Dev with heavier usage

Same infrastructure, but higher PDF volume, more Playwright jobs, and more Bedrock recovery calls.

| Cost driver | Change vs Scenario A | Extra USD / month | Extra BHD / month |
|-------------|----------------------|------------------:|------------------:|
| S3 storage | 100–200 GB PDFs | +8 – 20 | +3.01 – 7.52 |
| Fargate | API scaled to **0.5 vCPU / 2 GB** | +15 – 25 | +5.64 – 9.40 |
| Bedrock | 500–2,000 model calls | +20 – 60 | +7.52 – 22.56 |
| CloudWatch + transfer | Higher job volume | +10 – 25 | +3.76 – 9.40 |
| **Scenario B incremental** | | +50 – 130 | +19 – 49 |
| **Scenario B total** | | **350 – 450** | **132 – 169** |

---

### 4.3 Scenario C — Production (moderate scale)

Single-AZ, no NAT (or minimal), production-ready sizing.

| # | Service | Configuration | USD / month | BHD / month |
|---|---------|---------------|------------:|------------:|
| 1 | **Amazon RDS** | SQL Server **Web**, `db.t3.medium`, 100 GB, backups 7 d | 380 – 520 | 142.88 – 195.52 |
| 2 | **ECS Fargate** | API: **2 vCPU / 4 GB**, 2 tasks (HA behind ALB) | 110 – 150 | 41.36 – 56.40 |
| 3 | **ALB** | Production traffic + health checks | 22 – 35 | 8.27 – 13.16 |
| 4 | **S3** | 200–500 GB PDF archive | 15 – 45 | 5.64 – 16.92 |
| 5 | **ECR + Secrets + CloudWatch** | Production retention (30 d logs) | 25 – 45 | 9.40 – 16.92 |
| 6 | **SES** | Daily status mail + notifications (~500–2,000/month) | 1 – 5 | 0.38 – 1.88 |
| 7 | **Bedrock** | Regular recovery workload | 40 – 120 | 15.04 – 45.12 |
| 8 | **Data transfer** | Higher portal/PDF egress | 30 – 80 | 11.28 – 30.08 |
| 9 | **Route 53 + ACM** | Custom domain (optional) | 1 – 5 | 0.38 – 1.88 |
| | **Contingency (~10%)** | | 80 – 120 | 30.08 – 45.12 |
| | **Scenario C total** | | **850 – 1,200** | **319 – 451** |

---

### 4.4 Scenario D — Production (high availability)

For stricter uptime and private networking.

| Additional item | USD / month | BHD / month |
|-----------------|------------:|------------:|
| RDS **Multi-AZ** (duplicate standby) | +350 – 500 | +131.60 – 188.00 |
| **NAT Gateway** (1 AZ) + data processing | +35 – 55 | +13.16 – 20.68 |
| Larger RDS (`db.t3.large` or storage 200 GB+) | +150 – 250 | +56.40 – 94.00 |
| **Scenario D total** | **1,400 – 2,000** | **526 – 752** |

---

## 5. One-time and annual AWS costs

| Item | Frequency | USD (approx.) | BHD (approx.) | Notes |
|------|-----------|--------------:|--------------:|-------|
| Initial Terraform deploy | Once | 0 | 0 | No AWS setup fee |
| ECR first image push | Once | &lt; 5 | &lt; 1.88 | Included in first month |
| SES domain verification | Once | 0 | 0 | DNS records only |
| ACM TLS certificate | Annual | 0 | 0 | Free for ALB use |
| AWS Support (optional Business) | Monthly | 100+ | 37.60+ | Optional |
| **Reserved Instance / Savings Plan** | 1–3 year commit | −20% to −40% | — | Applies mainly to RDS &amp; Fargate at scale |

---

## 6. Licensing costs (non-AWS)

These are **not billed on the AWS invoice** but are required for full MIP.Aws operation.

### 6.1 GFH proprietary software

| Item | Cost model | Indicative BHD |
|------|------------|---------------:|
| **MIP.Aws application** | GFH internal / project delivery | **0** (internal development) |
| **Ongoing maintenance &amp; support** | GFH IT policy | *Per internal SLA* |

### 6.2 PressReader and publisher content

| Item | Cost model | Indicative BHD |
|------|------------|---------------:|
| **PressReader institutional subscription** | Commercial contract with PressReader / publisher | **TBD** — request quote from GFH licensing |
| **Per-newspaper portal rights** | Publisher agreements (e.g. UAE/Gulf titles) | **TBD** — compliance approval per source |
| **Concurrent session limits** | Per PressReader subscription tier | Operational constraint, not AWS cost |

> **Planning placeholder:** Institutional PressReader access for a single organisation often ranges **USD 500 – 3,000+ / year** depending on titles and seats — **approx. BHD 188 – 1,128 / year** at 0.376. **Confirm with GFH legal / publisher contracts.**

### 6.3 Microsoft SQL Server (via AWS RDS)

| Edition | How licensed on AWS | Included in Section 4? |
|---------|---------------------|------------------------|
| **SQL Server Express** | RDS license-included (`license-included`) | Yes — dev Scenario A |
| **SQL Server Web** | RDS license-included | Yes — production Scenario C |
| **SQL Server Standard** | RDS license-included (higher instance $) | Optional upgrade — add ~USD 200–400/mo |

No separate Microsoft SQL license purchase is required when using **License Included** RDS models.

### 6.4 Open-source components

| Component | License | Cost |
|-----------|---------|------|
| .NET 10, ASP.NET Core | MIT | BHD 0 |
| Playwright / Chromium | Apache 2.0 | BHD 0 |
| MudBlazor, Hangfire, EF Core | OSS (verify Hangfire LGPL for production) | BHD 0 |

### 6.5 Amazon Bedrock (usage-based AI)

| Model (current dev) | Pricing model | Typical dev add-on |
|---------------------|---------------|-------------------|
| `amazon.nova-lite-v1:0` | Per 1K input/output tokens | BHD 1 – 6 / month (light) |
| `amazon.nova-pro-v1:0` | Higher per-token rate | BHD 15 – 50 / month (moderate ops) |

Bedrock costs are **included in AWS scenarios** above but scale with recovery frequency.

---

## 7. PDF storage growth impact (S3)

Illustrative **incremental** S3 cost only (Standard storage, eu-north-1 ~USD 0.023/GB-month):

| Stored PDF volume | Extra USD / month | Extra BHD / month |
|------------------:|------------------:|------------------:|
| 50 GB | ~1.15 | ~0.43 |
| 200 GB | ~4.60 | ~1.73 |
| 500 GB | ~11.50 | ~4.32 |
| 1 TB | ~23.00 | ~8.65 |
| 2 TB | ~46.00 | ~17.30 |

Lifecycle rules (IA after 30 days, expire after 365 days) in Terraform reduce long-term cost.

**Example:** 20 newspapers × 30 MB/edition × 365 days ≈ 219 GB/year before expiry — add ~**BHD 2–5 / month** average to Scenario C at steady state.

---

## 8. Cost reduction options (dev / pilot)

| Action | Estimated saving | BHD / month saved (approx.) |
|--------|------------------|----------------------------:|
| Scale ECS API to **0** when not testing | Full Fargate API cost | 4 – 6 |
| **Stop RDS** instance off-hours (dev only) | Up to ~65% of RDS hours | 40 – 50 |
| Set `worker_desired_count = 0` | Worker Fargate | 2 – 4 |
| Disable Bedrock (`enable_bedrock = false`) | AI usage | 1 – 20 |
| Keep SES in sandbox | Minimal email | &lt; 1 |
| Avoid NAT Gateway | Per-AZ fixed fee | 13 – 21 |
| `terraform destroy` when pilot ends | All AWS infra | 98 – 752 (scenario dependent) |

---

## 9. Comparison: dev vs production (at a glance)

| Dimension | Dev (A) | Production (C) |
|-----------|---------|----------------|
| **BHD / month** | 98 – 120 | 319 – 451 |
| **BHD / year (AWS only)** | ~1,176 – 1,440 | ~3,828 – 5,412 |
| RDS | Express, t3.small | Web, t3.medium+ |
| ECS API | 0.25 vCPU, 1 GB × 1 | 2 vCPU, 4 GB × 2 |
| HTTPS / custom domain | Optional | Recommended |
| PressReader license | Required if using portals | Required |
| Compliance | Pilot / internal | Production SES + HTTPS |

---

## 10. Recommended budget request (GFH internal)

For **Year 1 pilot + early production** planning:

| Budget line | BHD (recommended envelope) | Notes |
|-------------|---------------------------:|-------|
| AWS infrastructure (12 months, dev → prod ramp) | **15,000 – 35,000** | Scenarios A→C over project timeline |
| PressReader / publisher licensing | **500 – 2,000** | Confirm with contracts team |
| Domain, certificates, misc. | **100 – 300** | Optional custom domain |
| Contingency (15%) | **2,500 – 5,500** | Usage spikes, storage growth |
| **Total Year 1 indicative** | **18,100 – 42,800 BHD** | Excludes internal labour |

---

## 11. Disclaimer

1. Costs are **estimates** based on Terraform defaults, AWS public pricing patterns for `eu-north-1`, and GFH MIP.Aws architecture as of June 2026.
2. **SQL Server on RDS** pricing varies by edition, instance size, and AWS pricing updates — validate with AWS Pricing Calculator before approval.
3. **PressReader and publisher fees** are contractual and must be obtained from GFH licensing / legal.
4. This document does **not** constitute a commercial quote from Amazon Web Services or PressReader.
5. Re-calculate BHD using the official CBB rate at approval time: [Central Bank of Bahrain](https://www.cbb.gov.bh/monetary-policy/).

---

## 12. References

| Document | Location |
|----------|----------|
| Deployment requirements | `docs/GFH_MIP_AWS_DEPLOYMENT_REQUIREMENTS.md` |
| AWS deployment guide | `docs/AWS_DEPLOYMENT.md` |
| Cost control | `docs/AWS_COST_CONTROL.md` |
| Terraform variables | `infra/terraform/variables.tf` |
| AWS Pricing Calculator | https://calculator.aws/ |

---

## Document history

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | June 2026 | Initial BHD cost estimate for MIP.Aws |
