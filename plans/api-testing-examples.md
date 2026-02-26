onRevertConfirmed() {
    if (!this.stateToRevert || this.isReverting()) return;

    this.isReverting.set(true);
    const scenarioId = this.activeScenarioId();
    this.applicationService
      .triggerComponentCommand(
        AppService.TENANT_ID,
        AppService.PRODUCT_ID,
        AppService.APP_FLOW_ID,
        this.componentContext().aggregateId!,
        this.componentId(),
        'revert',
        {
          CustomProperties: {
            RevertId: this.stateToRevert.eventId,
          },
          Inputs: {},
        },
        scenarioId,
      )
      .subscribe({
        next: () => {
          this.isReverting.set(false);
          this.canvasToastService.success(
            'Component state reverted successfully',
          );

          // Reset isFinalClick so the child component's ngOnChanges
          // calls resetInputModel() with the reverted data.
          // After save, isFinalClick stays true which blocks resetInputModel().

          this.applicationService.sendEvent({
            action: 'refresh',
            onReload: true,
          });
          this.stateToRevert = null;
        },
        error: (err) => {
          this.isReverting.set(false);
          this.canvasToastService.error('Failed to revert component state');
          console.error(err);
          this.stateToRevert = null;
        },
      });
  }# InsureSense AI — Testing Guide

> **Who is this for?** Anyone testing the app — QA, product, UX, or developers. No coding required for UI tests.
>
> **App URL:** Open `http://localhost:4200` in your browser
>
> **Last verified:** Feb 26, 2026 — All 27 endpoints pass, all 15 pages render correctly

---

## Table of Contents

