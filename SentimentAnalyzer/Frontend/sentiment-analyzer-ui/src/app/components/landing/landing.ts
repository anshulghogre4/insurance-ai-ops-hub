import { Component, OnInit, OnDestroy, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';

/** Represents an AI agent node in the orchestration visualization. */
interface AgentNode {
  id: string;
  name: string;
  role: string;
  color: string;
  bgColor: string;
  borderColor: string;
  icon: string;
  description: string;
  x: number;
  y: number;
}

/** Represents a connection between two agent nodes. */
interface AgentConnection {
  from: string;
  to: string;
  label?: string;
}

/** Represents an LLM provider in the fallback chain. */
interface ProviderNode {
  name: string;
  model: string;
  freeLimit: string;
  color: string;
  bgColor: string;
  status: 'active' | 'cooldown' | 'down';
  latency: string;
}

/** Represents a multimodal processing pipeline stage. */
interface ModalityStage {
  id: string;
  name: string;
  provider: string;
  icon: string;
  inputLabel: string;
  outputLabel: string;
  color: string;
  bgColor: string;
  example: { input: string; output: string };
}

/** A single step in the interactive demo simulation. */
interface DemoStep {
  agent: string;
  message: string;
  duration: number;
}

/** A single PII redaction example item. */
interface PiiExample {
  label: string;
  original: string;
  redacted: string;
  pattern: string;
}

/** A stat to display in the metrics section. */
interface StatItem {
  value: string;
  label: string;
  subtext: string;
  color: string;
  icon: string;
}

/** A technology badge for the tech grid. */
interface TechBadge {
  name: string;
  category: string;
  color: string;
}

@Component({
  selector: 'app-landing',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './landing.html',
  styleUrl: './landing.css'
})
export class LandingComponent implements OnInit, OnDestroy {

  // ─── Section 1: Agent Orchestration ───

  readonly agents: AgentNode[] = [
    { id: 'cto', name: 'CTO', role: 'Orchestrator', color: 'text-indigo-400', bgColor: 'bg-indigo-500/15', borderColor: 'border-indigo-500/30', icon: 'crown', description: 'Decomposes tasks, synthesizes final output, conflict resolution', x: 50, y: 8 },
    { id: 'ba', name: 'Business Analyst', role: 'Domain Expert', color: 'text-cyan-400', bgColor: 'bg-cyan-500/15', borderColor: 'border-cyan-500/30', icon: 'briefcase', description: 'Insurance domain analysis, business rules validation', x: 20, y: 30 },
    { id: 'dev', name: 'Developer', role: 'Implementation', color: 'text-emerald-400', bgColor: 'bg-emerald-500/15', borderColor: 'border-emerald-500/30', icon: 'code', description: 'Response formatting, implementation approach', x: 50, y: 30 },
    { id: 'qa', name: 'QA Agent', role: 'Validation', color: 'text-amber-400', bgColor: 'bg-amber-500/15', borderColor: 'border-amber-500/30', icon: 'check-circle', description: 'Quality validation, consistency checks', x: 80, y: 30 },
    { id: 'ai', name: 'AI Expert', role: 'Model Evaluation', color: 'text-purple-400', bgColor: 'bg-purple-500/15', borderColor: 'border-purple-500/30', icon: 'cpu', description: 'Model selection, responsible AI governance', x: 10, y: 58 },
    { id: 'arch', name: 'Architect', role: 'System Design', color: 'text-teal-400', bgColor: 'bg-teal-500/15', borderColor: 'border-teal-500/30', icon: 'layers', description: 'Storage design, performance optimization', x: 35, y: 58 },
    { id: 'ux', name: 'UX Designer', role: 'Experience', color: 'text-pink-400', bgColor: 'bg-pink-500/15', borderColor: 'border-pink-500/30', icon: 'palette', description: 'Screen layouts, accessibility, design system', x: 65, y: 58 },
    { id: 'triage', name: 'Claims Triage', role: 'Severity Assessment', color: 'text-orange-400', bgColor: 'bg-orange-500/15', borderColor: 'border-orange-500/30', icon: 'clipboard', description: 'Severity assessment, urgency classification, action recommendations', x: 25, y: 82 },
    { id: 'fraud', name: 'Fraud Detection', role: 'Risk Scoring', color: 'text-rose-400', bgColor: 'bg-rose-500/15', borderColor: 'border-rose-500/30', icon: 'shield-alert', description: 'Fraud probability scoring, SIU referral decisions', x: 75, y: 82 },
  ];

  readonly agentConnections: AgentConnection[] = [
    { from: 'cto', to: 'ba', label: 'Decompose' },
    { from: 'cto', to: 'dev' },
    { from: 'cto', to: 'qa' },
    { from: 'ba', to: 'dev' },
    { from: 'ba', to: 'ai' },
    { from: 'dev', to: 'qa' },
    { from: 'dev', to: 'arch' },
    { from: 'qa', to: 'ux' },
    { from: 'ai', to: 'arch' },
    { from: 'triage', to: 'fraud' },
    { from: 'ba', to: 'triage' },
    { from: 'qa', to: 'fraud' },
  ];

  activeAgentIndex = signal(-1);
  agentAnimationPhase = signal('idle');
  private agentAnimationTimer: ReturnType<typeof setInterval> | null = null;

  readonly orchestrationSequence = [
    { index: 0, label: 'CTO decomposes the claim...' },
    { index: 1, label: 'Business Analyst reviews insurance context...' },
    { index: 2, label: 'Developer formats the response...' },
    { index: 3, label: 'QA Agent validates consistency...' },
    { index: 4, label: 'AI Expert evaluates model selection...' },
    { index: 5, label: 'Architect checks storage design...' },
    { index: 6, label: 'UX Designer reviews experience...' },
    { index: 7, label: 'Claims Triage assesses severity...' },
    { index: 8, label: 'Fraud Detection scores risk...' },
    { index: -1, label: 'ANALYSIS_COMPLETE' },
  ];

  orchestrationStepLabel = signal('Click "Run Orchestration" to begin');

  // ─── Section 2: Provider Fallback Chain ───

  readonly providers: ProviderNode[] = [
    { name: 'Groq', model: 'Llama 3.3 70B', freeLimit: '250 req/day', color: 'text-orange-400', bgColor: 'bg-orange-500/15', status: 'active', latency: '~0.3s' },
    { name: 'Mistral', model: 'Mistral Large', freeLimit: '500K tokens/mo', color: 'text-blue-400', bgColor: 'bg-blue-500/15', status: 'active', latency: '~0.8s' },
    { name: 'Gemini', model: 'Gemini Pro', freeLimit: '60 req/min', color: 'text-cyan-400', bgColor: 'bg-cyan-500/15', status: 'active', latency: '~1.2s' },
    { name: 'OpenRouter', model: 'Multi-model', freeLimit: '$1 credit', color: 'text-purple-400', bgColor: 'bg-purple-500/15', status: 'active', latency: '~1.5s' },
    { name: 'Ollama', model: 'Local LLM', freeLimit: 'Unlimited', color: 'text-emerald-400', bgColor: 'bg-emerald-500/15', status: 'active', latency: '~2.0s' },
  ];

  providerChainStep = signal(-1);
  failoverActive = signal(false);
  private failoverTimer: ReturnType<typeof setTimeout> | null = null;

  // ─── Section 3: Multimodal Pipeline ───

  readonly modalities: ModalityStage[] = [
    {
      id: 'stt', name: 'Voice to Text', provider: 'Deepgram Nova-2',
      icon: 'microphone', inputLabel: 'Audio Waveform', outputLabel: 'Transcript',
      color: 'text-violet-400', bgColor: 'bg-violet-500/15',
      example: { input: 'adjuster-call-recording.wav', output: '"The policyholder reports water damage in the basement affecting approximately 800 sq ft..."' }
    },
    {
      id: 'vision', name: 'Image Analysis', provider: 'Azure Vision / Cloudflare',
      icon: 'eye', inputLabel: 'Damage Photo', outputLabel: 'Damage Assessment',
      color: 'text-blue-400', bgColor: 'bg-blue-500/15',
      example: { input: 'water-damage-basement.jpg', output: 'Detected: Standing water (6+ inches), damaged drywall, warped flooring. Severity: HIGH' }
    },
    {
      id: 'ocr', name: 'Document OCR', provider: 'OCR.space',
      icon: 'document', inputLabel: 'Scanned Document', outputLabel: 'Extracted Text',
      color: 'text-amber-400', bgColor: 'bg-amber-500/15',
      example: { input: 'policy-declaration-page.pdf', output: 'Policy: HO-2024-789456 | Coverage: $350,000 | Deductible: $1,000 | Effective: 01/01/2024' }
    },
    {
      id: 'ner', name: 'Entity Extraction', provider: 'HuggingFace BERT',
      icon: 'tag', inputLabel: 'Raw Text', outputLabel: 'Named Entities',
      color: 'text-emerald-400', bgColor: 'bg-emerald-500/15',
      example: { input: 'John Smith reported damage at 123 Oak St on Feb 10...', output: 'PER: John Smith | LOC: 123 Oak St | DATE: Feb 10 | POLICY: HO-2024-789456' }
    },
  ];

  activeModalityIndex = signal(0);
  modalityAnimating = signal(false);

  // ─── Section 4: Interactive Demo ───

  demoText = signal('');
  demoRunning = signal(false);
  demoComplete = signal(false);
  demoActiveAgent = signal('');
  demoPhase = signal('');
  demoFraudScore = signal(0);
  demoSeverity = signal('');
  demoActions = signal<string[]>([]);
  private demoTimers: ReturnType<typeof setTimeout>[] = [];

  readonly sampleClaimText = 'I reported water damage on Jan 15. My basement flooded after a pipe burst during the cold snap. The flooding affected approximately 800 sq ft, damaging drywall and furniture. Policy HO-2024-789456. Estimated repairs: $15,000. I have photos and a plumber report. Please assign an adjuster immediately.';

  readonly demoSteps: DemoStep[] = [
    { agent: 'CTO Agent', message: 'Decomposing claim into analysis tasks...', duration: 1200 },
    { agent: 'Claims Triage', message: 'Assessing severity: CRITICAL - Water damage, large area...', duration: 1500 },
    { agent: 'Fraud Detection', message: 'Scoring risk indicators... Score: 18/100 (Low)...', duration: 1400 },
    { agent: 'Business Analyst', message: 'Validating against policy HO-2024-789456 coverage...', duration: 1300 },
    { agent: 'QA Agent', message: 'Cross-checking consistency, quality score: 94/100...', duration: 1000 },
    { agent: 'CTO Agent', message: 'ANALYSIS_COMPLETE - Synthesizing final assessment...', duration: 800 },
  ];

  // ─── Section 5: PII Redaction ───

  readonly piiExamples: PiiExample[] = [
    { label: 'SSN', original: '123-45-6789', redacted: '[SSN-REDACTED]', pattern: '\\d{3}-\\d{2}-\\d{4}' },
    { label: 'Policy #', original: 'HO-2024-789456', redacted: '[POLICY-REDACTED]', pattern: '[A-Z]{2,3}-\\d{4,10}' },
    { label: 'Claim #', original: 'CLM-2024-00045678', redacted: '[CLAIM-REDACTED]', pattern: 'CLM-\\d{4}-\\d{4,8}' },
    { label: 'Phone', original: '(555) 123-4567', redacted: '[PHONE-REDACTED]', pattern: '\\(\\d{3}\\) \\d{3}-\\d{4}' },
    { label: 'Email', original: 'john.smith@email.com', redacted: '[EMAIL-REDACTED]', pattern: '[\\w.]+@[\\w.]+' },
  ];

  piiShowRedacted = signal(false);
  private piiToggleTimer: ReturnType<typeof setInterval> | null = null;

  // ─── Section 6: Stats & Metrics ───

  readonly stats: StatItem[] = [
    { value: '9', label: 'AI Agents', subtext: 'Specialized collaboration', color: 'text-indigo-400', icon: 'users' },
    { value: '5', label: 'LLM Providers', subtext: 'Resilient fallback chain', color: 'text-cyan-400', icon: 'server' },
    { value: '5', label: 'Multimodal Services', subtext: 'Voice, vision, OCR, NER', color: 'text-purple-400', icon: 'layers' },
    { value: '99.9%', label: 'Uptime Target', subtext: '5-provider redundancy', color: 'text-emerald-400', icon: 'shield' },
    { value: '< 60s', label: 'Analysis Time', subtext: '9-agent orchestration', color: 'text-amber-400', icon: 'clock' },
    { value: '100%', label: 'PII Protection', subtext: '5 redaction patterns', color: 'text-rose-400', icon: 'lock' },
    { value: '4', label: 'Orchestration Profiles', subtext: 'Optimized token usage', color: 'text-teal-400', icon: 'sliders' },
    { value: '246+', label: 'Test Cases', subtext: 'xUnit + Vitest + Playwright', color: 'text-orange-400', icon: 'check-list' },
  ];

  // ─── Section 7: Technology Badges ───

  readonly techCategories: { label: string; color: string; badges: TechBadge[] }[] = [
    {
      label: 'AI / ML',
      color: 'text-indigo-400',
      badges: [
        { name: 'Semantic Kernel', category: 'ai', color: 'border-indigo-500/30 bg-indigo-500/10 text-indigo-300' },
        { name: 'Groq', category: 'ai', color: 'border-orange-500/30 bg-orange-500/10 text-orange-300' },
        { name: 'Mistral', category: 'ai', color: 'border-blue-500/30 bg-blue-500/10 text-blue-300' },
        { name: 'Gemini', category: 'ai', color: 'border-cyan-500/30 bg-cyan-500/10 text-cyan-300' },
        { name: 'OpenRouter', category: 'ai', color: 'border-purple-500/30 bg-purple-500/10 text-purple-300' },
        { name: 'Ollama', category: 'ai', color: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300' },
      ]
    },
    {
      label: 'Multimodal',
      color: 'text-purple-400',
      badges: [
        { name: 'Deepgram', category: 'multi', color: 'border-violet-500/30 bg-violet-500/10 text-violet-300' },
        { name: 'Azure Vision', category: 'multi', color: 'border-blue-500/30 bg-blue-500/10 text-blue-300' },
        { name: 'Cloudflare AI', category: 'multi', color: 'border-orange-500/30 bg-orange-500/10 text-orange-300' },
        { name: 'OCR.space', category: 'multi', color: 'border-amber-500/30 bg-amber-500/10 text-amber-300' },
        { name: 'HuggingFace', category: 'multi', color: 'border-yellow-500/30 bg-yellow-500/10 text-yellow-300' },
      ]
    },
    {
      label: 'Stack',
      color: 'text-emerald-400',
      badges: [
        { name: '.NET 10', category: 'stack', color: 'border-purple-500/30 bg-purple-500/10 text-purple-300' },
        { name: 'Angular 21', category: 'stack', color: 'border-rose-500/30 bg-rose-500/10 text-rose-300' },
        { name: 'TypeScript 5.9', category: 'stack', color: 'border-blue-500/30 bg-blue-500/10 text-blue-300' },
        { name: 'Tailwind CSS', category: 'stack', color: 'border-cyan-500/30 bg-cyan-500/10 text-cyan-300' },
        { name: 'SQLite / Supabase', category: 'stack', color: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300' },
        { name: 'EF Core', category: 'stack', color: 'border-indigo-500/30 bg-indigo-500/10 text-indigo-300' },
      ]
    },
    {
      label: 'Quality & Security',
      color: 'text-rose-400',
      badges: [
        { name: 'WCAG AA', category: 'quality', color: 'border-emerald-500/30 bg-emerald-500/10 text-emerald-300' },
        { name: 'PII Redaction', category: 'quality', color: 'border-rose-500/30 bg-rose-500/10 text-rose-300' },
        { name: 'Resilient Failover', category: 'quality', color: 'border-amber-500/30 bg-amber-500/10 text-amber-300' },
        { name: 'xUnit', category: 'quality', color: 'border-teal-500/30 bg-teal-500/10 text-teal-300' },
        { name: 'Vitest', category: 'quality', color: 'border-green-500/30 bg-green-500/10 text-green-300' },
        { name: 'Playwright', category: 'quality', color: 'border-orange-500/30 bg-orange-500/10 text-orange-300' },
      ]
    },
  ];

  // ─── Scroll & Intersection Observer ───

  visibleSections = signal<Set<string>>(new Set());
  activeSection = signal('hero');

  readonly sectionNav = [
    { id: 'hero', label: 'Home' },
    { id: 'agents', label: 'Agents' },
    { id: 'providers', label: 'Providers' },
    { id: 'multimodal', label: 'Multimodal' },
    { id: 'demo', label: 'Demo' },
    { id: 'security', label: 'Security' },
    { id: 'stats', label: 'Stats' },
    { id: 'tech', label: 'Tech' },
  ];

  private observer: IntersectionObserver | null = null;

  // ─── Lifecycle ───

  ngOnInit(): void {
    // Mark hero as visible immediately - no animation needed for first visible section
    this.visibleSections.update(set => {
      const next = new Set(set);
      next.add('hero');
      return next;
    });
    this.setupIntersectionObserver();
    this.startPiiAnimation();
    this.startModalityCycle();
  }

  ngOnDestroy(): void {
    this.stopAgentAnimation();
    this.stopFailoverAnimation();
    this.stopDemoAnimation();
    this.stopPiiAnimation();
    this.stopModalityCycle();
    if (this.observer) {
      this.observer.disconnect();
    }
  }

  // ─── Intersection Observer for Scroll Animations ───

  private setupIntersectionObserver(): void {
    if (typeof IntersectionObserver === 'undefined') return;
    this.observer = new IntersectionObserver(
      (entries) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            this.visibleSections.update(set => {
              const next = new Set(set);
              next.add(entry.target.id);
              return next;
            });
            // Track the active section for section navigation
            if (entry.intersectionRatio >= 0.15) {
              this.activeSection.set(entry.target.id);
            }
          }
        });
      },
      { threshold: [0.1, 0.15, 0.5], rootMargin: '0px 0px -50px 0px' }
    );

    // Defer to allow DOM to render
    setTimeout(() => {
      const sectionIds = ['hero', 'agents', 'providers', 'multimodal', 'demo', 'security', 'stats', 'tech'];
      sectionIds.forEach(id => {
        const el = document.getElementById(id);
        if (el && this.observer) {
          this.observer.observe(el);
        }
      });
    }, 100);
  }

  isSectionVisible(id: string): boolean {
    return this.visibleSections().has(id);
  }

  // ─── Section 1: Agent Orchestration Controls ───

  startOrchestration(): void {
    if (this.agentAnimationPhase() === 'running') return;
    this.agentAnimationPhase.set('running');
    this.activeAgentIndex.set(-1);

    let step = 0;
    this.agentAnimationTimer = setInterval(() => {
      if (step >= this.orchestrationSequence.length) {
        this.agentAnimationPhase.set('complete');
        this.orchestrationStepLabel.set('ANALYSIS_COMPLETE - All agents have reported.');
        if (this.agentAnimationTimer) clearInterval(this.agentAnimationTimer);
        return;
      }
      const current = this.orchestrationSequence[step];
      this.activeAgentIndex.set(current.index);
      this.orchestrationStepLabel.set(current.label);
      step++;
    }, 1200);
  }

  resetOrchestration(): void {
    this.stopAgentAnimation();
    this.activeAgentIndex.set(-1);
    this.agentAnimationPhase.set('idle');
    this.orchestrationStepLabel.set('Click "Run Orchestration" to begin');
  }

  private stopAgentAnimation(): void {
    if (this.agentAnimationTimer) {
      clearInterval(this.agentAnimationTimer);
      this.agentAnimationTimer = null;
    }
  }

  isAgentActive(index: number): boolean {
    return this.activeAgentIndex() === index;
  }

  isAgentCompleted(index: number): boolean {
    const currentActive = this.activeAgentIndex();
    if (this.agentAnimationPhase() === 'complete') return true;
    if (this.agentAnimationPhase() !== 'running') return false;

    // Find the step index for the given agent index
    const agentStepIndex = this.orchestrationSequence.findIndex(s => s.index === index);
    const currentStepIndex = this.orchestrationSequence.findIndex(s => s.index === currentActive);

    return agentStepIndex >= 0 && currentStepIndex >= 0 && agentStepIndex < currentStepIndex;
  }

  // ─── Section 2: Provider Failover Animation ───

  startFailover(): void {
    if (this.failoverActive()) return;
    this.failoverActive.set(true);
    this.providerChainStep.set(0);

    const stepThrough = (index: number) => {
      if (index >= this.providers.length) {
        this.failoverActive.set(false);
        this.providerChainStep.set(0);
        return;
      }
      this.providerChainStep.set(index);
      this.failoverTimer = setTimeout(() => {
        // Simulate: first two fail, third succeeds
        if (index < 2) {
          stepThrough(index + 1);
        } else {
          // Success - stay on this one
          this.failoverActive.set(false);
        }
      }, 1200);
    };

    stepThrough(0);
  }

  private stopFailoverAnimation(): void {
    if (this.failoverTimer) {
      clearTimeout(this.failoverTimer);
      this.failoverTimer = null;
    }
  }

  getProviderStatus(index: number): string {
    if (!this.failoverActive() && this.providerChainStep() === -1) return 'idle';
    if (this.failoverActive()) {
      if (index < this.providerChainStep()) return 'failed';
      if (index === this.providerChainStep()) return 'trying';
      return 'waiting';
    }
    // Animation complete
    if (index < this.providerChainStep()) return 'failed';
    if (index === this.providerChainStep()) return 'success';
    return 'idle';
  }

  // ─── Section 3: Modality Cycle ───

  private modalityTimer: ReturnType<typeof setInterval> | null = null;

  selectModality(index: number): void {
    this.activeModalityIndex.set(index);
    this.modalityAnimating.set(true);
    setTimeout(() => this.modalityAnimating.set(false), 600);
  }

  private startModalityCycle(): void {
    this.modalityTimer = setInterval(() => {
      this.activeModalityIndex.update(i => (i + 1) % this.modalities.length);
      this.modalityAnimating.set(true);
      setTimeout(() => this.modalityAnimating.set(false), 600);
    }, 5000);
  }

  private stopModalityCycle(): void {
    if (this.modalityTimer) {
      clearInterval(this.modalityTimer);
      this.modalityTimer = null;
    }
  }

  // ─── Section 4: Interactive Demo ───

  loadSampleText(): void {
    this.demoText.set(this.sampleClaimText);
  }

  runDemo(): void {
    const text = this.demoText();
    if (!text.trim() || this.demoRunning()) return;

    this.demoRunning.set(true);
    this.demoComplete.set(false);
    this.demoActiveAgent.set('');
    this.demoPhase.set('');
    this.demoFraudScore.set(0);
    this.demoSeverity.set('');
    this.demoActions.set([]);

    let totalDelay = 0;
    this.demoSteps.forEach((step, i) => {
      const timer = setTimeout(() => {
        this.demoActiveAgent.set(step.agent);
        this.demoPhase.set(step.message);

        // Animate fraud score on step 3
        if (i === 2) {
          this.animateFraudScore(18);
        }
        // Set severity on step 2
        if (i === 1) {
          this.demoSeverity.set('Critical');
        }
        // Set actions on final step
        if (i === this.demoSteps.length - 1) {
          setTimeout(() => {
            this.demoActions.set([
              'Assign field adjuster within 24 hours',
              'Schedule emergency mitigation for mold prevention',
              'Verify policy HO-2024-789456 coverage limits',
              'Request plumber report and damage photos',
            ]);
            this.demoComplete.set(true);
            this.demoRunning.set(false);
          }, step.duration);
        }
      }, totalDelay);
      this.demoTimers.push(timer);
      totalDelay += step.duration;
    });
  }

  resetDemo(): void {
    this.stopDemoAnimation();
    this.demoRunning.set(false);
    this.demoComplete.set(false);
    this.demoActiveAgent.set('');
    this.demoPhase.set('');
    this.demoFraudScore.set(0);
    this.demoSeverity.set('');
    this.demoActions.set([]);
  }

  private animateFraudScore(target: number): void {
    let current = 0;
    const interval = setInterval(() => {
      current += 1;
      this.demoFraudScore.set(current);
      if (current >= target) clearInterval(interval);
    }, 40);
  }

  private stopDemoAnimation(): void {
    this.demoTimers.forEach(t => clearTimeout(t));
    this.demoTimers = [];
  }

  // ─── Section 5: PII Animation ───

  private startPiiAnimation(): void {
    this.piiToggleTimer = setInterval(() => {
      this.piiShowRedacted.update(v => !v);
    }, 3000);
  }

  private stopPiiAnimation(): void {
    if (this.piiToggleTimer) {
      clearInterval(this.piiToggleTimer);
      this.piiToggleTimer = null;
    }
  }

  togglePiiView(): void {
    this.piiShowRedacted.update(v => !v);
  }

  // ─── Scroll to section ───

  scrollToSection(sectionId: string): void {
    // Immediately mark the target section (and all sections above it) visible
    // This prevents the opacity:0 CSS from hiding content when smooth-scrolling
    const sectionOrder = ['hero', 'agents', 'providers', 'multimodal', 'demo', 'security', 'stats', 'tech'];
    const targetIdx = sectionOrder.indexOf(sectionId);
    this.visibleSections.update(set => {
      const next = new Set(set);
      for (let i = 0; i <= targetIdx; i++) {
        next.add(sectionOrder[i]);
      }
      return next;
    });
    this.activeSection.set(sectionId);
    const el = document.getElementById(sectionId);
    if (el) {
      el.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }
  }
}
