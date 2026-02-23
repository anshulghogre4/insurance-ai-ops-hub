# Free AI Credits & Multimodal Providers — Reference Guide

> **Compiled by:** AI Expert Agent
> **Date:** 2026-02-21
> **Purpose:** Track all available free AI credits, multimodal providers, and new trends for the Insurance AI Operations Hub
> **Status:** Active — update as providers change tiers

---

## 1. Signup Credits (One-Time Free Money)

Sign up, get credits, use them before they expire.

| Provider | Free Credits | Expires | What You Get | Sign Up | Status |
|----------|-------------|---------|-------------|---------|--------|
| **xAI (Grok)** | **$25 free** | 30 days | Grok 4 + Grok 4.1 Fast, **2M context window** | [console.x.ai](https://console.x.ai) | ☐ Not signed up |
| **Together AI** | **$25 free** | 60 days | Llama 4, Qwen3, Mistral, 200+ models | [api.together.xyz](https://api.together.xyz) | ☐ Not signed up |
| **Deepgram** | **$200 free** | **No expiry** | Speech-to-text, best free STT deal | [deepgram.com](https://deepgram.com) | ☐ Not signed up |
| **Google Cloud** | **$300 free** | 90 days | Vision AI, OCR, Speech, Translation | [cloud.google.com/free](https://cloud.google.com/free) | ☐ Not signed up |
| **RunPod** | **$500 free** | 12 months | Serverless GPU, deploy any model | [runpod.io](https://runpod.io) | ☐ Not signed up |
| **AssemblyAI** | **$50 free** | One-time | STT + sentiment + PII detection built-in | [assemblyai.com](https://assemblyai.com) | ☐ Not signed up |
| **DeepSeek** | **5M tokens** | 30 days | DeepSeek R1 (near GPT-4 reasoning) | [platform.deepseek.com](https://platform.deepseek.com) | ☐ Not signed up |

**Total available: ~$1,100+ in free credits**

---

## 2. Permanent Free Tiers (Never Expire)

Long-term workhorses — no expiration, no credit card.

| Provider | Free Limit | Multimodal? | Best For | Sign Up | Status |
|----------|-----------|-------------|----------|---------|--------|
| **Groq** | 14,400 req/day | Text only | Fastest inference (300 tok/s), real-time | [console.groq.com](https://console.groq.com) | ✅ Active |
| **Mistral** | 1B tokens/month | Text only | Highest volume free tier | [console.mistral.ai](https://console.mistral.ai) | ✅ Active |
| **Gemini** (Google AI Studio) | 250 req/day (Pro), 1000/day (Flash-Lite) | **YES — vision + audio + video** | Long docs (1M context), image analysis | [aistudio.google.com](https://aistudio.google.com) | ✅ Active |
| **Cloudflare Workers AI** | 10,000 neurons/day | **YES — vision models** | Edge inference, Llama 4 Scout, Gemma 3 | [dash.cloudflare.com](https://dash.cloudflare.com) | ☐ Not signed up |
| **OpenRouter** | 50 req/day (free) | **YES — via free models** | Access 24+ free models, DeepSeek R1 | [openrouter.ai](https://openrouter.ai) | ☐ Not signed up |
| **HuggingFace** | Rate-limited | **YES — 300+ vision models** | Specialized models, NER, classification | [huggingface.co](https://huggingface.co) | ☐ Not signed up |
| **Ollama** | Unlimited (local) | **YES — LLaVA, Llama 3.2 Vision** | PII-safe, offline, zero cost | Local install | ✅ Installed |
| **OCR.space** | 500 req/day | OCR only | Document text extraction | [ocr.space](https://ocr.space) | ☐ Not signed up |
| **Google Cloud Vision** | 1,000 units/month | Image analysis | OCR, labels, face detection | [cloud.google.com/vision](https://cloud.google.com/vision) | ☐ Not signed up |

---

## 3. Multimodal Capabilities Map

What each modality unlocks for insurance:

| Modality | Best Free Provider | Free Limit | Insurance Use Case |
|----------|-------------------|-----------|-------------------|
| **Text → Analysis** | Groq / Mistral | 14.4K/day, 1B tok/mo | Claims triage, sentiment, fraud scoring |
| **Image → Text** (damage photos) | Gemini 2.0 Flash | 1000 req/day | Photograph claim damage, get AI description |
| **Image → Text** (local/PII-safe) | Ollama + LLaVA | Unlimited | Analyze damage photos without sending to cloud |
| **Document → Text** (OCR) | OCR.space / Google Vision | 500/day, 1K/month | Extract text from scanned policy PDFs |
| **Speech → Text** (call recordings) | Deepgram | $200 free, no expiry | Transcribe policyholder calls for sentiment analysis |
| **Text → Speech** | Cloudflare Workers AI | 10K neurons/day | Read-aloud claim summaries for accessibility |
| **Video → Analysis** | Gemini 2.0 Flash | Part of 1000 req/day | Analyze video damage evidence |

---

## 4. Provider Chain Strategy

### Current (Text-Only)
```
Groq (Llama 3.3 70B) → Mistral (Large 2) → Gemini (2.0 Flash) → Ollama (local)
```

### Expanded (Multimodal — Future)
```
TEXT Analysis:
  Groq → Mistral → Gemini → Ollama

IMAGE Analysis (damage photos, documents):
  Gemini Flash (free, vision) → Cloudflare Workers AI → Ollama LLaVA (local)

DOCUMENT OCR:
  OCR.space (500/day) → Google Vision (1K/month) → Gemini (PDF native)

SPEECH-TO-TEXT:
  Deepgram ($200 free) → Browser SpeechRecognition API (zero cost)

COMPLEX REASONING:
  xAI Grok ($25 credit) → DeepSeek R1 (via OpenRouter) → Gemini Pro
```

---

## 5. Free Model Inventory

### Text Models (via API)

| Model | Provider | Parameters | Context | Speed | Best For |
|-------|----------|-----------|---------|-------|----------|
| Llama 3.3 70B | Groq | 70B | 128K | 300 tok/s | Real-time analysis |
| Mistral Large 2 | Mistral | ~123B | 128K | Medium | Bulk processing |
| Gemini 2.5 Flash | Google | Unknown | 1M | Fast | Long document analysis |
| Gemini 2.5 Pro | Google | Unknown | 1M | Slower | Complex reasoning (250/day) |
| DeepSeek R1 | OpenRouter | 671B MoE | 128K | Medium | Chain-of-thought reasoning |
| Grok 4.1 Fast | xAI ($25 credit) | Unknown | 2M | Fast | Largest context window |
| Llama 3.2 3B | Ollama (local) | 3B | 128K | ~10 tok/s | PII-safe local processing |

### Vision/Multimodal Models

| Model | Provider | Free Access | Capabilities |
|-------|----------|------------|-------------|
| Gemini 2.0 Flash | Google AI Studio | 1000 req/day | Image + audio + video understanding |
| Llama 4 Scout | Cloudflare Workers AI | 10K neurons/day | Natively multimodal, 16 MoE experts |
| Gemma 3 | Cloudflare Workers AI | 10K neurons/day | Vision + text, 128K context, 140+ languages |
| Mistral Small 3.1 | Cloudflare Workers AI | 10K neurons/day | Vision + text, 128K context |
| LLaVA | Ollama (local) | Unlimited | Image → text, runs on CPU |
| Llama 3.2 Vision 11B | HuggingFace / Ollama | Rate-limited / local | Image understanding |

### Embedding Models (for RAG)

| Model | Provider | Free Limit | Dimensions | Best For |
|-------|----------|-----------|-----------|----------|
| nomic-embed-text | Ollama (local) | Unlimited | 768 | General document RAG |
| mxbai-embed-large | Ollama (local) | Unlimited | 1024 | High-accuracy retrieval |
| all-minilm | Ollama (local) | Unlimited | 384 | Lightweight, fast |
| text-embedding-004 | Gemini (Google) | Free tier | 768 | Cloud embedding |

### Speech Models

| Model | Provider | Free Limit | Features |
|-------|----------|-----------|----------|
| Nova-2 | Deepgram ($200 credit) | ~3,300 hours | Real-time STT, custom vocabulary |
| Universal | AssemblyAI ($50 credit) | ~185 hours | STT + sentiment + PII detection |
| Whisper | Ollama / local | Unlimited | Open-source, self-hosted |
| SpeechRecognition | Browser API | Unlimited | Zero cost, client-side |

### OCR Models

| Tool | Provider | Free Limit | Features |
|------|----------|-----------|----------|
| OCR API v2 | OCR.space | 500 req/day | PDF, image, handwriting, 25+ languages |
| Cloud Vision | Google Cloud | 1K units/month | Labels, OCR, face detection |
| Document AI | Google Cloud ($300 credit) | Credit-based | Structured document extraction |
| PdfPig | .NET library (MIT) | Unlimited | PDF text extraction, no API needed |

---

## 6. 2026 Trends Relevant to Insurance AI

| Trend | What It Is | Free Tool | Insurance Use Case |
|-------|-----------|-----------|-------------------|
| **Agentic RAG** | AI agents that retrieve + reason + act autonomously | Semantic Kernel (ours) | Auto-triage claims by querying policy docs |
| **Multimodal Claims** | Photo + text + audio in one analysis | Gemini Flash (free) | "Upload photo + describe damage" = complete FNOL |
| **2M Token Context** | Feed entire policy manuals in one prompt | xAI Grok 4.1 ($25 credit) | Entire policy book as context for Q&A |
| **Edge AI Inference** | Run AI at CDN edge, <50ms latency | Cloudflare Workers AI | Real-time fraud scoring at the edge |
| **Voice-to-Claim** | Speak a claim, AI processes it | Deepgram ($200 free) + Groq | Phone call → automatic FNOL creation |
| **AI-Powered OCR** | Not just text extraction — understanding | Google Vision (free 1K/mo) | Read handwritten claim forms and understand them |
| **Open-Source VLMs** | Vision Language Models running locally | Ollama + LLaVA | PII-safe damage photo analysis |
| **Adaptive RAG** | Auto-optimizing chunk size + embeddings | LlamaIndex / Haystack | Self-tuning policy document retrieval |
| **AI Aggregators** | Single API to access 400+ models | OpenRouter / AIML API | Try different models without vendor lock-in |

---

## 7. Recommended Signup Priority

### Must Do (5 min each, highest value):

| # | Provider | Credits | Why |
|---|----------|---------|-----|
| 1 | **xAI Grok** | $25 | 2M context = entire policy manuals in one prompt |
| 2 | **Deepgram** | $200 (no expiry) | Voice-to-text for call center analysis |
| 3 | **Cloudflare Workers AI** | Forever free | Multimodal edge AI, vision models |

### Nice to Have (grab credits while available):

| # | Provider | Credits | Why |
|---|----------|---------|-----|
| 4 | **Together AI** | $25 | 200+ models for A/B testing |
| 5 | **Google Cloud** | $300 | Vision AI + OCR for document processing |
| 6 | **RunPod** | $500 | GPU hosting for fine-tuning experiments |
| 7 | **OpenRouter** | Free models | Access DeepSeek R1, Llama 4 for free |

### Already Active:

- ✅ Groq (14,400 req/day)
- ✅ Mistral (1B tokens/month)
- ✅ Gemini (multimodal, 1M context)
- ✅ Ollama + nomic-embed-text (local, unlimited)
- ✅ OpenAI (legacy credits)

---

## 8. Monthly Free Capacity Summary

| Resource | Monthly Limit | Provider |
|----------|--------------|---------|
| Text analyses | 15,000+ (Groq) + 1.3M (Mistral) | Groq + Mistral |
| Image analyses | 30,000 (Gemini) + 300K neurons (Cloudflare) | Gemini + Cloudflare |
| Document OCR | 15,000 (OCR.space) + 1,000 (Google Vision) | OCR.space + Google |
| Speech transcription | ~3,300 hours (Deepgram credit) | Deepgram |
| Embeddings | Unlimited | Ollama (local) |
| Vector storage | Unlimited (local disk) | ChromaDB (local) |

**Total: Enterprise-grade AI capacity at $0/month**

---

## Sources

- [Every Free AI API in 2026 - Complete Guide](https://awesomeagents.ai/tools/free-ai-inference-providers-2026/)
- [Gemini API Pricing](https://ai.google.dev/gemini-api/docs/pricing)
- [Google Cloud Free AI Tools](https://cloud.google.com/use-cases/free-ai-tools)
- [Cloudflare Workers AI Pricing](https://developers.cloudflare.com/workers-ai/platform/pricing/)
- [Cloudflare Workers AI Models](https://developers.cloudflare.com/workers-ai/models/)
- [Top Free AI APIs 2026](https://aicurator.io/free-ai-apis/)
- [20 Best Free AI APIs 2026](https://visionvix.com/free-ai-apis/)
- [Best Speech-to-Text APIs 2026 — Deepgram](https://deepgram.com/learn/best-speech-to-text-apis-2026)
- [Best Speech-to-Text APIs 2026 — AssemblyAI](https://www.assemblyai.com/blog/best-api-models-for-real-time-speech-recognition-and-transcription)
- [HuggingFace Inference API](https://huggingface.co/inference-api)
- [HuggingFace Vision Language Models 2025](https://huggingface.co/blog/vlms-2025)
- [OCR.space Free API](https://ocr.space/ocrapi)
- [Google Cloud Vision AI](https://cloud.google.com/vision)
- [AIML API — 400+ Models](https://aimlapi.com/best-ai-apis-for-free)
- [Multimodal AI: Open-Source VLMs 2026 — BentoML](https://www.bentoml.com/blog/multimodal-ai-a-guide-to-open-source-vision-language-models)
- [AI Aggregators: Multi-Model Platforms 2026](https://graygrids.com/blog/ai-aggregators-multiple-models-platform)

---

*Last updated: 2026-02-21. Review monthly — free tiers change frequently.*