1. [How to Start the App](#1-how-to-start-the-app)
2. [Analyze Customer Sentiment (v1)](#2-analyze-customer-sentiment)
3. [Insurance Analysis (v2 — Full AI Pipeline)](#3-insurance-analysis)
4. [Claims Triage](#4-claims-triage)
5. [Fraud Detection](#5-fraud-detection)
6. [Cross-Claim Fraud Correlation](#6-cross-claim-fraud-correlation)
7. [Document Intelligence (Upload & Ask Questions)](#7-document-intelligence)
8. [CX Copilot (AI Chat Assistant)](#8-cx-copilot)
9. [Provider Health Monitor](#9-provider-health-monitor)
10. [Dashboard](#10-dashboard)
11. [What Errors Look Like](#11-what-errors-look-like)
12. [API Quick Reference (for developers)](#12-api-quick-reference)

---

## 1. How to Start the App

Open **two terminals** and run:

**Terminal 1 — Backend:**
```
cd SentimentAnalyzer/Backend
dotnet run
```
Wait for: `Now listening on: http://localhost:5143`

**Terminal 2 — Frontend:**
```
cd SentimentAnalyzer/Frontend/sentiment-analyzer-ui
npx ng serve --configuration e2e --port 4200
```
Wait for: `Local: http://localhost:4200/`

> The `--configuration e2e` flag lets you skip login. Remove it to test with real Supabase auth.

Open **http://localhost:4200** in your browser. You should see the landing page with "9 AI Agents. 5 LLM Providers. 1 Intelligent Platform."

---

## 2. Analyze Customer Sentiment

**Go to:** http://localhost:4200/sentiment

This is the v1 legacy analyzer — it tells you if a message is positive, negative, or neutral.

### Test 1: Angry customer

**Paste this into the text box:**
> I reported water damage to my kitchen on January 15th. It has been three weeks and I have not received any response from the adjuster. My kitchen floor is warping and mold is starting to grow. This is completely unacceptable service.

**Click "Analyze Sentiment"**

**You should see:**
- Red **"Negative"** badge
- Confidence around **90%**
- Emotion bars showing high **anger** and **frustration**
- An explanation paragraph describing why the sentiment is negative

### Test 2: Happy customer

**Paste this:**
> I want to thank my agent Sarah for handling my auto claim so quickly. From filing to settlement in just 5 days. Outstanding service. I have been with this company for 8 years and this experience reinforces why I stay.

**You should see:**
- Green **"Positive"** badge
- Confidence around **95%**
- Emotion bars showing high **joy** and **gratitude**

---

## 3. Insurance Analysis

**Go to:** http://localhost:4200/insurance

This is the v2 multi-agent analyzer — it gives you 7 dimensions of insight, not just sentiment.

### Test 1: Billing dispute (high churn risk)

1. **Select "Complaint"** from the Interaction Type dropdown (or click the "Billing Dispute" quick template)
2. **Paste this:**
> My premium went from $695 to $847 overnight with no explanation. I have called 3 times and gotten different answers each time. One person said it was a rate change, another blamed my credit score, and the third said there was no increase on record. I demand a written explanation. If this is not resolved within 48 hours, I will file a complaint with the state insurance commissioner.

3. **Click "Analyze"**

**You should see a results card with:**
- **Sentiment:** Negative (red badge)
- **Purchase Intent:** Very low (around 10-20%)
- **Customer Persona:** Something like "Claim-Frustrated" or "PreScreened"
- **Risk Indicators:** High churn risk, High complaint escalation risk
- **Quality Score:** Badge showing analysis reliability

### Test 2: Renewal inquiry (loyal customer)

1. **Select "General"** from Interaction Type
2. **Paste this:**
> My auto policy is up for renewal next month. I have been a loyal customer for 12 years with zero claims. I noticed my premium went up by $45 this year. What safe-driver discounts are available? Would bundling my homeowners and auto policies save money?

3. **Click "Analyze"**

**You should see:**
- **Sentiment:** Neutral or Positive
- **Purchase Intent:** Moderate-high (65-80%)
- **Churn Risk:** Low
- **Policy Recommendations:** Suggestions for bundling discounts

> **Tip:** Click the **"History"** button (top-right) to see all past analyses.

---

## 4. Claims Triage

**Go to:** http://localhost:4200/claims/triage

Submit a claim description and the AI will assess severity, urgency, fraud risk, and recommend next steps.

> **Note: LLM Non-Determinism** — AI responses vary between runs. The same input may produce different severity/urgency/fraud scores each time. The expected values below are *typical* results, not guaranteed. If you get a weaker result (e.g., "Medium" instead of "Critical"), run the same text again — the AI often classifies more accurately on a second attempt.

### Test 1: Emergency water damage (should be Critical)

1. **Select Interaction Type:** `Complaint`
2. **Paste this into the Claim Description box** (or click the **"Water Damage"** quick template):
> Emergency — water pipe burst in my basement at 3 AM. The entire lower level is flooded with 4 inches of standing water. Significant damage to hardwood floors, drywall up to 2 feet, and furnace. Water is still actively leaking despite shutting off the main valve. I need emergency mitigation services immediately.

3. **Click "Triage Claim"**

**You should see:**
- **Severity:** Critical (red badge)
- **Urgency:** Immediate (orange badge)
- **Claim Type:** Property Damage
- **Estimated Loss:** $10,000 - $50,000 range
- **Fraud Score:** Low (green gauge, around 0-20)
- **Recommended Actions:** "Dispatch Emergency Services" (High priority), "Notify Adjuster" (High), possibly temporary accommodation

### Test 2: Suspicious high-value claim (should flag High fraud)

1. **Select Interaction Type:** `Email`
2. **Paste this into the Claim Description box:**
> I am filing a claim for a total loss on my 2024 BMW X5 valued at $85,000. The vehicle caught fire in my driveway last night around 2 AM with no witnesses. I had just increased my coverage limits three weeks ago. This is my third claim in the past 8 months — the first was a theft claim for jewelry and electronics worth $12,000, and the second was water damage at $25,000. I do not have a fire department report yet but the car is completely destroyed. I need the full payout as quickly as possible as I am behind on payments.

3. **Click "Triage Claim"**

**You should see:**
- **Severity:** High (red/orange badge)
- **Urgency:** High
- **Claim Type:** Auto or Property Damage
- **Estimated Loss:** $50,000+ range
- **Fraud Score:** High (red gauge, 60-90+ range) — multiple red flags: recent coverage increase, no witnesses, 2 AM timing, third claim in 8 months, pressure for fast payout, behind on payments
- **Fraud Flags:** Should list indicators like "Recent policy change", "Multiple prior claims", "No witnesses", "Financial pressure"
- **Recommended Actions:** "Refer to SIU (Special Investigations Unit)", "Request fire department report", "Review prior claims history"

### Test 3: Upload evidence files (multimodal AI analysis)

After triaging a claim (Test 1 or Test 2), you can attach evidence files. The AI uses multimodal services (Azure Vision, Cloudflare Vision, OCR.space, AssemblyAI, HuggingFace) to analyze them.

The upload zone on the triage page accepts: **images** (JPG, PNG), **audio** (WAV, MP3), and **PDFs**.

#### 3A: Upload a damage photo (image)
1. **Triage a claim first** (use Test 1 water damage text)
2. **Drag or click** the "Attach Evidence" upload zone below the claim text
3. **Select a JPG/PNG image** — e.g., a photo of water damage, a dented car, a damaged roof
   - Sample images to try: any photo from your phone, a screenshot, or download a stock damage photo
4. **The AI will analyze the image** and add an evidence record to the claim

**You should see:**
- Upload progress indicator
- Evidence type: "Image"
- AI description of what it detected in the photo (e.g., "Water damage visible on hardwood flooring with standing water")
- Evidence attached to the claim detail page

#### 3B: Upload an audio recording (voice statement)
1. **On the same claim**, upload a **.wav or .mp3** file — e.g., a recorded phone call, policyholder statement, or adjuster notes
   - You can record a quick voice memo on your phone describing damage
2. **The AI will transcribe the audio** (via AssemblyAI/HuggingFace) and attach it as evidence

**You should see:**
- Evidence type: "Audio"
- Transcription of the recording in the evidence details

#### 3C: Upload a PDF document (police report, estimate)
1. **Upload a .pdf file** — e.g., a police report, contractor repair estimate, medical bill, or any insurance document
2. **The AI will OCR the PDF** and extract key information

**You should see:**
- Evidence type: "Document"
- Extracted text content from the PDF

#### 3D: Upload via curl (API direct)
```bash
# Upload an image to claim ID 1
curl -X POST http://localhost:5143/api/insurance/claims/upload \
  -F "file=@/path/to/damage-photo.jpg" \
  -F "claimId=1"

# Upload a PDF to claim ID 2
curl -X POST http://localhost:5143/api/insurance/claims/upload \
  -F "file=@/path/to/police-report.pdf" \
  -F "claimId=2"

# Upload an audio file to claim ID 1
curl -X POST http://localhost:5143/api/insurance/claims/upload \
  -F "file=@/path/to/statement.wav" \
  -F "claimId=1"
```

**Constraints:** Max file size 10 MB. Supported types: `image/*`, `audio/*`, `application/pdf`.

> **Tip:** You don't need "real" insurance documents. Any photo, audio clip, or PDF will work — the AI analyzes whatever you give it.

### After triaging, try these:

- **Go to** http://localhost:4200/claims/history → You'll see all claims in a table. Click a row to view details.
- **Go to** http://localhost:4200/claims/1 → Full detail view with triage assessment, fraud gauge, evidence list, and recommended actions.

---

## 5. Fraud Detection

**Go to:** http://localhost:4200/dashboard/fraud

This page shows claims with a fraud score above 55 (flagged for investigation).

### Test: Create a suspicious claim, then analyze it

1. **Go to** http://localhost:4200/claims/triage
2. **Select Interaction Type:** `Call`
3. **Paste this suspicious claim:**
> Reporting theft of all electronics from my rental apartment while on vacation. Items include a 75-inch Samsung TV purchased 2 months ago, MacBook Pro, two iPads, PlayStation 5, a Rolex watch, and about $3,000 cash. No signs of forced entry. My neighbor did not see anything unusual. I do not have receipts for most items. I should mention I increased my coverage limits just 3 weeks before this incident.

4. **Click "Triage Claim"** — Note the claim ID in the response
5. **On the claim detail page,** click **"Run Deep Fraud Analysis"**

**You should see the fraud score jump significantly because of:**
- Coverage increased shortly before the incident (Timing indicator)
- No receipts for high-value items (Documentation indicator)
- No signs of forced entry (Pattern indicator)
- High total value with recent coverage increase (Financial indicator)

6. **Now go to** http://localhost:4200/dashboard/fraud — If the fraud score is above 55, the claim will appear here with an SIU referral recommendation.

---

## 6. Cross-Claim Fraud Correlation

**Go to:** http://localhost:4200/fraud/correlations/1 (replace `1` with any claim ID)

This analyzes patterns across multiple claims to detect organized fraud. It uses 4 strategies:
- **DateProximity** — Claims filed close together
- **SimilarNarrative** — Claims with similar text
- **SharedFlags** — Claims sharing the same fraud indicators
- **SameSeverity** — Claims at the same severity level

### Test: Run correlation analysis

1. Make sure you have at least 2-3 claims in the system (from the triage tests above)
2. **Go to** http://localhost:4200/fraud/correlations/1
3. **Click "Run New Analysis"**

**If correlations are found, you'll see:**
- Split cards showing source claim vs. correlated claim
- Strategy badges (blue, purple, orange, red) for each detection method
- Confidence score for each correlation
- **Confirm** or **Dismiss** buttons for each correlation

**If no correlations are found** (common with only 2-3 unrelated claims), you'll see an empty state: "No correlations found. Run a new analysis to detect cross-claim fraud patterns."

> **Tip:** To see correlations in action, triage 2-3 similar claims (e.g., multiple water damage claims filed close together with overlapping fraud flags).

---

## 7. Document Intelligence

Upload insurance documents (PDFs, images) and ask questions about them. The AI reads, indexes, and answers with source citations.

### Test 1: Upload a policy document

1. **Go to** http://localhost:4200/documents/upload
2. **Select "Policy"** from the Document Category dropdown
3. **Drag-and-drop a PDF file** onto the upload zone (or click "browse")
   - Accepted formats: PDF, PNG, JPEG, TIFF (max 5 MB)
   - Try with any real insurance policy PDF you have
4. **Click "Upload & Process"**

**You should see:** Processing completes with document details — file name, page count, chunk count, and "VoyageAI" as the embedding provider.

> **QA-verified result:** A 1-page homeowners policy PDF uploaded in ~2 seconds. Status: "Ready", 1 chunk, VoyageAI embeddings.

### Test 2: Ask a question about the uploaded document

1. **Go to** http://localhost:4200/documents/query
2. **Type a question** like:
   - "Does my policy cover water damage?"
   - "What is my deductible amount?"
   - "What are the coverage limits for personal property?"
3. **Click "Ask"**

**You should see:**
- An AI-generated answer citing specific sections of your document
- A confidence gauge
- Expandable citations showing the exact text the AI used to answer
- Source document name and section name for each citation

> **QA-verified results:**
> - "Does my policy cover water damage?" → Correctly identified: sudden plumbing discharge is COVERED, gradual seepage is NOT COVERED, flood requires separate policy, sewer backup covered with endorsement E-102
> - "What is my deductible?" → "$1,000 per occurrence"
> - PII auto-redacted: policy numbers appear as `[POLICY-REDACTED]` in citations

### Test 3: Ask without uploading anything

1. Delete all documents (or use a fresh database)
2. Ask any question

**You should see:** "No relevant document content found. Please upload documents first or rephrase your question."

---

## 8. CX Copilot

**Go to:** http://localhost:4200/cx/copilot

An AI chat assistant for insurance customer experience. It classifies the tone of its response and detects when a conversation needs human escalation.

### Test 1: Polite inquiry (no escalation)

**Type this in the chat box:**
> Hello, I would like to check the status of my auto claim from last week. Claim number AC-2024-55678. My adjuster said they would call me back within 48 hours but I have not heard anything yet.

**Press Enter or click Send**

**You should see:**
- The AI's response appears as a chat message (streaming text, word by word)
- **Tone badge:** "Professional" or "Empathetic" (blue/green)
- **No escalation indicator**
- A disclaimer at the bottom about AI-generated guidance

### Test 2: Frustrated customer (escalation triggered)

**Type this:**
> I have been waiting 6 weeks for my claim. Nobody will help me. I want to speak to a supervisor now or I am contacting my lawyer and the insurance commissioner.

**You should see:**
- **Tone badge:** "Empathetic" or "Urgent" (orange/red)
- **Escalation badge:** Pulsing red indicator with reason (mentions "lawyer" and "insurance commissioner")
- The AI acknowledges the frustration and recommends speaking with a supervisor
- Disclaimer footer

**Escalation trigger keywords include:** supervisor, manager, lawyer, attorney, insurance commissioner, sue, lawsuit, legal action, BBB, media, news station, terrible, unacceptable, worst, horrible

### Test 3: Add claim context (optional)

1. Click **"Add claim context"** toggle
2. Enter a claim ID or description
3. Send a message — the AI will reference the specific claim in its response

---

## 9. Provider Health Monitor

**Go to:** http://localhost:4200/dashboard/providers

Shows real-time status of all AI providers powering the system.

**You should see two sections:**

### LLM Providers (7 providers)
A horizontal fallback chain showing: **Groq → Cerebras → Mistral → Gemini → OpenRouter → OpenAI → Ollama**

Each provider card shows:
- **Status:** Healthy (green), Degraded (yellow), or Down (red)
- **Available:** Yes/No
- **Consecutive Failures:** How many times it failed in a row
- **Cooldown:** If degraded, how long until it's retried (30s → 60s → 120s → 300s max)

### Multimodal Services (6 services)
- **Deepgram** — Speech-to-Text
- **AzureVision** — Image Analysis
- **CloudflareVision** — Image Fallback
- **OcrSpace** — Document OCR
- **HuggingFace** — Entity Extraction
- **Voyage AI** — Finance Embeddings

Each shows Configured/Not Configured and Available/Unavailable status.

> The page auto-refreshes every 30 seconds. Click the refresh button for an immediate update.

---

## 10. Dashboard

**Go to:** http://localhost:4200/dashboard

The analytics hub showing aggregated metrics from all your tests.

**Top row — Sentiment Metrics:**
- **Total Analyses** — How many insurance analyses you've run
- **Avg Purchase Intent** — Average purchase intent score across all analyses
- **Avg Sentiment** — Average sentiment confidence score
- **High Risk Alerts** — Count of analyses flagged for churn or complaint escalation

**Second row — Claims Metrics:**
- **Total Claims** — How many claims have been triaged
- **Critical Severity** — Claims needing immediate attention
- **Avg Fraud Score** — Average fraud score across all claims (out of 100)

**Charts:**
- **Sentiment Distribution** — Doughnut chart (Positive/Negative/Neutral/Mixed)
- **Customer Personas** — Bar chart showing persona breakdown

**Quick Actions:** Direct links to Claims Triage, Claims History, Provider Health, and Fraud Alerts

**Recent Analyses Table:** Last 10 analyses with sentiment, purchase intent, and timestamp

---

## 11. What Errors Look Like

The app handles errors gracefully. Here's what you'll see:

| What you do | What happens | Where to see it |
|-------------|-------------|-----------------|
| Submit empty text | "Text cannot be empty" message | Any analysis page |
| Look up a claim that doesn't exist | "Claim not found" alert | `/claims/99999` |
| Look up a non-existent document | "Document not found" alert | `/documents/99999` |
| Ask a question with no documents uploaded | "Please upload documents first" answer | `/documents/query` |
| All AI providers are down | "An unexpected error occurred" with retry option | Any analysis page |
| Hit rate limit | "Too many requests, please try again later" | After rapid-fire submissions |

**Rate limits per endpoint:**
- Sentiment/Insurance analysis: 10 per minute
- Claims triage: 5 per minute
- Fraud analysis: 5 per minute
- Document upload: 3 per minute

---

## 12. API Quick Reference (for developers)

For developers who want to test via curl or Postman. All endpoints use `http://localhost:5143`.

### Health Checks
```bash
curl http://localhost:5143/api/sentiment/health
curl http://localhost:5143/api/insurance/health
curl http://localhost:5143/api/insurance/health/providers
```

### Sentiment Analysis (v1)
```bash
curl -X POST http://localhost:5143/api/sentiment/analyze \
  -H "Content-Type: application/json" \
  -d '{"text": "Your text here"}'
```

### Insurance Analysis (v2)
```bash
curl -X POST http://localhost:5143/api/insurance/analyze \
  -H "Content-Type: application/json" \
  -d '{"text": "Your text here", "interactionType": "Complaint"}'
# interactionType: General, Email, Call, Chat, Review, Complaint
```

### Claims Triage
```bash
curl -X POST http://localhost:5143/api/insurance/claims/triage \
  -H "Content-Type: application/json" \
  -d '{"text": "Claim description here", "interactionType": "Complaint"}'
```

### Claims History & Detail
```bash
curl "http://localhost:5143/api/insurance/claims/history?page=1&pageSize=20"
curl http://localhost:5143/api/insurance/claims/1
```

### Fraud Analysis
```bash
curl -X POST http://localhost:5143/api/insurance/fraud/analyze \
  -H "Content-Type: application/json" \
  -d '{"claimId": 1}'

curl http://localhost:5143/api/insurance/fraud/score/1
curl http://localhost:5143/api/insurance/fraud/alerts
```

### Fraud Correlation
```bash
curl -X POST http://localhost:5143/api/insurance/fraud/correlate \
  -H "Content-Type: application/json" \
  -d '{"claimId": 1}'

curl "http://localhost:5143/api/insurance/fraud/correlations/1?page=1&pageSize=20"
```

### Document Upload & Query
```bash
# Upload (multipart form)
curl -X POST http://localhost:5143/api/insurance/documents/upload \
  -F "file=@your-policy.pdf;type=application/pdf" \
  -F "category=Policy"

# Query
curl -X POST http://localhost:5143/api/insurance/documents/query \
  -H "Content-Type: application/json" \
  -d '{"question": "Does my policy cover water damage?"}'

# History & Detail
curl "http://localhost:5143/api/insurance/documents/history?page=1&pageSize=10"
curl http://localhost:5143/api/insurance/documents/1
```

### CX Copilot
```bash
# Non-streaming
curl -X POST http://localhost:5143/api/insurance/cx/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Your question here"}'

# SSE Streaming
curl -X POST http://localhost:5143/api/insurance/cx/stream \
  -H "Content-Type: application/json" \
  -d '{"message": "Your question here"}'
```

### Dashboard & History
```bash
curl http://localhost:5143/api/insurance/dashboard
curl http://localhost:5143/api/insurance/history
```

---

## Response Shape Reference

For developers verifying frontend-backend contracts:

| Endpoint | Key Response Fields |
|----------|-------------------|
| `POST /api/sentiment/analyze` | `sentiment`, `confidenceScore`, `explanation`, `emotionBreakdown` |
| `POST /api/insurance/analyze` | `sentiment`, `confidenceScore`, `insuranceAnalysis { purchaseIntentScore, customerPersona, journeyStage, riskIndicators, policyRecommendations }`, `quality` |
| `POST /api/insurance/claims/triage` | `claimId`, `severity`, `urgency`, `claimType`, `fraudScore`, `fraudRiskLevel`, `estimatedLossRange`, `recommendedActions[]` |
| `POST /api/insurance/fraud/analyze` | `claimId`, `fraudScore`, `riskLevel`, `indicators[]`, `recommendedActions[]`, `referToSIU`, `confidence` |
| `POST /api/insurance/fraud/correlate` | `claimId`, `correlations[]`, `count` |
| `POST /api/insurance/documents/upload` | `documentId`, `fileName`, `status`, `pageCount`, `chunkCount`, `embeddingProvider` |
| `POST /api/insurance/documents/query` | `answer`, `confidence`, `citations[]`, `llmProvider`, `elapsedMilliseconds` |
| `POST /api/insurance/cx/chat` | `response`, `tone`, `escalationRecommended`, `escalationReason`, `llmProvider`, `elapsedMilliseconds`, `disclaimer` |
| `POST /api/insurance/cx/stream` | SSE: `data: { type:"content", content:"token", metadata }` |
| `GET /api/insurance/dashboard` | `metrics`, `sentimentDistribution`, `topPersonas` |
| `GET /api/insurance/health/providers` | `llmProviders[]`, `multimodalServices[]` |
| Paginated endpoints | `items[]`, `totalCount`, `page`, `pageSize`, `totalPages` |
