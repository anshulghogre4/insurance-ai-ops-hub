# Azure Free Services Guide — $1 Budget Alert

> **Context:** Pay-as-you-go subscription with $1 monthly budget alert
> **Goal:** Maximize Azure free services for Insurance AI Operations Hub without accidental charges
> **Date:** 2026-02-27

---

## Charge Risk Categories

| Category | Meaning | Action |
|----------|---------|--------|
| **Zero Risk** | Truly free forever, no credit card charges possible | Use freely |
| **Controllable** | Free tier with hard caps or F0 SKU limits | Safe with proper SKU selection |
| **Soft Cap (Dangerous)** | Free allowance, then auto-charges beyond it | Requires daily cap / alerts |
| **Avoid** | No meaningful free tier or easy to exceed | Do not enable |

---

## Top 10 Recommended Azure Free Services

### 1. Azure AI Document Intelligence (F0) — ALREADY ACTIVE
- **Category:** Controllable
- **Free Tier:** 500 pages/month, 2 pages/request, 4MB max file
- **Charge Risk:** ZERO (F0 SKU has hard cap — requests rejected after 500 pages, never bills)
- **Our Use:** Tier 2 OCR in resilient fallback chain (PdfPig -> Azure -> OCR Space -> Gemini)
- **Status:** API key and endpoint configured in user-secrets

### 2. Azure Key Vault (Standard — No Free Tier)
- **Category:** Controllable (paid, but near-zero cost)
- **Pricing:** $0.03 per 10,000 secret operations (Standard tier — no F0 exists)
- **Charge Risk:** NEAR ZERO — ~15 secrets × ~100 reads/day = ~3,000 ops/month = **< $0.01/month**
- **Our Use:** Store AI provider API keys, DB connection strings, Supabase credentials
- **Why:** Replaces `dotnet user-secrets` for cloud deployment. Managed Identity = no key rotation headaches
- **Note:** Certificates cost $3/renewal, keys $0.03/10K ops. We only need secrets — skip certs/keys

### 3. Azure Static Web Apps (Free Plan)
- **Category:** Zero Risk
- **Free Tier:** 100GB bandwidth/month, 2 custom domains, auto-SSL, GitHub CI/CD built-in (always free)
- **Charge Risk:** ZERO — Free plan is a hard tier, not a consumption allowance
- **Our Use:** Host Angular 21 frontend (SPA with route fallback)
- **Why:** Purpose-built for SPAs, global CDN, preview environments per PR

### 4. Azure Container Apps (Consumption Plan)
- **Category:** Controllable
- **Free Tier:** 180,000 vCPU-seconds + 360,000 GiB-seconds/month (always free)
- **Charge Risk:** VERY LOW — free allowance covers ~50 hours of continuous running
- **Our Use:** Host .NET 10 backend API (alternative to App Service after B1 expiry)
- **Why:** Always-free (unlike App Service B1 which expires at 12 months)

### 5. Azure App Service (B1 Linux)
- **Category:** Controllable
- **Free Tier:** 750 hours/month (12 months free, then paid ~$13/month)
- **Charge Risk:** ZERO for 12 months — single instance stays within 750hrs
- **Our Use:** Host .NET 10 backend API (first 12 months)
- **Sunset Plan:** Migrate to Container Apps at month 10

### 6. Azure SQL Database (Serverless, Free Offer)
- **Category:** Controllable
- **Free Tier:** 100,000 vCore-seconds/month, 32GB storage (always free)
- **Charge Risk:** VERY LOW — auto-pauses when idle, hard limit on vCore-seconds
- **Our Use:** Production database (alternative to Supabase PostgreSQL)
- **Why:** Native EF Core support, auto-pause saves compute, 32GB is plenty

### 7. Application Insights
- **Category:** Soft Cap (Dangerous) — REQUIRES DAILY CAP
- **Free Tier:** 5GB data ingestion/month (always free)
- **Charge Risk:** MEDIUM without daily cap — verbose logging can exceed 5GB
- **Our Use:** Backend telemetry, error tracking, performance monitoring
- **MANDATORY CONFIG:**
  ```
  Daily cap: 0.16 GB/day (= ~5GB/month)
  Sampling: Enable adaptive sampling (reduces volume by ~80%)
  Alerts: Set alert at 80% of daily cap
  ```
- **Without daily cap:** A single busy day could ingest 5GB and charges begin at $2.30/GB

