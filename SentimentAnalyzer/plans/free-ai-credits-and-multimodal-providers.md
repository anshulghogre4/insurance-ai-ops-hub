# Free AI Credits & Multimodal Providers — Reference Guide

> **Compiled by:** AI Expert Agent + Business Analyst Agent
> **Date:** 2026-02-23 (Updated)
> **Purpose:** Track all available free AI credits, multimodal providers, and new trends for the Insurance AI Operations Hub
> **Status:** Active — update as providers change tiers

---

## 1. Signup Credits (One-Time Free Money)

Sign up, get credits, use them before they expire.

| # | Provider | Free Credits | Expires | What You Get | Sign Up | Status |
|---|----------|-------------|---------|-------------|---------|--------|
| 1 | **xAI (Grok)** | **$25 free** | 30 days | Grok 4 + Grok 4.1 Fast, **2M context window** | [console.x.ai](https://console.x.ai) | ☐ Not signed up |
| 2 | **Together AI** | **$25 free** | 60 days | Llama 4, Qwen3, Mistral, 200+ models | [api.together.xyz](https://api.together.xyz) | ☐ Not signed up |
| 3 | **Deepgram** | **$200 free** | **No expiry** | Speech-to-text, best free STT deal | [deepgram.com](https://deepgram.com) | ✅ Active |
| 4 | **Google Cloud** | **$300 free** | 90 days | Vision AI, OCR, Speech, Translation | [cloud.google.com/free](https://cloud.google.com/free) | ☐ Not signed up |
| 5 | **RunPod** | **$500 free** | 12 months | Serverless GPU, deploy any model | [runpod.io](https://runpod.io) | ☐ Not signed up |
| 6 | **AssemblyAI** | **$50 free** | One-time | STT + sentiment + PII detection built-in | [assemblyai.com](https://assemblyai.com) | ☐ Not signed up |
| 7 | **DeepSeek** | **5M tokens** | 30 days | DeepSeek R1 (near GPT-4 reasoning) | [platform.deepseek.com](https://platform.deepseek.com) | ☐ Not signed up |
| 8 | **SambaNova** | **$5 free** (~30M tokens on 8B) | 30 days | Llama, DeepSeek, Qwen, Whisper STT | [cloud.sambanova.ai](https://cloud.sambanova.ai) | ☐ Not signed up |
| 9 | **Fireworks AI** | **$1 free** | No expiry | Llama, Mixtral, FireLLaVA multimodal | [fireworks.ai](https://fireworks.ai) | ☐ Not signed up |

**Total available: ~$1,106+ in free credits**

---

## 2. Permanent Free Tiers (Never Expire)

Long-term workhorses — no expiration, no credit card required.

| # | Provider | Free Limit | Multimodal? | Best For | Sign Up | Status |
|---|----------|-----------|-------------|----------|---------|--------|
| 1 | **Groq** | 14,400 req/day | Text only | Fastest inference (300 tok/s), real-time | [console.groq.com](https://console.groq.com) | ✅ Active |
| 2 | **Mistral** | 1B tokens/month | Text only | Highest volume free tier | [console.mistral.ai](https://console.mistral.ai) | ✅ Active |
| 3 | **Gemini** (Google AI Studio) | 250 req/day (Pro), 1000/day (Flash) | **YES — vision + audio + video** | Long docs (1M context), image analysis | [aistudio.google.com](https://aistudio.google.com) | ✅ Active |
| 4 | **Cerebras** | **1M tokens/day** | Text only | **Fastest inference (2,600 tok/s)**, Qwen3 235B | [cloud.cerebras.ai](https://cloud.cerebras.ai) | ✅ Active |
| 5 | **Cloudflare Workers AI** | 10,000 neurons/day | **YES — vision models** | Edge inference, Llama 4 Scout, Gemma 3 | [dash.cloudflare.com](https://dash.cloudflare.com) | ✅ Active |
| 6 | **OpenRouter** | 50 req/day (free models) | **YES — via free models** | Access 24+ free models, Gemma 3 | [openrouter.ai](https://openrouter.ai) | ✅ Active |
| 7 | **HuggingFace** | Rate-limited | **YES — 300+ vision models** | Specialized models, NER, classification | [huggingface.co](https://huggingface.co) | ✅ Active |
| 8 | **Ollama** | Unlimited (local) | **YES — LLaVA, Llama 3.2 Vision** | PII-safe, offline, zero cost | Local install | ✅ Installed |
| 9 | **OCR.space** | 500 req/day | OCR only | Document text extraction | [ocr.space](https://ocr.space) | ✅ Active |
| 10 | **Voyage AI** | **50M tokens** (finance-2), **200M** (general) | Embeddings only | **Finance-domain embeddings** (RAG) | [dash.voyageai.com](https://dash.voyageai.com) | ☐ Not signed up |
| 11 | **Google Cloud Vision** | 1,000 units/month | Image analysis | OCR, labels, face detection | [cloud.google.com/vision](https://cloud.google.com/vision) | ☐ Not signed up |
| 12 | **GitHub Models** | 50 req/day (GPT-4o), 150/day (small) | Text only | Free GPT-4o access for testing | [github.com/marketplace/models](https://github.com/marketplace/models) | ☐ Not signed up |
| 13 | **Chutes AI** | Free tier (decentralized) | Text only | DeepSeek R1, Llama 3.1 via Bittensor | [chutes.ai](https://chutes.ai) | ☐ Not signed up |
| 14 | **AIML API** | Free tier | Text + Image | 400+ models, GPT-4o, Claude, Gemini | [aimlapi.com](https://aimlapi.com) | ☐ Not signed up |

---

## 3. Voyage AI — Signup Instructions

### Why Voyage AI for Insurance?

**`voyage-finance-2`** is purpose-built for financial/insurance domain text. It outperforms OpenAI embeddings by **7%** and Cohere by **12%** on financial retrieval benchmarks. Perfect for:
- Insurance policy document retrieval (RAG)
- Claims text semantic search
- Financial report embedding
- Numerical reasoning over tables

### Step-by-Step Signup

1. Go to **[https://dash.voyageai.com](https://dash.voyageai.com)**
2. **Create an account** — sign up with email, Google, or GitHub (no credit card required)
3. Once logged in, navigate to the **API Keys** section
4. Click **"Create new secret key"**
5. **Copy and store immediately** — the key is shown only once
6. Set in .NET user secrets:
   ```bash
   dotnet user-secrets set "AgentSystem:Voyage:ApiKey" "your-key-here"
   ```

### Free Tier Allocation (One-Time, Not Monthly)

| Model Category | Free Tokens | Notes |
|----------------|-------------|-------|
| **voyage-finance-2** (insurance/finance) | **50 million** | ~37,500 pages of insurance documents |
| General models (voyage-4-large, voyage-4, voyage-4-lite) | **200 million** | General-purpose embeddings |
| Multimodal (voyage-multimodal-3.5) | **200M text + 150B pixels** | Image + text embeddings |
| Rerankers (rerank-2.5) | **200 million** | Re-rank search results |

### Rate Limits (Free Tier)

| Model | RPM | TPM |
|-------|-----|-----|
| voyage-finance-2 | 2,000 | 3M |
| voyage-4-large | 2,000 | 3M |
| voyage-4 | 2,000 | 8M |
| voyage-4-lite | 2,000 | 16M |

### API Usage Example

```bash
# Embed insurance text with voyage-finance-2
curl https://api.voyageai.com/v1/embeddings \
  -H "Authorization: Bearer $VOYAGE_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "input": ["Water damage claim filed Jan 15, 2024. Policy HO-2024-789456. Estimated loss: $15,000."],
    "model": "voyage-finance-2",
    "input_type": "document"
  }'
```

### Key Facts
- **Acquired by MongoDB** — service continues independently
- **Does NOT train on your data** — safe for PII/insurance compliance
- **OpenAI-similar format** — `Bearer` auth, `/v1/embeddings` endpoint
- **Base URL**: `https://api.voyageai.com/v1`
- Free tokens are **one-time allotment**, not monthly renewal

---

## 4. Cerebras — Setup Reference

### Why Cerebras?

**World's fastest inference** — 2,600 tokens/second on Llama 3.3 70B (20x faster than NVIDIA GPU inference). OpenAI-compatible API format.

### Signup

1. Go to **[https://cloud.cerebras.ai](https://cloud.cerebras.ai)**
2. Sign up (no credit card required)
3. Get API key from dashboard
4. Set in .NET user secrets:
   ```bash
   dotnet user-secrets set "AgentSystem:Cerebras:ApiKey" "your-key-here"
   ```

### Free Tier

| Limit | Value |
|-------|-------|
| Tokens per day | **1,000,000** |
| Requests per minute | 30 |
| Tokens per minute | 60,000 |

### Available Models (Free Tier)

| Model | Parameters | Context | Speed |
|-------|-----------|---------|-------|
| **Qwen 3 235B** (Instruct + Thinking) | 235B MoE | 128K | ~2,600 tok/s |
| **GPT-OSS 120B** | 120B | 128K | ~1,500 tok/s |
| **ZAI GLM-4.7** | Unknown | 128K | Fast |

### API Format (OpenAI-Compatible)

```bash
curl https://api.cerebras.ai/v1/chat/completions \
  -H "Authorization: Bearer $CEREBRAS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "qwen-3-235b-a22b-instruct",
    "messages": [{"role": "user", "content": "Analyze this claim..."}]
  }'
```

---

## 5. Multimodal Capabilities Map

What each modality unlocks for insurance:

| Modality | Best Free Provider | Free Limit | Insurance Use Case |
|----------|-------------------|-----------|-------------------|
| **Text → Analysis** | Groq / Mistral / Cerebras | 14.4K/day, 1B tok/mo, 1M tok/day | Claims triage, sentiment, fraud scoring |
| **Image → Text** (damage photos) | Gemini 2.0 Flash | 1000 req/day | Photograph claim damage, get AI description |
| **Image → Text** (local/PII-safe) | Ollama + LLaVA | Unlimited | Analyze damage photos without sending to cloud |
| **Document → Text** (OCR) | OCR.space / Google Vision | 500/day, 1K/month | Extract text from scanned policy PDFs |
| **Speech → Text** (call recordings) | Deepgram | $200 free, no expiry | Transcribe policyholder calls for sentiment analysis |
| **Text → Embeddings** (RAG) | Voyage AI (finance-2) | 50M tokens | Semantic search over policy/claims documents |
| **Text → Speech** | Cloudflare Workers AI | 10K neurons/day | Read-aloud claim summaries for accessibility |
| **Video → Analysis** | Gemini 2.0 Flash | Part of 1000 req/day | Analyze video damage evidence |

---

## 6. Provider Chain Strategy

### Current Active Chain (6 Providers)
```
Groq (Llama 3.3 70B) → Mistral (Large 2) → Gemini (2.5 Flash) → OpenRouter (Gemma 3 27B) → OpenAI (GPT-4o-mini) → Ollama (local)
```

### Expanded Chain (With New Providers)
```
TEXT Analysis:
  Groq → Cerebras → Mistral → Gemini → OpenRouter → OpenAI → Ollama

IMAGE Analysis (damage photos, documents):
  Gemini Flash (free, vision) → Azure Vision → Cloudflare Workers AI → Ollama LLaVA (local)

DOCUMENT OCR:
  OCR.space (500/day) → Google Vision (1K/month) → Gemini (PDF native)

SPEECH-TO-TEXT:
  Deepgram ($200 free) → SambaNova Whisper → Browser SpeechRecognition API (zero cost)

EMBEDDINGS (RAG):
  Voyage AI finance-2 (50M tokens) → Ollama nomic-embed-text (unlimited, local)

COMPLEX REASONING:
  xAI Grok ($25 credit) → DeepSeek R1 (via Chutes/OpenRouter) → Gemini Pro
```

---

## 7. Complete Free Model Inventory (60+ Models)

### Part 1: LLM Text Models (30 Models)

| # | Model | Provider | Parameters | Context | Speed | Free Limit | Best For |
|---|-------|----------|-----------|---------|-------|-----------|----------|
| 1 | **Llama 3.3 70B** | Groq | 70B | 128K | 300 tok/s | 14,400 req/day | Real-time claims analysis |
| 2 | **Llama 3.1 8B** | Groq | 8B | 128K | 750 tok/s | 14,400 req/day | Lightweight, fast processing |
| 3 | **Llama 3.1 8B** | Cerebras | 8B | 128K | 2,600 tok/s | 1M tok/day | Ultra-fast inference |
| 4 | **Qwen 3 235B Instruct** | Cerebras | 235B MoE | 128K | ~2,600 tok/s | 1M tok/day | Best free reasoning model |
| 5 | **Qwen 3 235B Thinking** | Cerebras | 235B MoE | 128K | ~2,600 tok/s | 1M tok/day | Chain-of-thought reasoning |
| 6 | **GPT-OSS 120B** | Cerebras | 120B | 128K | ~1,500 tok/s | 1M tok/day | OpenAI open-source model |
| 7 | **ZAI GLM-4.7** | Cerebras | Unknown | 128K | Fast | 1M tok/day | Multilingual, code |
| 8 | **Mistral Large 2** | Mistral | ~123B | 128K | Medium | 1B tok/month | Bulk processing, highest volume |
| 9 | **Mistral Small 3.1** | Mistral | 24B | 128K | Fast | 1B tok/month | Fast, efficient |
| 10 | **Codestral** | Mistral | 22B | 256K | Fast | 1B tok/month | Code generation, longest context |
| 11 | **Mistral Nemo** | Mistral | 12B | 128K | Fast | 1B tok/month | Lightweight text tasks |
| 12 | **Gemini 2.5 Flash** | Google AI Studio | Unknown | 1M | Fast | 1000 req/day | Long document analysis (thinking) |
| 13 | **Gemini 2.5 Pro** | Google AI Studio | Unknown | 1M | Slower | 250 req/day | Complex reasoning |
| 14 | **Gemini 2.0 Flash-Lite** | Google AI Studio | Unknown | 1M | Fastest | 1500 req/day | High-volume simple tasks |
| 15 | **Gemma 3 27B** | OpenRouter | 27B | 128K | Medium | Free (rate-limited) | Quality open model |
| 16 | **Gemma 3 27B** | Cloudflare | 27B | 128K | Medium | 10K neurons/day | Edge inference |
| 17 | **Llama 4 Scout** | Cloudflare | 109B MoE | 10M | Fast | 10K neurons/day | Massive context, natively multimodal |
| 18 | **DeepSeek R1** | Chutes AI | 671B MoE | 128K | Medium | Free tier | Chain-of-thought reasoning |
| 19 | **DeepSeek V3** | Chutes AI | 671B MoE | 128K | Medium | Free tier | General-purpose powerhouse |
| 20 | **GPT-4o** | GitHub Models | Unknown | 128K | Medium | 50 req/day | Free GPT-4o access (testing) |
| 21 | **GPT-4o-mini** | GitHub Models | Unknown | 128K | Fast | 150 req/day | Lightweight GPT |
| 22 | **Phi-4** | GitHub Models | 14B | 128K | Fast | 150 req/day | Microsoft small model |
| 23 | **Grok 4** | xAI ($25 credit) | Unknown | 2M | Fast | Credit-based | Largest context window |
| 24 | **Grok 4.1 Fast** | xAI ($25 credit) | Unknown | 2M | Fastest | Credit-based | Real-time + huge context |
| 25 | **Llama 3.1 405B** | Together AI ($25) | 405B | 128K | Medium | Credit-based | Largest open Llama |
| 26 | **Qwen3 72B** | Together AI ($25) | 72B | 128K | Fast | Credit-based | Multilingual reasoning |
| 27 | **DeepSeek R1** | SambaNova ($5) | 671B MoE | 128K | Fast | 200K tok/day | Fast DeepSeek inference |
| 28 | **Llama 3.2 3B** | Ollama (local) | 3B | 128K | ~10 tok/s | Unlimited | PII-safe local processing |
| 29 | **Llama 3.1 8B** | Ollama (local) | 8B | 128K | ~8 tok/s | Unlimited | Local general-purpose |
| 30 | **Mistral 7B** | Ollama (local) | 7B | 32K | ~12 tok/s | Unlimited | Local fast inference |

### Part 2: Vision / Multimodal Models (12 Models)

| # | Model | Provider | Free Access | Capabilities |
|---|-------|----------|------------|-------------|
| 31 | **Gemini 2.5 Flash** | Google AI Studio | 1000 req/day | Image + audio + video + PDF understanding |
| 32 | **Gemini 2.0 Flash-Lite** | Google AI Studio | 1500 req/day | Fast image understanding |
| 33 | **Llama 4 Scout** | Cloudflare Workers AI | 10K neurons/day | Natively multimodal, 16 MoE experts |
| 34 | **Gemma 3 27B Vision** | Cloudflare Workers AI | 10K neurons/day | Vision + text, 128K context, 140+ languages |
| 35 | **Mistral Small 3.1 Vision** | Cloudflare Workers AI | 10K neurons/day | Vision + text, 128K context |
| 36 | **LLaVA 1.6** | Ollama (local) | Unlimited | Image → text, runs on CPU |
| 37 | **Llama 3.2 Vision 11B** | Ollama / HuggingFace | Unlimited / rate-limited | Image understanding |
| 38 | **Llama 3.2 Vision 90B** | HuggingFace | Rate-limited | High-quality image analysis |
| 39 | **FireLLaVA 13B** | Fireworks AI ($1 credit) | Credit-based | Multimodal chat |
| 40 | **Azure Computer Vision 4.0** | Azure (configured) | Pay-as-you-go | Damage detection, image captions |
| 41 | **Cloudflare Vision** | Cloudflare (configured) | 10K neurons/day | Image analysis fallback |
| 42 | **voyage-multimodal-3.5** | Voyage AI | 200M text + 150B pixels | Image + text embeddings |

### Part 3: Embedding Models for RAG (10 Models)

| # | Model | Provider | Free Limit | Dimensions | Best For |
|---|-------|----------|-----------|-----------|----------|
| 43 | **voyage-finance-2** | Voyage AI | **50M tokens** | 1024 | **Insurance/finance domain RAG** |
| 44 | **voyage-4-large** | Voyage AI | 200M tokens | 1024 | Best general-purpose retrieval |
| 45 | **voyage-4** | Voyage AI | 200M tokens | 1024 | Balanced quality/cost |
| 46 | **voyage-4-lite** | Voyage AI | 200M tokens | 1024 | Fastest, cheapest |
| 47 | **voyage-law-2** | Voyage AI | 50M tokens | 1024 | Legal domain (regulatory docs) |
| 48 | **voyage-code-3** | Voyage AI | 200M tokens | 1024 | Code search |
| 49 | **text-embedding-004** | Google (Gemini) | Free tier | 768 | Cloud embedding |
| 50 | **nomic-embed-text** | Ollama (local) | Unlimited | 768 | General document RAG |
| 51 | **mxbai-embed-large** | Ollama (local) | Unlimited | 1024 | High-accuracy retrieval |
| 52 | **all-minilm** | Ollama (local) | Unlimited | 384 | Lightweight, fast |

### Part 4: Speech-to-Text Models (6 Models)

| # | Model | Provider | Free Limit | Features |
|---|-------|----------|-----------|----------|
| 53 | **Nova-2** | Deepgram ($200 credit) | ~3,300 hours | Real-time STT, custom vocabulary, diarization |
| 54 | **Universal** | AssemblyAI ($50 credit) | ~185 hours | STT + sentiment + PII detection built-in |
| 55 | **Whisper Large-v3** | SambaNova ($5 credit) | Credit-based | OpenAI Whisper, fast inference |
| 56 | **Whisper Large-v3** | Ollama / local | Unlimited | Open-source, self-hosted |
| 57 | **SpeechRecognition** | Browser API | Unlimited | Zero cost, client-side |
| 58 | **Whisper API** | OpenAI ($5 credit) | Credit-based | Best accuracy, $0.006/min |

### Part 5: OCR / Document Models (5 Models)

| # | Model | Provider | Free Limit | Features |
|---|-------|----------|-----------|----------|
| 59 | **OCR API v2** | OCR.space | 500 req/day | PDF, image, handwriting, 25+ languages |
| 60 | **Cloud Vision API** | Google Cloud | 1K units/month | Labels, OCR, face detection |
| 61 | **Document AI** | Google Cloud ($300 credit) | Credit-based | Structured document extraction |
| 62 | **PdfPig** | .NET library (MIT) | Unlimited | PDF text extraction, no API needed |
| 63 | **Gemini PDF Native** | Google AI Studio | Part of free tier | Direct PDF understanding |

### Part 6: Reranking Models (3 Models)

| # | Model | Provider | Free Limit | Best For |
|---|-------|----------|-----------|----------|
| 64 | **rerank-2.5** | Voyage AI | 200M tokens | Best reranking quality |
| 65 | **rerank-2.5-lite** | Voyage AI | 200M tokens | Fast reranking |
| 66 | **Cohere Rerank** | Cohere | 1000 req/month | Alternative reranker |

### Part 7: NER / Classification Models (4 Models)

| # | Model | Provider | Free Limit | Best For |
|---|-------|----------|-----------|----------|
| 67 | **dslim/bert-base-NER** | HuggingFace | Rate-limited | Named entity recognition (configured) |
| 68 | **distilbert-base-uncased-finetuned-sst-2** | HuggingFace | Rate-limited | Sentiment classification |
| 69 | **facebook/bart-large-mnli** | HuggingFace | Rate-limited | Zero-shot classification |
| 70 | **dslim/bert-large-NER** | HuggingFace | Rate-limited | Higher accuracy NER |

**Total: 70 models across 7 categories**

---

## 8. Top 10 Recommendations for Insurance Domain

Ranked by value for insurance-specific use cases:

| Rank | Model/Provider | Why | Free Value |
|------|---------------|-----|-----------|
| 1 | **Voyage AI voyage-finance-2** | Purpose-built for financial/insurance text embeddings, 7% better than OpenAI | 50M tokens |
| 2 | **Cerebras Qwen 3 235B** | Fastest inference + largest free reasoning model | 1M tok/day |
| 3 | **Groq Llama 3.3 70B** | Real-time claims triage at 300 tok/s | 14,400 req/day |
| 4 | **Mistral Large 2** | Highest free volume for bulk claim processing | 1B tok/month |
| 5 | **Gemini 2.5 Flash** | Multimodal (photo + audio + PDF) + 1M context | 1000 req/day |
| 6 | **Deepgram Nova-2** | Best STT for transcribing policyholder calls | $200 / ~3,300 hours |
| 7 | **HuggingFace BERT NER** | Extract policy#, names, dates from claim text | Rate-limited |
| 8 | **OCR.space** | Extract text from scanned policy PDFs | 500 req/day |
| 9 | **Ollama (local stack)** | PII-safe local inference, unlimited, zero cost | Unlimited |
| 10 | **Cloudflare Workers AI** | Edge inference for real-time fraud scoring | 10K neurons/day |

---

## 9. Combined Free Capacity Estimate

### Monthly Capacity (All Providers Combined)

| Resource | Monthly Limit | Providers |
|----------|--------------|---------|
| Text analyses | **~1.3M+** | Groq (432K) + Mistral (1B tok) + Cerebras (30M tok) + Gemini (30K) |
| Image analyses | **~46,000** | Gemini (30K) + Cloudflare (300K neurons) + Azure Vision |
| Document OCR | **16,000** | OCR.space (15K) + Google Vision (1K) |
| Speech transcription | **~3,300 hours** | Deepgram ($200 credit) |
| Embeddings (finance) | **50M tokens** | Voyage AI finance-2 (one-time) |
| Embeddings (general) | **Unlimited** | Ollama nomic-embed-text (local) |
| Reranking | **200M tokens** | Voyage AI rerank-2.5 (one-time) |
| Vector storage | **Unlimited** | Local disk / Supabase (500MB free) |

**Total: Enterprise-grade AI capacity at $0/month operational cost**

---

## 10. Data Privacy Matrix

Critical for insurance compliance — which providers are safe for PII?

| Provider | Trains on Data? | PII Safe? | Notes |
|----------|----------------|-----------|-------|
| **Ollama** | N/A (local) | ✅ **Yes** | Runs on your machine, nothing leaves |
| **Voyage AI** | ❌ No | ✅ **Yes** | Explicitly does not train on input data |
| **Groq** | ❌ No (API mode) | ⚠️ Redact first | Fast, but external — use PII redaction |
| **Mistral** | ❌ No (API mode) | ⚠️ Redact first | EU-based, GDPR compliant |
| **Gemini** | ❌ No (API mode) | ⚠️ Redact first | Google AI Studio API does not train |
| **Cerebras** | ❌ No | ⚠️ Redact first | External API — redact PII |
| **OpenAI** | ❌ No (API mode) | ⚠️ Redact first | API mode explicitly opts out |
| **Cloudflare** | ❌ No | ⚠️ Redact first | Edge processing, no data retention |
| **HuggingFace** | Varies by model | ⚠️ NER exception | NER requires raw text (by design) |
| **Deepgram** | ❌ No | ⚠️ Redact audio | Audio data — ensure no PII in recordings |
| **OCR.space** | ❌ No | ⚠️ Redact output | Document text may contain PII |

**Rule:** Always run `PIIRedactionService` before external API calls (except NER which needs raw text for entity detection).

---

## 11. 2026 Trends Relevant to Insurance AI

| Trend | What It Is | Free Tool | Insurance Use Case |
|-------|-----------|-----------|-------------------|
| **Agentic RAG** | AI agents that retrieve + reason + act autonomously | Semantic Kernel (ours) | Auto-triage claims by querying policy docs |
| **Finance Embeddings** | Domain-specific vector models for financial text | Voyage AI finance-2 | Semantic search over policy/claims corpus |
| **Multimodal Claims** | Photo + text + audio in one analysis | Gemini Flash (free) | "Upload photo + describe damage" = complete FNOL |
| **2M Token Context** | Feed entire policy manuals in one prompt | xAI Grok 4.1 ($25 credit) | Entire policy book as context for Q&A |
| **Ultra-Fast Inference** | 2,600+ tok/s from specialized hardware | Cerebras (free 1M/day) | Real-time fraud scoring at conversation speed |
| **Edge AI Inference** | Run AI at CDN edge, <50ms latency | Cloudflare Workers AI | Real-time fraud scoring at the edge |
| **Voice-to-Claim** | Speak a claim, AI processes it | Deepgram ($200 free) + Groq | Phone call → automatic FNOL creation |
| **AI-Powered OCR** | Not just text extraction — understanding | Google Vision (free 1K/mo) | Read handwritten claim forms and understand them |
| **Open-Source VLMs** | Vision Language Models running locally | Ollama + LLaVA | PII-safe damage photo analysis |
| **Decentralized AI** | Distributed GPU inference | Chutes AI (Bittensor) | Censorship-resistant, low-cost inference |
| **AI Aggregators** | Single API to access 400+ models | OpenRouter / AIML API | Try different models without vendor lock-in |

---

## 12. Recommended Signup Priority

### Must Do (5 min each, highest value):

| # | Provider | Credits/Tier | Why | URL |
|---|----------|-------------|-----|-----|
| 1 | **Voyage AI** | 50M tokens (finance-2) | Purpose-built financial embeddings for RAG | [dash.voyageai.com](https://dash.voyageai.com) |
| 2 | **xAI Grok** | $25 | 2M context = entire policy manuals in one prompt | [console.x.ai](https://console.x.ai) |
| 3 | **Together AI** | $25 | 200+ models including Llama 4, Qwen3, for A/B testing | [api.together.xyz](https://api.together.xyz) |

### Nice to Have (grab credits while available):

| # | Provider | Credits/Tier | Why | URL |
|---|----------|-------------|-----|-----|
| 4 | **Google Cloud** | $300 | Vision AI + OCR for document processing | [cloud.google.com/free](https://cloud.google.com/free) |
| 5 | **RunPod** | $500 | GPU hosting for fine-tuning experiments | [runpod.io](https://runpod.io) |
| 6 | **AssemblyAI** | $50 | STT with built-in sentiment + PII detection | [assemblyai.com](https://assemblyai.com) |
| 7 | **GitHub Models** | 50 GPT-4o req/day | Free GPT-4o for testing | [github.com/marketplace/models](https://github.com/marketplace/models) |

### Already Active (✅ Configured):

| Provider | Limit | Status |
|----------|-------|--------|
| Groq | 14,400 req/day | ✅ API key active |
| Mistral | 1B tokens/month | ✅ API key active |
| Gemini | 1000 req/day (Flash) | ✅ API key active |
| Cerebras | 1M tokens/day | ✅ API key active |
| OpenRouter | Free models + $4.88 credit | ✅ API key active |
| OpenAI | $5 credit remaining | ✅ API key active (v1 + v2) |
| Ollama | Unlimited (local) | ✅ Installed |
| Deepgram | $200 credit (no expiry) | ✅ API key active |
| Azure Vision | Pay-as-you-go | ✅ API key active |
| Cloudflare Vision | 10K neurons/day | ✅ API key active |
| OCR.space | 500 req/day | ✅ API key active |
| HuggingFace NER | Rate-limited | ✅ API key active |

---

## Sources

- [Voyage AI — API Key & Installation](https://docs.voyageai.com/docs/api-key-and-installation)
- [Voyage AI — Pricing](https://docs.voyageai.com/docs/pricing)
- [Voyage AI — Finance Embeddings Blog](https://blog.voyageai.com/2024/06/03/domain-specific-embeddings-finance-edition-voyage-finance-2/)
- [Voyage AI — FAQ](https://docs.voyageai.com/docs/faq)
- [Cerebras — Pricing](https://www.cerebras.ai/pricing)
- [Cerebras — Supported Models](https://inference-docs.cerebras.ai/models/overview)
- [Cerebras — Free Tier Analysis](https://adam.holter.com/cerebras-opens-a-free-1m-tokens-per-day-inference-tier-and-claims-20x-faster-than-nvidia-real-benchmarks-model-limits-and-why-ui2-matters/)
- [Every Free AI API in 2026 — Complete Guide](https://awesomeagents.ai/tools/free-ai-inference-providers-2026/)
- [Free AI API Credits 2026 — Get AI Perks](https://www.getaiperks.com/en/blogs/27-ai-api-free-tier-credits-2026/)
- [Gemini API Pricing](https://ai.google.dev/gemini-api/docs/pricing)
- [Google Cloud Free AI Tools](https://cloud.google.com/use-cases/free-ai-tools)
- [Cloudflare Workers AI Pricing](https://developers.cloudflare.com/workers-ai/platform/pricing/)
- [Cloudflare Workers AI Models](https://developers.cloudflare.com/workers-ai/models/)
- [GitHub Models — Free GPT, Llama, Phi](https://blog.jiatool.com/en/posts/github_models/)
- [Chutes AI — Free API](https://free-llm.com/provider/chutes-ai)
- [SambaNova Cloud Developer Tier](https://sambanova.ai/blog/sambanova-cloud-developer-tier-is-live)
- [Fireworks AI Pricing](https://fireworks.ai/pricing)
- [AIML API — 400+ Models](https://aimlapi.com/best-ai-apis-for-free)
- [Free LLM API Resources (GitHub)](https://github.com/cheahjs/free-llm-api-resources)
- [Best Speech-to-Text APIs 2026 — Deepgram](https://deepgram.com/learn/best-speech-to-text-apis-2026)
- [HuggingFace Inference API](https://huggingface.co/inference-api)
- [OCR.space Free API](https://ocr.space/ocrapi)

---

*Last updated: 2026-02-23. Review monthly — free tiers change frequently.*
