# MCP Integration Reference — Insurance AI Operations Hub

> **Purpose:** Comprehensive reference for Model Context Protocol (MCP) server integration. Covers current setup, Playwright MCP workflows, Sprint 5 planned servers, and how to add new MCP servers.

---

## Table of Contents

1. [What is MCP](#1-what-is-mcp)
2. [Current Configuration](#2-current-configuration)
3. [Playwright MCP Deep Dive](#3-playwright-mcp-deep-dive)
4. [Stitch MCP Deep Dive](#4-stitch-mcp-deep-dive)
5. [Playwright MCP Workflows for Insurance App](#5-playwright-mcp-workflows-for-insurance-app)
6. [Sprint 5 Planned MCP Servers](#6-sprint-5-planned-mcp-servers)
7. [Adding New MCP Servers](#7-adding-new-mcp-servers)
8. [MCP Best Practices](#8-mcp-best-practices)
9. [Troubleshooting](#9-troubleshooting)

---

## 1. What is MCP

### Overview
Model Context Protocol (MCP) is an open protocol that enables AI assistants (like Claude Code) to interact with external tools and services through standardized server interfaces. MCP servers expose **tools** that Claude Code can call during conversations.

### How Claude Code Uses MCP
- MCP servers are configured in `.mcp.json` at the project root
- Claude Code starts configured MCP servers automatically when the project loads
- Tools from MCP servers appear alongside built-in tools (Read, Write, Bash, etc.)
- Each MCP server can expose multiple tools with specific parameters

### Transport Types
| Transport | How It Works | Use Case |
|-----------|-------------|----------|
| **stdio** | Claude Code spawns the server as a child process, communicates via stdin/stdout | Local tools (Playwright, file processors) |
| **proxy** | Connects to an external proxy service | Cloud services (Stitch, authenticated APIs) |
| **sse** | Server-Sent Events over HTTP | Long-running services, webhooks |

---

## 2. Current Configuration

### `.mcp.json` (Project Root)

```json
{
  "mcpServers": {
    "playwright": {
      "command": "npx",
      "args": ["@playwright/mcp@latest", "--headless"]
    },
    "stitch": {
      "command": "npx",
      "args": ["@_davideast/stitch-mcp", "proxy"]
    }
  }
}
```

### Server Status
| Server | Package | Transport | Status | Added |
|--------|---------|-----------|--------|-------|
| **Playwright** | `@playwright/mcp@latest` | stdio (headless) | Active | Sprint 4 Week 4 |
| **Stitch** | `@_davideast/stitch-mcp` | proxy | Active | Sprint 4 Week 4 |

---

## 3. Playwright MCP Deep Dive

### What It Does
Playwright MCP provides headless browser automation directly within Claude Code. It can navigate web pages, interact with UI elements, take screenshots, capture network traffic, and evaluate JavaScript — all without leaving the conversation.

### Available Tools (18)

#### Navigation
| Tool | Parameters | Purpose |
|------|-----------|---------|
| `browser_navigate` | `url: string` | Navigate to a URL |
| `browser_navigate_back` | (none) | Go back in browser history |
| `browser_tabs` | `action: list/new/close/select` | Manage browser tabs |

#### Page Inspection
| Tool | Parameters | Purpose |
|------|-----------|---------|
| `browser_snapshot` | `filename?: string` | Capture accessibility tree (better than screenshot for actions) |
| `browser_take_screenshot` | `type: png/jpeg, fullPage?: bool, ref?: string` | Visual screenshot |
| `browser_console_messages` | `level: error/warning/info/debug` | Get browser console output |
| `browser_network_requests` | `includeStatic?: bool` | List all network requests |

#### User Interaction
| Tool | Parameters | Purpose |
|------|-----------|---------|
| `browser_click` | `ref: string, element?: string` | Click an element |
| `browser_type` | `ref: string, text: string, submit?: bool` | Type text into field |
| `browser_fill_form` | `fields: [{name, type, ref, value}]` | Fill multiple form fields |
| `browser_select_option` | `ref: string, values: string[]` | Select dropdown option |
| `browser_press_key` | `key: string` | Press keyboard key |
| `browser_hover` | `ref: string` | Hover over element |
| `browser_drag` | `startRef, endRef` | Drag and drop |
| `browser_file_upload` | `paths: string[]` | Upload files to file input |

#### Utilities
| Tool | Parameters | Purpose |
|------|-----------|---------|
| `browser_evaluate` | `function: string` | Execute JavaScript on page |
| `browser_wait_for` | `text?: string, textGone?: string, time?: number` | Wait for condition |
| `browser_handle_dialog` | `accept: bool, promptText?: string` | Handle alert/confirm/prompt |
| `browser_resize` | `width: number, height: number` | Resize browser window |
| `browser_close` | (none) | Close browser |
| `browser_install` | (none) | Install browser if missing |

### How `ref` Works
- Call `browser_snapshot` first to get the accessibility tree
- Each interactive element in the snapshot has a `ref` identifier (e.g., `ref="S1E3"`)
- Use that `ref` in subsequent `browser_click`, `browser_type`, etc. calls

### Insurance App Example
```
1. browser_navigate → http://localhost:4200/claims/triage
2. browser_snapshot → get accessibility tree with form field refs
3. browser_type → ref="textarea-claim", text="Water pipe burst..."
4. browser_click → ref="button-submit"
5. browser_wait_for → text="Triaged"
6. browser_take_screenshot → capture result display
7. browser_console_messages → check for errors
```

---

## 4. Stitch MCP Deep Dive

### What It Does
Stitch MCP connects to Google's Stitch AI design-to-code pipeline. You describe a UI component in natural language, and Stitch generates Angular 21 + Tailwind CSS code.

### Setup
```bash
# One-time authentication
npx @_davideast/stitch-mcp init
```
This opens a browser for Google OAuth. After auth, Stitch proxy is ready.

### Use Cases for Insurance App
- **Sprint 5 UI Revamp:** Redesign all 18 components with modern layouts
- **New Component Scaffolding:** Describe a component, get Angular code
- **Design System Updates:** Generate consistent styling across components

---

## 5. Playwright MCP Workflows for Insurance App

### Workflow 1: Live API Testing via Browser

**Goal:** Navigate to each route, fill forms with real insurance data, submit, verify response rendering.

```
Step 1: browser_navigate → http://localhost:4200/claims/triage
Step 2: browser_snapshot → identify form elements
Step 3: browser_type → fill claim text with realistic water damage description
Step 4: browser_click → submit button
Step 5: browser_wait_for → "Triaged" or "Critical" (severity result)
Step 6: browser_take_screenshot → capture the triage result display
Step 7: browser_console_messages → verify no JavaScript errors
Step 8: browser_network_requests → verify API call to /api/insurance/claims/triage returned 200
```

**Repeat for each of 15 routes with appropriate form data.**

### Workflow 2: UI/UX Validation

**Goal:** Verify accessibility, ARIA labels, responsive layout, theme consistency.

```
Step 1: browser_navigate → target route
Step 2: browser_snapshot → capture full accessibility tree
Step 3: Verify: all buttons have aria-labels, all inputs have labels, headings are hierarchical
Step 4: browser_resize → { width: 375, height: 667 } (mobile)
Step 5: browser_snapshot → verify mobile layout (hamburger menu, stacked cards)
Step 6: browser_resize → { width: 1280, height: 720 } (desktop)
Step 7: browser_take_screenshot → baseline screenshot
```

### Workflow 3: E2E Test Generation

**Goal:** Record a user journey and generate a Playwright test spec.

```
Step 1: browser_navigate → http://localhost:4200/documents/upload
Step 2: browser_snapshot → identify form elements
Step 3: Interact: select category, upload file, submit
Step 4: browser_wait_for → upload result
Step 5: browser_click → "Query this document" action
Step 6: browser_type → question text
Step 7: browser_click → submit query
Step 8: browser_wait_for → answer display

→ Generate e2e/document-journey.spec.ts from recorded interactions
```

### Workflow 4: Visual Regression (Sprint 5)

**Goal:** Before/after screenshots for UI revamp comparison.

```
Step 1: browser_navigate → each route
Step 2: browser_take_screenshot → filename: "before-sprint5/{route}.png"
--- Apply Stitch MCP redesign ---
Step 3: browser_navigate → same routes
Step 4: browser_take_screenshot → filename: "after-sprint5/{route}.png"
Step 5: Compare screenshots side-by-side
```

### Workflow 5: SSE Streaming Validation

**Goal:** Test CX Copilot streaming in the browser (not just mocked).

```
Step 1: browser_navigate → http://localhost:4200/cx/copilot
Step 2: browser_snapshot → identify chat input
Step 3: browser_type → "What is the claims process for water damage?"
Step 4: browser_click → send button
Step 5: browser_wait_for → (wait 5-10 seconds for streaming to complete)
Step 6: browser_snapshot → verify message appeared with tone badge
Step 7: browser_take_screenshot → capture complete chat UI
Step 8: browser_console_messages → verify no SSE parsing errors
```

---

## 6. Sprint 5 Planned MCP Servers

### Priority Tiers

#### Tier 1: God-Tier Stack (P0 — Set up in Sprint 5 Week 1)

##### 1. Supabase MCP
```json
{
  "supabase": {
    "command": "npx",
    "args": ["-y", "@supabase/mcp-server-supabase@latest", "--supabase-url", "https://your-project.supabase.co", "--supabase-service-role-key", "your-service-role-key"]
  }
}
```
- **Purpose:** Schema management, query testing, migrations, RLS policies
- **Free Tier:** 500 MB database, 1 GB file storage, 50K monthly active users
- **Use Cases:** Test SQLite → PostgreSQL migration, validate schema changes, run ad-hoc queries

##### 2. GitHub MCP
```json
{
  "github": {
    "command": "npx",
    "args": ["-y", "@modelcontextprotocol/server-github"],
    "env": { "GITHUB_PERSONAL_ACCESS_TOKEN": "ghp_your_token" }
  }
}
```
- **Purpose:** PR management, automated code review, issue tracking
- **Free Tier:** Unlimited public repos, 2000 Actions minutes/month
- **Use Cases:** Create PRs from Claude Code, review PR diffs, manage sprint issues

##### 3. Context7 MCP
```json
{
  "context7": {
    "command": "npx",
    "args": ["-y", "@context7/mcp-server"]
  }
}
```
- **Purpose:** Up-to-date documentation for .NET 10, Angular 21, Semantic Kernel, Tailwind
- **Free Tier:** Unlimited
- **Use Cases:** Query latest API docs without hallucination, verify breaking changes, get code examples

##### 4. Sequential Thinking MCP
```json
{
  "sequential-thinking": {
    "command": "npx",
    "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"]
  }
}
```
- **Purpose:** Structured multi-step reasoning for complex decisions
- **Free Tier:** Unlimited (runs locally)
- **Use Cases:** Architecture decisions, debugging complex agent interactions, planning sprint work

#### Tier 2: Observability & Security (P1 — Sprint 5 Week 2)

##### 5. Sentry MCP
```json
{
  "sentry": {
    "command": "npx",
    "args": ["-y", "@sentry/mcp-server"],
    "env": { "SENTRY_AUTH_TOKEN": "your-token", "SENTRY_ORG": "your-org" }
  }
}
```
- **Purpose:** Error tracking across 5 LLM + 5 multimodal providers
- **Free Tier:** 5,000 errors/month, 10,000 performance transactions/month
- **Use Cases:** Track provider failures, monitor fallback chain behavior, alert on error spikes

##### 6. Grafana MCP
```json
{
  "grafana": {
    "command": "npx",
    "args": ["-y", "@grafana/mcp-server"],
    "env": { "GRAFANA_URL": "http://localhost:3000", "GRAFANA_API_KEY": "your-key" }
  }
}
```
- **Purpose:** Provider health dashboards, latency monitoring
- **Free Tier:** Grafana Cloud free (10K metrics series, 50GB logs, 50GB traces)
- **Use Cases:** Build LLM provider latency dashboard, multimodal service uptime tracking

##### 7. Snyk MCP
```json
{
  "snyk": {
    "command": "npx",
    "args": ["-y", "@snyk/mcp-server"],
    "env": { "SNYK_TOKEN": "your-token" }
  }
}
```
- **Purpose:** Dependency vulnerability scanning for .NET + npm
- **Free Tier:** Unlimited tests for open source projects
- **Use Cases:** Scan NuGet + npm dependencies, identify CVEs, suggest fixes

#### Tier 3: Caching & Research (P2 — Sprint 5 Week 3+)

##### 8. Upstash MCP
```json
{
  "upstash": {
    "command": "npx",
    "args": ["-y", "@upstash/mcp-server"],
    "env": { "UPSTASH_REDIS_REST_URL": "https://your-redis.upstash.io", "UPSTASH_REDIS_REST_TOKEN": "your-token" }
  }
}
```
- **Purpose:** Redis rate limiting + analysis result caching
- **Free Tier:** 10,000 commands/day, 256 MB storage
- **Use Cases:** Cache expensive LLM analysis results, implement distributed rate limiting

##### 9. Tavily MCP
```json
{
  "tavily": {
    "command": "npx",
    "args": ["-y", "@tavily/mcp-server"],
    "env": { "TAVILY_API_KEY": "your-key" }
  }
}
```
- **Purpose:** Insurance domain research, regulatory updates
- **Free Tier:** 1,000 searches/month
- **Use Cases:** Research insurance regulations, find industry benchmarks, validate domain knowledge

### Cost Analysis
| Server | Monthly Cost |
|--------|-------------|
| Playwright | $0 (local) |
| Stitch | $0 (Google free tier) |
| Supabase | $0 (free tier) |
| GitHub | $0 (free tier) |
| Context7 | $0 (local) |
| Sequential Thinking | $0 (local) |
| Sentry | $0 (free tier) |
| Grafana | $0 (cloud free tier) |
| Snyk | $0 (open source) |
| Upstash | $0 (free tier) |
| Tavily | $0 (free tier) |
| **Total** | **$0/month** |

---

## 7. Adding New MCP Servers

### Step-by-Step Guide

#### Step 1: Choose the MCP Server
- Browse the [MCP server registry](https://github.com/modelcontextprotocol/servers)
- Verify it supports your use case
- Check the transport type (stdio, proxy, sse)

#### Step 2: Edit `.mcp.json`

Add the server configuration to your project's `.mcp.json`:

```json
{
  "mcpServers": {
    "existing-servers": { "..." },
    "new-server": {
      "command": "npx",
      "args": ["-y", "@package/mcp-server"],
      "env": {
        "API_KEY": "your-key"
      }
    }
  }
}
```

#### Step 3: Install Dependencies (if needed)
```bash
# Most MCP servers auto-install via npx -y
# Some may need manual install:
npm install -g @package/mcp-server
```

#### Step 4: One-Time Auth (if needed)
Some servers require initial authentication:
```bash
npx @package/mcp-server init  # Opens browser for OAuth
```

#### Step 5: Restart Claude Code
MCP servers are loaded when Claude Code starts. After editing `.mcp.json`:
1. Close the current Claude Code session
2. Reopen the project
3. Claude Code will automatically start the new MCP server

#### Step 6: Verify
Ask Claude Code: "What MCP tools are available?" — the new server's tools should appear.

### Config Templates

#### stdio (local process)
```json
{
  "server-name": {
    "command": "npx",
    "args": ["-y", "@package/mcp-server", "--flag", "value"]
  }
}
```

#### stdio with environment variables
```json
{
  "server-name": {
    "command": "npx",
    "args": ["-y", "@package/mcp-server"],
    "env": {
      "API_KEY": "your-key",
      "BASE_URL": "https://api.example.com"
    }
  }
}
```

#### proxy (external service)
```json
{
  "server-name": {
    "command": "npx",
    "args": ["@package/mcp-server", "proxy"]
  }
}
```

---

## 8. MCP Best Practices

### When to Use MCP vs Direct CLI
| Task | Use MCP | Use CLI |
|------|---------|---------|
| Browse a webpage | Playwright MCP | - |
| Take screenshots | Playwright MCP | - |
| Fill forms and click buttons | Playwright MCP | - |
| Run Playwright test suite | - | `npm run e2e` |
| Create a PR | GitHub MCP | `gh pr create` |
| Query documentation | Context7 MCP | - |
| Run backend tests | - | `dotnet test` |
| Build project | - | `dotnet build` / `ng build` |

**Rule of thumb:** Use MCP for interactive/exploratory tasks. Use CLI for batch/scripted tasks.

### Composing Multiple MCP Servers
MCP servers can be used together in a single workflow:

**Example: Full Feature Workflow**
1. **Context7 MCP** → Look up Angular 21 signal patterns
2. **Sequential Thinking** → Plan component architecture
3. **Stitch MCP** → Generate UI design from description
4. Code the component (built-in tools)
5. **Playwright MCP** → Navigate to component, validate rendering
6. **GitHub MCP** → Create PR with the changes
7. **Sentry MCP** → Verify no new errors after deployment

### Security Considerations
- **Never commit API keys** in `.mcp.json` — use environment variables instead
- **Use `.env` files** for secrets, ensure `.gitignore` includes `.env`
- **Rotate keys regularly** — especially for services with free tier limits
- **Principle of least privilege** — only grant MCP servers the permissions they need
- **Audit MCP packages** — verify packages are from trusted publishers

### Environment Variable Pattern (Recommended)
Instead of hardcoding keys in `.mcp.json`:

```json
{
  "server-name": {
    "command": "npx",
    "args": ["-y", "@package/mcp-server"],
    "env": {
      "API_KEY": "${SENTRY_AUTH_TOKEN}"
    }
  }
}
```

Set environment variables in your shell profile or `.env` file.

---

## 9. Troubleshooting

### Common Issues

#### "MCP server failed to start"
- Verify Node.js is installed: `node --version` (need 18+)
- Try running the server manually: `npx @package/mcp-server`
- Check for port conflicts

#### "Tool not found"
- Restart Claude Code (MCP servers load on startup)
- Verify `.mcp.json` syntax (valid JSON)
- Check server logs in Claude Code output

#### "Browser not installed" (Playwright MCP)
- Call `browser_install` tool
- Or run: `npx playwright install chromium`

#### "Authentication required" (Stitch, GitHub, etc.)
- Run the server's init command: `npx @package/mcp-server init`
- Verify tokens haven't expired
- Check environment variables are set

#### "Rate limited" (free tier servers)
- Check free tier limits for the service
- Implement request batching where possible
- Consider upgrading to paid tier for heavy usage

### Diagnostic Commands
```bash
# Check .mcp.json is valid JSON
cat .mcp.json | python3 -m json.tool

# Verify npx can find the package
npx @playwright/mcp@latest --help

# Check environment variables
echo $GITHUB_PERSONAL_ACCESS_TOKEN

# List running MCP processes
ps aux | grep mcp
```

---

## Quick Reference Card

| Server | Package | Transport | One-Time Auth | Status |
|--------|---------|-----------|---------------|--------|
| Playwright | `@playwright/mcp@latest` | stdio | No | Active |
| Stitch | `@_davideast/stitch-mcp` | proxy | Yes (`init`) | Active |
| Supabase | `@supabase/mcp-server-supabase` | stdio | No (key in env) | Planned (S5W1) |
| GitHub | `@modelcontextprotocol/server-github` | stdio | No (PAT in env) | Planned (S5W1) |
| Context7 | `@context7/mcp-server` | stdio | No | Planned (S5W1) |
| Sequential Thinking | `@modelcontextprotocol/server-sequential-thinking` | stdio | No | Planned (S5W1) |
| Sentry | `@sentry/mcp-server` | stdio | No (token in env) | Planned (S5W2) |
| Grafana | `@grafana/mcp-server` | stdio | No (key in env) | Planned (S5W2) |
| Snyk | `@snyk/mcp-server` | stdio | No (token in env) | Planned (S5W2) |
| Upstash | `@upstash/mcp-server` | stdio | No (token in env) | Planned (S5W3) |
| Tavily | `@tavily/mcp-server` | stdio | No (key in env) | Planned (S5W3) |