### 8. Azure Communication Services (Email)
- **Category:** Controllable
- **Free Tier:** 100 emails/day (always free)
- **Charge Risk:** ZERO — hard daily limit, excess emails rejected (not billed)
- **Our Use:** Fraud alert notifications, claim status updates to policyholders
- **Why:** Simple REST API, no SMTP server needed

### 9. Azure Cosmos DB (Free Tier)
- **Category:** Soft Cap (Dangerous) — USE WITH CAUTION
- **Free Tier:** 1,000 RU/s + 25GB storage (always free, ONE per subscription)
- **Charge Risk:** MEDIUM — if throughput exceeds 1,000 RU/s, auto-scales and charges
- **Our Use:** NOT recommended for primary DB. Consider only for caching or session storage
- **MANDATORY CONFIG:**
  ```
  Max throughput: 1,000 RU/s (do NOT enable autoscale)
  Provisioned mode only (not serverless — serverless has no free tier)
  ```
- **Safer Alternative:** Azure SQL Free (hard cap, no overage billing)

### 10. Azure Blob Storage (LRS)
- **Category:** Controllable
- **Free Tier:** 5GB storage + 20,000 read + 10,000 write operations/month (12 months free)
- **Charge Risk:** VERY LOW — our document uploads are small (PDFs < 4MB each)
- **Our Use:** Store uploaded claim evidence (PDFs, images, audio) for RAG pipeline
- **Why:** Cheaper than DB for binary files, lifecycle policies auto-delete old files

---

## Azure AI Services — Free Tier (F0) Catalog

All F0 SKUs have **hard caps** — requests are rejected (HTTP 429) after the limit, never billed.
One F0 resource per service per subscription. All are **Zero/Controllable risk** for a $1 budget.

### Tier A: Directly Useful for Insurance AI Hub (Adopt Now)

#### 1. Azure AI Language (F0) — Always Free
- **Free Tier:** 5,000 text records/month (shared across ALL features below)
- **Features Included:**
  - Sentiment Analysis + Opinion Mining
  - Named Entity Recognition (NER)
  - Key Phrase Extraction
  - Language Detection
  - Text Summarization (extractive + abstractive)
  - PII Entity Detection + Redaction
  - Entity Linking
- **Insurance Use Cases:**
  - Replace/supplement HuggingFace FinBERT for sentiment pre-screening
  - Azure NER alongside HuggingFace BERT NER for better entity coverage
  - **PII Detection API** — cloud-based PII redaction to complement our regex `IPIIRedactor`
  - Summarize lengthy claim narratives before agent analysis
  - Detect language on international policyholder communications
- **Integration:** `Azure.AI.TextAnalytics` NuGet, or raw REST API
- **Limit Note:** 1 document with 2 features = 2 records consumed. Budget for ~2,500 documents/month with dual-feature calls

#### 2. Azure AI Vision (F0) — 12 Months Free
- **Free Tier:** 5,000 transactions/month, 20 TPS
- **Features Included:**
  - Image Analysis (tags, objects, people, brands, adult content)
  - OCR (Read API — text extraction from images)
  - Smart Crops + Thumbnails
  - Caption Generation (natural language image descriptions)
- **Insurance Use Cases:**
  - Replace/supplement Cloudflare Vision for claim photo analysis
  - Damage assessment on property claim photos (object detection)
  - Vehicle damage recognition for auto claims
  - Caption generation for evidence photos in claim reports
- **Integration:** `Azure.AI.Vision.ImageAnalysis` NuGet
- **Synergy:** Already have `IImageAnalysisService` with Azure Vision + Cloudflare keyed services. F0 provides 5K free calls to strengthen Tier 1

#### 3. Azure AI Content Safety (F0) — Always Free
- **Free Tier:** 5,000 text + 5,000 image analyses/month
- **Features Included:**
  - Text moderation (hate, violence, self-harm, sexual content)
  - Image moderation (same categories)
  - Prompt shields (jailbreak detection)
  - Groundedness detection (hallucination check)
- **Insurance Use Cases:**
  - Screen CX Copilot responses before sending to policyholders
  - Detect abusive language in policyholder communications
  - **Prompt shield** on CX Copilot to prevent prompt injection attacks
  - **Groundedness check** to detect hallucinated claim details in agent outputs
- **Integration:** `Azure.AI.ContentSafety` NuGet
- **Why Critical:** CX Copilot streams responses to policyholders — content safety is mandatory

