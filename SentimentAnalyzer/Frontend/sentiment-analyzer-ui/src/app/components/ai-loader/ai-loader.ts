import { Component, computed, input, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';

/** Profile types that map to different AI agent pipelines */
export type AiLoaderProfile =
  | 'sentiment'
  | 'insurance'
  | 'triage'
  | 'fraud'
  | 'cx'
  | 'document'
  | 'correlation';

/** A single stage in the AI pipeline */
interface PipelineStage {
  label: string;
}

/** Pipeline stage configurations per profile */
const PIPELINE_STAGES: Record<AiLoaderProfile, PipelineStage[]> = {
  sentiment: [
    { label: 'Sentiment Analyzer' }
  ],
  insurance: [
    { label: 'Business Analyst' },
    { label: 'Developer' },
    { label: 'QA Agent' },
    { label: 'Architect' }
  ],
  triage: [
    { label: 'Claims Triage' },
    { label: 'Fraud Detection' },
    { label: 'QA Validation' }
  ],
  fraud: [
    { label: 'Fraud Detection' },
    { label: 'Pattern Analysis' },
    { label: 'SIU Assessment' }
  ],
  cx: [
    { label: 'CX Agent' },
    { label: 'Tone Analysis' },
    { label: 'Escalation Check' }
  ],
  document: [
    { label: 'OCR Engine' },
    { label: 'Text Chunker' },
    { label: 'Embedding Gen' }
  ],
  correlation: [
    { label: 'Claim Scanner' },
    { label: 'Strategy Match' },
    { label: 'Score Calc' }
  ]
};

/** Phase text configurations per profile */
const PHASE_TEXT: Record<AiLoaderProfile, { threshold: number; text: string }[]> = {
  sentiment: [
    { threshold: 0, text: 'Initializing sentiment engine...' },
    { threshold: 3, text: 'Analyzing emotional tone...' },
    { threshold: 10, text: 'Computing confidence scores...' },
    { threshold: 20, text: 'Generating sentiment report...' }
  ],
  insurance: [
    { threshold: 0, text: 'Submitting for analysis...' },
    { threshold: 3, text: 'Business Analyst reviewing domain context...' },
    { threshold: 10, text: 'Developer formatting structured output...' },
    { threshold: 20, text: 'QA Agent validating results...' },
    { threshold: 30, text: 'Architect reviewing system integrity...' },
    { threshold: 40, text: 'Finalizing comprehensive assessment...' }
  ],
  triage: [
    { threshold: 0, text: 'Submitting claim...' },
    { threshold: 3, text: 'Claims Triage Agent analyzing severity...' },
    { threshold: 10, text: 'Fraud Detection Agent scoring risk...' },
    { threshold: 20, text: 'Business Analyst validating domain rules...' },
    { threshold: 30, text: 'QA Agent checking quality...' },
    { threshold: 40, text: 'Finalizing triage assessment...' }
  ],
  fraud: [
    { threshold: 0, text: 'Initializing fraud analysis...' },
    { threshold: 3, text: 'Fraud Detection Agent scanning patterns...' },
    { threshold: 12, text: 'Pattern Analysis engine correlating data...' },
    { threshold: 25, text: 'SIU Assessment evaluating risk factors...' },
    { threshold: 35, text: 'Compiling fraud intelligence report...' }
  ],
  cx: [
    { threshold: 0, text: 'Connecting to CX Agent...' },
    { threshold: 3, text: 'CX Agent analyzing customer context...' },
    { threshold: 10, text: 'Tone Analysis evaluating sentiment...' },
    { threshold: 18, text: 'Escalation Check assessing urgency...' },
    { threshold: 25, text: 'Generating response recommendations...' }
  ],
  document: [
    { threshold: 0, text: 'Preparing document pipeline...' },
    { threshold: 3, text: 'OCR Engine extracting text...' },
    { threshold: 12, text: 'Text Chunker splitting content...' },
    { threshold: 22, text: 'Embedding Generator creating vectors...' },
    { threshold: 30, text: 'Indexing document for retrieval...' }
  ],
  correlation: [
    { threshold: 0, text: 'Initiating cross-claim scan...' },
    { threshold: 5, text: 'Cross-Claim Scanner querying database...' },
    { threshold: 15, text: 'Strategy Matcher applying correlation rules...' },
    { threshold: 25, text: 'Score Calculator computing risk scores...' },
    { threshold: 35, text: 'Finalizing correlation report...' }
  ]
};

/** Estimated total seconds per profile */
const ESTIMATED_DURATION: Record<AiLoaderProfile, number> = {
  sentiment: 15,
  insurance: 45,
  triage: 45,
  fraud: 40,
  cx: 30,
  document: 35,
  correlation: 40
};

/** Rotating insight tips per profile — shown during loading to educate users */
const INSIGHT_TIPS: Record<AiLoaderProfile, string[]> = {
  sentiment: [
    'AI agents analyze emotional tone across 7 dimensions',
    'Confidence scores help identify ambiguous communications',
    'Sentiment patterns reveal customer satisfaction trends'
  ],
  insurance: [
    'Multi-agent analysis covers risk, compliance, and customer intent',
    '4 specialized AI agents collaborate for comprehensive assessment',
    'Purchase intent scoring identifies cross-sell opportunities'
  ],
  triage: [
    'Claims are assessed across severity, urgency, and fraud risk simultaneously',
    'Fraud flags are cross-referenced against industry patterns',
    'High-severity claims are automatically prioritized for review',
    'Evidence from images and documents strengthens fraud detection',
    'Each claim is scored across 5 risk categories'
  ],
  fraud: [
    'Deep analysis examines timing, behavioral, financial, and documentation patterns',
    'SIU referral is recommended for claims scoring above 75',
    'Cross-claim correlation detects organized fraud rings',
    'Pattern analysis compares against historical fraud profiles',
    'AI confidence score indicates assessment reliability'
  ],
  cx: [
    'Tone classification detects Professional, Empathetic, Urgent, or Informational',
    'Escalation triggers include legal threats and regulator mentions',
    'Responses include regulatory disclaimers automatically',
    'Dual-pass PII redaction protects customer data'
  ],
  document: [
    'OCR supports PDF, DOCX, and scanned image formats',
    'Text is chunked into semantically meaningful insurance sections',
    'Vector embeddings enable natural language policy queries',
    'RAG retrieval finds the most relevant policy clauses'
  ],
  correlation: [
    '4 strategies: DateProximity, SimilarNarrative, SharedFlags, SameSeverity',
    'Claim-type-specific time windows: Auto 90d, Property 180d, WorkersComp 365d',
    'Cross-claim patterns help detect organized fraud rings',
    'Correlation scores reflect match strength across multiple factors'
  ]
};

/** Profile-specific illustration configurations */
const PROFILE_ILLUSTRATIONS: Record<AiLoaderProfile, { title: string; color: string; accentColor: string }> = {
  sentiment: { title: 'Sentiment', color: '#6366f1', accentColor: '#a78bfa' },
  insurance: { title: 'Insurance', color: '#6366f1', accentColor: '#818cf8' },
  triage: { title: 'Triage', color: '#6366f1', accentColor: '#c084fc' },
  fraud: { title: 'Fraud', color: '#ef4444', accentColor: '#f97316' },
  cx: { title: 'CX', color: '#10b981', accentColor: '#34d399' },
  document: { title: 'Document', color: '#3b82f6', accentColor: '#60a5fa' },
  correlation: { title: 'Correlation', color: '#f59e0b', accentColor: '#fbbf24' }
};

@Component({
  selector: 'app-ai-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (isActive()) {
      <div class="glass-card-static p-6 sm:p-8 mb-6 animate-fade-in ai-loader" role="status" aria-live="polite"
           [attr.aria-label]="'AI analysis in progress: ' + phaseText()">

        <!-- Row 1: Illustration + Status Panel side-by-side -->
        <div class="flex flex-col sm:flex-row items-center gap-6 mb-6">

          <!-- Profile-specific animated illustration -->
          <div class="flex-shrink-0" aria-hidden="true">
            @switch (profile()) {
              @case ('triage') {
                <!-- Shield + scanning lines -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <defs>
                    <linearGradient id="triageGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stop-color="#6366f1" stop-opacity="0.8"/>
                      <stop offset="100%" stop-color="#c084fc" stop-opacity="0.8"/>
                    </linearGradient>
                  </defs>
                  <!-- Shield shape -->
                  <path d="M60 10 L95 30 L95 65 Q95 95 60 110 Q25 95 25 65 L25 30 Z"
                        fill="none" stroke="url(#triageGrad)" stroke-width="2" class="ai-shield-path"/>
                  <!-- Scan lines sweeping down -->
                  <line x1="35" y1="40" x2="85" y2="40" stroke="#6366f1" stroke-width="1.5" class="ai-scan-line scan-1" stroke-linecap="round"/>
                  <line x1="35" y1="55" x2="85" y2="55" stroke="#a78bfa" stroke-width="1.5" class="ai-scan-line scan-2" stroke-linecap="round"/>
                  <line x1="35" y1="70" x2="85" y2="70" stroke="#c084fc" stroke-width="1.5" class="ai-scan-line scan-3" stroke-linecap="round"/>
                  <!-- Center checkmark that appears -->
                  <path d="M45 60 L55 70 L75 48" fill="none" stroke="#34d399" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" class="ai-checkmark-draw"/>
                  <!-- Pulsing dot -->
                  <circle cx="60" cy="30" r="4" fill="#6366f1" class="ai-dot-pulse"/>
                </svg>
              }
              @case ('fraud') {
                <!-- Magnifying glass scanning document -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <defs>
                    <linearGradient id="fraudGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stop-color="#ef4444" stop-opacity="0.7"/>
                      <stop offset="100%" stop-color="#f97316" stop-opacity="0.7"/>
                    </linearGradient>
                  </defs>
                  <!-- Document -->
                  <rect x="20" y="15" width="55" height="70" rx="4" fill="none" stroke="rgba(255,255,255,0.15)" stroke-width="1.5"/>
                  <line x1="30" y1="30" x2="65" y2="30" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="30" y1="40" x2="60" y2="40" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="30" y1="50" x2="55" y2="50" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="30" y1="60" x2="65" y2="60" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="30" y1="70" x2="50" y2="70" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <!-- Magnifying glass orbit -->
                  <circle cx="78" cy="72" r="20" fill="none" stroke="url(#fraudGrad)" stroke-width="2" class="ai-mag-lens"/>
                  <line x1="92" y1="86" x2="108" y2="102" stroke="#ef4444" stroke-width="3" stroke-linecap="round" class="ai-mag-handle"/>
                  <!-- Alert indicators -->
                  <circle cx="45" cy="40" r="3" fill="#ef4444" class="ai-alert-dot alert-1"/>
                  <circle cx="55" cy="55" r="3" fill="#f97316" class="ai-alert-dot alert-2"/>
                  <circle cx="38" cy="65" r="3" fill="#fbbf24" class="ai-alert-dot alert-3"/>
                </svg>
              }
              @case ('document') {
                <!-- Document with scanning beam -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <defs>
                    <linearGradient id="docGrad" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#3b82f6" stop-opacity="0.8"/>
                      <stop offset="100%" stop-color="#60a5fa" stop-opacity="0.4"/>
                    </linearGradient>
                  </defs>
                  <!-- Document pages stacked -->
                  <rect x="28" y="18" width="55" height="70" rx="3" fill="rgba(255,255,255,0.03)" stroke="rgba(255,255,255,0.08)" stroke-width="1"/>
                  <rect x="24" y="14" width="55" height="70" rx="3" fill="rgba(255,255,255,0.05)" stroke="rgba(255,255,255,0.12)" stroke-width="1"/>
                  <rect x="20" y="10" width="55" height="70" rx="3" fill="rgba(255,255,255,0.07)" stroke="rgba(255,255,255,0.15)" stroke-width="1.5"/>
                  <!-- Text lines -->
                  <line x1="28" y1="24" x2="65" y2="24" stroke="rgba(255,255,255,0.12)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="28" y1="34" x2="58" y2="34" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="28" y1="44" x2="62" y2="44" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="28" y1="54" x2="50" y2="54" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <line x1="28" y1="64" x2="68" y2="64" stroke="rgba(255,255,255,0.1)" stroke-width="2" stroke-linecap="round"/>
                  <!-- Scan beam sweeping down -->
                  <rect x="20" y="10" width="55" height="4" rx="1" fill="url(#docGrad)" class="ai-doc-scan-beam"/>
                  <!-- Embedding vectors flying out -->
                  <circle cx="85" cy="35" r="3" fill="#3b82f6" class="ai-embed-dot embed-1"/>
                  <circle cx="92" cy="48" r="2.5" fill="#60a5fa" class="ai-embed-dot embed-2"/>
                  <circle cx="88" cy="62" r="2" fill="#93c5fd" class="ai-embed-dot embed-3"/>
                  <circle cx="95" cy="75" r="3" fill="#3b82f6" class="ai-embed-dot embed-4"/>
                </svg>
              }
              @case ('cx') {
                <!-- Chat bubbles with tone waves -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <defs>
                    <linearGradient id="cxGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stop-color="#10b981" stop-opacity="0.8"/>
                      <stop offset="100%" stop-color="#34d399" stop-opacity="0.6"/>
                    </linearGradient>
                  </defs>
                  <!-- Customer bubble -->
                  <rect x="10" y="20" width="50" height="28" rx="14" fill="rgba(255,255,255,0.08)" stroke="rgba(255,255,255,0.15)" stroke-width="1"/>
                  <line x1="20" y1="31" x2="50" y2="31" stroke="rgba(255,255,255,0.15)" stroke-width="1.5" stroke-linecap="round"/>
                  <line x1="20" y1="38" x2="40" y2="38" stroke="rgba(255,255,255,0.1)" stroke-width="1.5" stroke-linecap="round"/>
                  <!-- AI response bubble -->
                  <rect x="35" y="58" width="60" height="32" rx="14" fill="none" stroke="url(#cxGrad)" stroke-width="1.5" class="ai-chat-bubble"/>
                  <line x1="45" y1="70" x2="82" y2="70" stroke="#10b981" stroke-width="1.5" stroke-linecap="round" class="ai-typing-line type-1"/>
                  <line x1="45" y1="78" x2="72" y2="78" stroke="#34d399" stroke-width="1.5" stroke-linecap="round" class="ai-typing-line type-2"/>
                  <!-- Tone wave radiating -->
                  <path d="M100 50 Q106 45 100 40" fill="none" stroke="#10b981" stroke-width="1.5" class="ai-wave wave-1" stroke-linecap="round"/>
                  <path d="M104 54 Q112 45 104 36" fill="none" stroke="#34d399" stroke-width="1" class="ai-wave wave-2" stroke-linecap="round"/>
                  <path d="M108 58 Q118 45 108 32" fill="none" stroke="#6ee7b7" stroke-width="0.8" class="ai-wave wave-3" stroke-linecap="round"/>
                </svg>
              }
              @case ('correlation') {
                <!-- Connected claim nodes -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <defs>
                    <linearGradient id="corrGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                      <stop offset="0%" stop-color="#f59e0b" stop-opacity="0.8"/>
                      <stop offset="100%" stop-color="#fbbf24" stop-opacity="0.6"/>
                    </linearGradient>
                  </defs>
                  <!-- Connection web lines -->
                  <line x1="30" y1="30" x2="90" y2="30" stroke="rgba(245,158,11,0.2)" stroke-width="1" stroke-dasharray="3 2" class="ai-corr-line corr-1"/>
                  <line x1="30" y1="30" x2="60" y2="90" stroke="rgba(245,158,11,0.2)" stroke-width="1" stroke-dasharray="3 2" class="ai-corr-line corr-2"/>
                  <line x1="90" y1="30" x2="60" y2="90" stroke="rgba(245,158,11,0.2)" stroke-width="1" stroke-dasharray="3 2" class="ai-corr-line corr-3"/>
                  <line x1="30" y1="30" x2="90" y2="90" stroke="rgba(245,158,11,0.15)" stroke-width="1" stroke-dasharray="3 2" class="ai-corr-line corr-4"/>
                  <line x1="90" y1="30" x2="30" y2="90" stroke="rgba(245,158,11,0.15)" stroke-width="1" stroke-dasharray="3 2" class="ai-corr-line corr-5"/>
                  <!-- Claim nodes -->
                  <circle cx="30" cy="30" r="12" fill="none" stroke="url(#corrGrad)" stroke-width="1.5" class="ai-claim-node node-1"/>
                  <text x="30" y="34" text-anchor="middle" fill="#f59e0b" font-size="9" font-weight="600" class="ai-claim-label">C1</text>
                  <circle cx="90" cy="30" r="12" fill="none" stroke="url(#corrGrad)" stroke-width="1.5" class="ai-claim-node node-2"/>
                  <text x="90" y="34" text-anchor="middle" fill="#fbbf24" font-size="9" font-weight="600" class="ai-claim-label">C2</text>
                  <circle cx="60" cy="90" r="12" fill="none" stroke="url(#corrGrad)" stroke-width="1.5" class="ai-claim-node node-3"/>
                  <text x="60" y="94" text-anchor="middle" fill="#f59e0b" font-size="9" font-weight="600" class="ai-claim-label">C3</text>
                  <circle cx="30" cy="90" r="10" fill="none" stroke="rgba(245,158,11,0.3)" stroke-width="1" class="ai-claim-node node-4"/>
                  <text x="30" y="94" text-anchor="middle" fill="rgba(245,158,11,0.5)" font-size="8">C4</text>
                  <circle cx="90" cy="90" r="10" fill="none" stroke="rgba(245,158,11,0.3)" stroke-width="1" class="ai-claim-node node-5"/>
                  <text x="90" y="94" text-anchor="middle" fill="rgba(245,158,11,0.5)" font-size="8">C5</text>
                  <!-- Match score pulsing center -->
                  <circle cx="60" cy="50" r="6" fill="#f59e0b" class="ai-dot-pulse"/>
                </svg>
              }
              @default {
                <!-- Default: neural network brain -->
                <svg viewBox="0 0 120 120" class="w-28 h-28 ai-illustration">
                  <line class="neural-connection neural-conn-1" x1="60" y1="20" x2="95" y2="42" />
                  <line class="neural-connection neural-conn-2" x1="95" y1="42" x2="95" y2="78" />
                  <line class="neural-connection neural-conn-3" x1="95" y1="78" x2="60" y2="100" />
                  <line class="neural-connection neural-conn-4" x1="60" y1="100" x2="25" y2="78" />
                  <line class="neural-connection neural-conn-5" x1="25" y1="78" x2="25" y2="42" />
                  <line class="neural-connection neural-conn-6" x1="25" y1="42" x2="60" y2="20" />
                  <line class="neural-connection neural-conn-7" x1="60" y1="20" x2="60" y2="100" />
                  <line class="neural-connection neural-conn-8" x1="25" y1="42" x2="95" y2="78" />
                  <line class="neural-connection neural-conn-9" x1="95" y1="42" x2="25" y2="78" />
                  <circle class="neural-node neural-node-1" cx="60" cy="20" r="6" />
                  <circle class="neural-node neural-node-2" cx="95" cy="42" r="6" />
                  <circle class="neural-node neural-node-3" cx="95" cy="78" r="6" />
                  <circle class="neural-node neural-node-4" cx="60" cy="100" r="6" />
                  <circle class="neural-node neural-node-5" cx="25" cy="78" r="6" />
                  <circle class="neural-node neural-node-6" cx="25" cy="42" r="6" />
                  <circle class="neural-hub" cx="60" cy="60" r="10" />
                  <circle class="neural-hub-pulse" cx="60" cy="60" r="10" />
                  <line class="neural-connection neural-conn-hub-1" x1="60" y1="60" x2="60" y2="20" />
                  <line class="neural-connection neural-conn-hub-2" x1="60" y1="60" x2="95" y2="42" />
                  <line class="neural-connection neural-conn-hub-3" x1="60" y1="60" x2="95" y2="78" />
                  <line class="neural-connection neural-conn-hub-4" x1="60" y1="60" x2="60" y2="100" />
                  <line class="neural-connection neural-conn-hub-5" x1="60" y1="60" x2="25" y2="78" />
                  <line class="neural-connection neural-conn-hub-6" x1="60" y1="60" x2="25" y2="42" />
                </svg>
              }
            }
          </div>

          <!-- Status panel -->
          <div class="flex-1 min-w-0">
            <!-- Phase text -->
            <p class="text-sm sm:text-base font-semibold mb-2 text-white/90">
              {{ phaseText() }}
            </p>

            <!-- Progress bar -->
            <div class="progress-track mb-3" role="progressbar"
                 [attr.aria-valuenow]="progressPercent()"
                 aria-valuemin="0" aria-valuemax="100"
                 [attr.aria-label]="'Analysis progress: ' + progressPercent() + '%'">
              <div class="progress-fill bg-gradient-to-r from-indigo-500 via-purple-500 to-pink-500 transition-all duration-1000 ease-out"
                   [style.width.%]="progressPercent()">
              </div>
            </div>

            <!-- Elapsed + provider row -->
            <div class="flex items-center justify-between gap-3 flex-wrap">
              <span class="text-xs tabular-nums text-white/40 inline-flex items-center gap-1.5">
                <svg class="w-3 h-3 animate-spin" fill="none" viewBox="0 0 24 24" aria-hidden="true">
                  <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"/>
                  <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
                </svg>
                {{ elapsedSeconds() }}s elapsed
              </span>
              @if (provider()) {
                <span class="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[10px] font-medium tracking-wide uppercase bg-white/5 border border-white/10 text-white/50">
                  <span class="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-pulse"></span>
                  {{ provider() }}
                </span>
              }
            </div>

            <!-- API status message (driven by parent from API response) -->
            @if (statusMessage()) {
              <div class="mt-2 px-3 py-1.5 rounded-lg bg-indigo-500/10 border border-indigo-500/20 animate-fade-in">
                <p class="text-xs text-indigo-300">
                  <svg class="w-3 h-3 inline mr-1" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"/>
                  </svg>
                  {{ statusMessage() }}
                </p>
              </div>
            }
          </div>
        </div>

        <!-- Pipeline Stages -->
        <div class="flex items-center justify-center gap-0 mb-5 flex-wrap">
          @for (stage of stages(); track stage.label; let i = $index; let last = $last) {
            <div class="flex items-center gap-0">
              <div class="flex flex-col items-center gap-1.5 px-2 sm:px-3 min-w-[72px] sm:min-w-[90px]">
                <div class="w-8 h-8 sm:w-9 sm:h-9 rounded-full flex items-center justify-center text-xs font-bold transition-all duration-500"
                     [class]="getStageNodeClass(i)">
                  @if (isStageCompleted(i)) {
                    <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2.5" d="M5 13l4 4L19 7"/>
                    </svg>
                  } @else {
                    <span>{{ i + 1 }}</span>
                  }
                </div>
                <span class="text-[10px] sm:text-xs font-medium text-center leading-tight transition-colors duration-300"
                      [class]="getStageTextClass(i)">
                  {{ stage.label }}
                </span>
              </div>
              @if (!last) {
                <div class="w-6 sm:w-8 h-0.5 rounded-full transition-all duration-500 -mt-5"
                     [class]="getConnectorClass(i)">
                </div>
              }
            </div>
          }
        </div>

        <!-- Rotating insight tip -->
        <div class="text-center mt-4 px-4">
          <p class="text-[11px] text-white/30 italic transition-opacity duration-500">
            <svg class="w-3 h-3 inline mr-1 -mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9.663 17h4.673M12 3v1m6.364 1.636l-.707.707M21 12h-1M4 12H3m3.343-5.657l-.707-.707m2.828 9.9a5 5 0 117.072 0l-.548.547A3.374 3.374 0 0014 18.469V19a2 2 0 11-4 0v-.531c0-.895-.356-1.754-.988-2.386l-.548-.547z"/>
            </svg>
            {{ currentTip() }}
          </p>
        </div>

        <!-- Skeleton preview cards -->
        @if (showSkeletons()) {
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 mt-5">
            @for (i of skeletonSlots; track i) {
              <div class="skeleton h-16 rounded-xl"></div>
            }
          </div>
        }
      </div>
    }
  `
})
export class AiLoaderComponent implements OnDestroy {
  /** Which AI pipeline profile to display */
  profile = input<AiLoaderProfile>('triage');

  /** Whether the loader is active/visible */
  isActive = input<boolean>(false);

  /** Elapsed seconds since analysis started */
  elapsedSeconds = input<number>(0);

  /** Optional LLM provider name to display */
  provider = input<string>('');

  /** Whether to show skeleton placeholder cards */
  showSkeletons = input<boolean>(true);

  /** API-driven status message (parent sets from API callback, e.g. "Provider switched to Gemini") */
  statusMessage = input<string>('');

  readonly skeletonSlots = [1, 2, 3, 4];

  stages = computed(() => PIPELINE_STAGES[this.profile()] ?? PIPELINE_STAGES['triage']);

  activeStageIndex = computed(() => {
    const elapsed = this.elapsedSeconds();
    const stageCount = this.stages().length;
    const estimated = ESTIMATED_DURATION[this.profile()] ?? 45;
    const perStage = estimated / stageCount;
    return Math.min(Math.floor(elapsed / perStage), stageCount - 1);
  });

  phaseText = computed(() => {
    const elapsed = this.elapsedSeconds();
    const phases = PHASE_TEXT[this.profile()] ?? PHASE_TEXT['triage'];
    let text = phases[0].text;
    for (const phase of phases) {
      if (elapsed >= phase.threshold) {
        text = phase.text;
      }
    }
    return text;
  });

  progressPercent = computed(() => {
    const estimated = ESTIMATED_DURATION[this.profile()] ?? 45;
    return Math.min(Math.round((this.elapsedSeconds() / estimated) * 100), 95);
  });

  /** Rotating insight tip — changes every 6 seconds */
  currentTip = computed(() => {
    const tips = INSIGHT_TIPS[this.profile()] ?? INSIGHT_TIPS['triage'];
    const index = Math.floor(this.elapsedSeconds() / 6) % tips.length;
    return tips[index];
  });

  ngOnDestroy(): void {
    // Lifecycle cleanup handled by Angular
  }

  getStageNodeClass(index: number): string {
    const active = this.activeStageIndex();
    if (index < active) {
      return 'bg-emerald-500 text-white shadow-lg shadow-emerald-500/25 stage-completed';
    } else if (index === active) {
      return 'bg-indigo-500 text-white shadow-lg shadow-indigo-500/30 stage-active';
    }
    return 'bg-white/5 border border-white/10 text-white/40 stage-pending';
  }

  getStageTextClass(index: number): string {
    const active = this.activeStageIndex();
    if (index < active) return 'text-emerald-400';
    if (index === active) return 'text-indigo-300 font-semibold';
    return 'text-white/30';
  }

  getConnectorClass(index: number): string {
    const active = this.activeStageIndex();
    if (index < active) return 'bg-emerald-500';
    if (index === active) return 'bg-gradient-to-r from-indigo-500 to-white/10';
    return 'bg-white/10';
  }

  isStageCompleted(index: number): boolean {
    return index < this.activeStageIndex();
  }
}