#### 4. Azure AI Translator (F0) — Always Free
- **Free Tier:** 2,000,000 characters/month
- **Features Included:**
  - Text translation (130+ languages)
  - Language detection
  - Transliteration
  - Dictionary lookup
- **Insurance Use Cases:**
  - Translate policyholder communications from non-English languages before sentiment analysis
  - Multi-language support for CX Copilot chat
  - Translate claim documents submitted in foreign languages
- **Integration:** REST API (no SDK needed, simple HTTP POST)
- **Volume:** 2M chars = ~400 pages of text/month — more than enough

#### 5. Azure AI Speech (F0) — Always Free
- **Free Tier:**
  - Speech-to-Text: 5 audio hours/month (standard + custom)
  - Text-to-Speech: 500,000 neural characters/month
  - Speech Translation: 5 audio hours/month
- **Insurance Use Cases:**
  - Replace/supplement Deepgram STT for claim audio evidence transcription
  - Add text-to-speech for CX Copilot accessibility (read responses aloud)
  - Translate audio claims from non-English callers
- **Integration:** `Microsoft.CognitiveServices.Speech` NuGet
- **Synergy:** Already have `ISpeechToTextService` with Deepgram. Azure Speech as fallback provider

### Tier B: High Value, Add When Needed

#### 6. Azure AI Face (F0) — Always Free
- **Free Tier:** 30,000 transactions/month
- **Features Included:**
  - Face detection (age, gender, emotion, glasses, facial hair)
  - Face verification (1:1 matching)
  - Face identification (1:N matching)
- **Insurance Use Cases:**
  - Identity verification for high-value claims (compare claimant photo to ID)
  - Fraud detection: detect same person filing under different identities
  - Emotion detection from video evidence (injury severity assessment)
- **Integration:** `Azure.AI.Vision.Face` NuGet
- **Note:** Requires Microsoft approval for face identification features (access form required)

#### 7. Azure AI Anomaly Detector (F0) — Always Free
- **Free Tier:** 20,000 transactions/month
- **Features Included:**
  - Univariate anomaly detection (single metric time series)
  - Multivariate anomaly detection (correlated metrics)
- **Insurance Use Cases:**
  - Detect anomalous claim patterns (sudden spike in claims from a region)
  - Fraud ring detection via unusual claim frequency patterns
  - Premium pricing anomaly detection
- **Integration:** `Azure.AI.AnomalyDetector` NuGet
- **Synergy:** Complements our 4-strategy fraud correlation system

#### 8. Azure AI Search (Free) — Always Free
- **Free Tier:** 50MB storage, 3 indexes, 10,000 documents
- **Features Included:**
  - Full-text search with BM25 ranking
  - Vector search (hybrid semantic + keyword)
  - Faceted navigation
  - AI enrichment pipeline
- **Insurance Use Cases:**
  - Replace in-memory vector search in Document Intelligence RAG pipeline
  - Semantic search across uploaded policy documents
  - Hybrid search: vector embeddings + keyword matching for better recall
- **Integration:** `Azure.Search.Documents` NuGet
- **Limit:** 50MB is tight — good for ~500 chunked documents. Fine for dev/demo

#### 9. Azure AI Immersive Reader (F0) — Always Free
- **Free Tier:** 3,000,000 characters/month
- **Features Included:**
  - Text-to-speech reading
  - Translation in 100+ languages
  - Syllable splitting, picture dictionary
  - Reading preferences (font size, spacing, colors)
- **Insurance Use Cases:**
  - Accessibility feature for policy documents (read aloud complex insurance terms)
  - Help non-native speakers understand policy language
- **Integration:** JavaScript SDK (frontend embed)

#### 10. Azure AI Custom Vision (F0) — 12 Months Free
- **Free Tier:** 10,000 predictions/month + 1 training hour + 2 projects
- **Features Included:**
  - Custom image classification (train your own model)
  - Custom object detection
- **Insurance Use Cases:**
  - Train custom model to classify damage types (water, fire, wind, hail, vehicle)
  - Detect specific insurance document types (policy dec page, loss notice, estimate)
- **Integration:** `Azure.CognitiveServices.Vision.CustomVision.Prediction` NuGet
- **Note:** 1 training hour/month is very limited — train offline, deploy for inference only

### Tier C: Niche / Future Use

#### 11. Azure AI Speaker Recognition (F0) — Always Free
- **Free Tier:** 10,000 verification + 10,000 identification transactions/month
- **Insurance Use Cases:** Voice biometric authentication for phone claim reporting
- **Integration:** Speech SDK

#### 12. Azure AI Bot Service — Always Free
- **Free Tier:** 10,000 premium channel messages + unlimited standard
- **Insurance Use Cases:** Deploy CX Copilot as Teams/Slack bot for internal claims adjusters
- **Integration:** Bot Framework SDK

#### 13. Azure Health Bot — Always Free
- **Free Tier:** 3,000 messages/month (10 msg/sec)
- **Insurance Use Cases:** Workers' comp injury triage chatbot (pre-built medical protocols)
- **Integration:** Web Chat embed

---

## Azure AI Services — Insurance Priority Matrix

| Priority | Service | Free Limit | Insurance ROI | Effort |
|----------|---------|-----------|---------------|--------|
| **P0** | AI Document Intelligence | 500 pages/mo | Already active (OCR chain) | Done |
| **P1** | AI Language (F0) | 5K records/mo | Sentiment + NER + PII + Summarization | Low |
| **P1** | AI Content Safety (F0) | 5K+5K/mo | CX Copilot safety + prompt shields | Low |
| **P2** | AI Vision (F0) | 5K txns/mo | Claim photo analysis (strengthen Tier 1) | Low |
| **P2** | AI Translator (F0) | 2M chars/mo | Multi-language policyholder support | Low |
| **P2** | AI Speech (F0) | 5 hrs/mo | STT fallback for Deepgram | Medium |
| **P3** | AI Anomaly Detector (F0) | 20K txns/mo | Fraud pattern detection | Medium |
| **P3** | AI Search (Free) | 50MB/3 idx | RAG vector search upgrade | Medium |
| **P3** | AI Face (F0) | 30K txns/mo | Identity verification (needs approval) | High |
| **P4** | AI Custom Vision (F0) | 10K pred/mo | Custom damage classifier | High |
| **P4** | AI Immersive Reader (F0) | 3M chars/mo | Accessibility for policy docs | Low |
| **P4** | Speaker Recognition (F0) | 10K+10K/mo | Voice biometrics (future) | High |

---

## Additional Free Services (Lower Priority)

| # | Service | Free Tier | Charge Risk | Potential Use |
|---|---------|-----------|-------------|---------------|
| 11 | **Azure Functions** | 1M executions + 400K GB-s/month | Zero | Scheduled fraud correlation batch jobs |
| 12 | **Azure Service Bus** | 1,000 operations/day (Basic) | Zero | Async claim processing queue |
| 13 | **Azure Event Grid** | 100,000 operations/month | Zero | Event-driven fraud alert pipeline |
| 14 | **Azure API Management (Consumption)** | 1M calls/month | Controllable | API gateway, rate limiting, analytics |
| 15 | **Azure Cognitive Search (Free)** | 3 indexes, 50MB storage | Zero | Enhanced RAG search (replace in-memory) |
| 16 | **Azure Notification Hubs (Free)** | 1M pushes/month | Zero | Mobile push notifications (future) |
| 17 | **Azure SignalR (Free)** | 20 connections, 20K messages/day | Zero | Real-time claim status updates |
| 18 | **Azure Maps** | 5,000 transactions/day | Controllable | Claim location mapping, fraud geo-analysis |
| 19 | **Azure Monitor (Logs)** | 5GB ingestion/month | Soft Cap | Centralized logging (use daily cap!) |
| 20 | **Bandwidth** | 15GB outbound/month | Controllable | Network egress (12 months free) |

---

## Services to AVOID ($1 Budget)

| Service | Why Avoid | Minimum Monthly Cost |
|---------|-----------|---------------------|
| **Azure OpenAI** | No free tier. Pay-per-token from first call | $5-50+/month |
| **Azure Machine Learning** | Compute instances charge immediately | $10+/month |
| **Azure Cognitive Services (S0+)** | Paid SKUs have no spending caps | Varies |
| **Azure Kubernetes Service (AKS)** | Cluster management free but VMs charge | $30+/month |
| **Azure Front Door** | No free tier for WAF/CDN | $35+/month |
| **Azure Redis Cache** | Cheapest tier is $13/month | $13/month |
| **Azure Cosmos DB (Autoscale)** | Can burst past free 1,000 RU/s silently | Unpredictable |
| **Azure SQL (Standard/Premium)** | Only Serverless Free tier is $0 | $5-15+/month |
| **Azure Virtual Machines** | B1 free for 12 months, then $7+/month | $7+/month |
| **Azure AI Services (multi-service)** | Combines multiple APIs, easy to exceed | Hard to track |

---

## Budget Protection Checklist

### Mandatory Setup (Do Before Enabling ANY Service)

- [ ] **Budget alert configured:** Azure Portal > Subscriptions > Cost Management > Budgets > $1/month
- [ ] **Second alert at $0.50** (50% threshold) for early warning
- [ ] **Action group:** Email notification on budget threshold breach
- [ ] **Resource group:** Create `insurance-ai-rg` — all resources in one group for easy cost tracking
- [ ] **Tags:** Add `project:insurance-ai-hub` and `budget:free-tier` to every resource

### Per-Service Safety Checks

- [ ] **Always select F0/Free SKU** when creating Cognitive Services resources
- [ ] **Application Insights:** Set daily cap to 0.16 GB/day immediately after creation
- [ ] **Cosmos DB:** If used, lock throughput to 1,000 RU/s provisioned (NO autoscale)
- [ ] **App Service:** Use B1 only (not B2/B3), set deployment slots to 0
- [ ] **Storage:** Enable lifecycle management to auto-delete blobs > 90 days
- [ ] **Azure SQL:** Verify "Free offer" toggle is ON during creation

### Monthly Monitoring Routine

1. **Check Cost Management weekly:** Azure Portal > Cost Management > Cost Analysis
2. **Review Application Insights ingestion:** Ensure < 5GB/month
3. **Verify no surprise resources:** `az resource list --resource-group insurance-ai-rg --output table`
4. **Check budget alert status:** Ensure alerts are active and email is correct

---

## Recommended Adoption Order

| Phase | Services | Monthly Cost | Cumulative |
|-------|----------|-------------|------------|
| **Phase 0** (Now) | AI Document Intelligence F0 | $0 | $0 |
| **Phase 1** (Sprint 5) | Static Web Apps + Key Vault | $0 | $0 |
| **Phase 2** (Sprint 5) | App Service B1 + App Insights | $0 | $0 |
| **Phase 3** (Sprint 6) | Azure SQL Free + Blob Storage | $0 | $0 |
| **Phase 4** (Month 10) | Container Apps (replace App Service) | $0 | $0 |
| **Phase 5** (Future) | Functions + Event Grid + Service Bus | $0 | $0 |

**Total projected monthly cost: $0** (with proper SKU selection and daily caps)

---

## Quick Reference: Free Tier Limits at a Glance

```
=== AZURE AI SERVICES (F0 — hard cap, never bills) ===
AI Document Intelligence F0    500 pages/month         (12 months — ACTIVE)
AI Language F0                 5,000 text records/month (always free)
AI Content Safety F0           5K text + 5K images/mo  (always free)
AI Vision F0                   5,000 transactions/month(12 months)
AI Translator F0               2M characters/month     (always free)
AI Speech F0 (STT)             5 audio hours/month     (always free)
AI Speech F0 (TTS)             500K neural chars/month (always free)
AI Face F0                     30,000 transactions/mo  (always free)
AI Anomaly Detector F0         20,000 transactions/mo  (always free)
AI Search Free                 50MB, 3 indexes         (always free)
AI Custom Vision F0            10K predictions/month   (12 months)
AI Immersive Reader F0         3M characters/month     (always free)
AI Speaker Recognition F0      10K+10K transactions/mo (always free)

=== INFRASTRUCTURE (various tiers) ===
Key Vault (Standard)           $0.03/10K ops           (no free tier, ~$0.01/mo)
Static Web Apps Free           100 GB bandwidth/month  (always free)
Container Apps Consumption     180K vCPU-s/month       (always free)
App Service B1 Linux           750 hours/month         (12 months only)
Azure SQL Free                 100K vCore-s/month      (always free)
Application Insights           5 GB ingestion/month    (SET DAILY CAP!)
Blob Storage LRS               5 GB + 20K reads/month  (12 months only)
Azure Functions                1M executions/month     (always free)
Communication Services Email   100 emails/day          (always free)
```

---

*Generated for Insurance AI Operations Hub — Pay-as-you-go subscription with $1 budget alert.*
*All "always free" services remain free indefinitely. "12 months free" services need migration plan at month 10.*
