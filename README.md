<div align="center">

# ⚡ Infinite-Lite-Token
### The Ultimate Open-Source Zero-Cost Multi-Agent AI Coding Hub & Universal Proxy

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
| [![.NET 8.0](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
| [![Python 3.11+](https://img.shields.io/badge/Python-3.11+-yellow.svg)](https://python.org)
| [![LiteLLM Proxy](https://img.shields.io/badge/LiteLLM-Proxy-orange.svg)](https://github.com/BerriAI/litellm)

*Bypass rate limits, aggregate dozens of free API keys (Google Gemini, Groq, Nvidia NIM, OpenRouter, Cerebras), run Claude Code / Codex CLI with zero cost, and reverse-engineer any binary with REA Studio.*

</div>

---

## 🌟 Overview

**Infinite-Lite-Token** combines the Free Claude Code (FCC) local bridge concept with LiteLLM's multi-key load-balancing proxy and a dynamic Task Complexity Classifier. It allows developers and non-technical users alike to leverage elite AI models (DeepSeek-R1, Llama 3.3 70B, Gemini 2.5 Pro/Flash) indefinitely across multiple free-tier provider accounts without paying a dime.

---

## 🏗️ Architecture

```ascii
┌────────────────────────────────────────────────────────────────────────┐
│                       Infinite-Lite-Token WPF UI                       │
│  (Setup Wizard | Key Vault | Monster Coding Chat | REA Studio)         │
└──────────────────────────────────┬─────────────────────────────────────┘
                                   │ IPC / HTTP (127.0.0.1:4000)
┌──────────────────────────────────▼─────────────────────────────────────┐
│                   Local LiteLLM Proxy & RouterDaemon                   │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │               router_middleware.py (Auto-Classifier)             │  │
│  └────────────────────────────────┬─────────────────────────────────┘  │
│                                   │ Fallback & Simple-Shuffle          │
│     ┌─────────────────────────────┼─────────────────────────────┐      │
│     ▼                             ▼                             ▼      │
│  [Tier: REA]              [Tier: Pro / Flash]             [Tier: Lite] │
│  (Qwen Coder, DS-R1)     (Gemini 2.5 Pro, Llama 70B)    (Flash-Lite, 8B)│
└─────┬─────────────────────────────┬─────────────────────────────┬──────┘
      │                             │                             │
      ▼                             ▼                             ▼
Google AI Studio             Groq / Nvidia NIM             OpenRouter / Cerebras
  (12+ Keys)                    (3+ Keys)                    (2+ Keys)
```

---

## ✨ Key Features

1. **Zero-Touch Dependency Bootstrapper (`RuntimeBootstrapper.cs`)**:
   - Automatically detects or downloads portable Python 3.11 and Node.js 22 into `%LocalAppData%/InfiniteLiteToken/runtime`.
   - Silently provisions `litellm[proxy]`, `uvicorn`, `mcp`, `@musistudio/claude-code-router`, `@anthropic-ai/claude-code`, `@openai/codex`, and `rea-agents`.
2. **Infinite Key Pool Vault (`KeyVaultService.cs`)**:
   - Stores API keys securely at rest using Windows DPAPI (`ProtectedData.Protect`).
   - Bulk Import modal allows pasting 10–15 keys at once (one per line).
   - Live dashboard monitors request counts, rate-limit cooldowns, and health statuses.
3. **Intelligent AI Routing Engine (`router_middleware.py`)**:
   - Intercepts requests for `infinite-auto` and analyzes prompt complexity, token count, and tool definitions.
   - Automatically routes REA tasks to specialized models (`tier-rea`), complex architecture to DeepSeek-R1 (`tier-pro`), and casual chats to flash/lite tiers.
4. **Monster Coding Chat & CLI Bridge (`BuiltInAgentService.cs`)**:
   - Built-in file tools (ReadFile, WriteFile, ApplyDiff, SearchFiles, RunSandboxedCommand).
   - Headless subprocess bridge for Claude Code and Codex CLI.
5. **REA Studio (Reverse Engineer Anything)**:
   - Drag-and-drop workspace for `.dll`, `.exe`, `.apk`, and `.wasm`.
   - 1-click action presets: *Explain Architecture*, *Reconstruct Source*, *Analyze Protocols*, and *Security Audit*.

---

## 🔑 Where to Get Free API Keys

| Provider | Free Tier Highlights | Get Your Free Key |
| :--- | :--- | :--- |
| **Google AI Studio** | Generous rate limits for Gemini 2.5 Pro & Flash | [Get Gemini Key ↗](https://aistudio.google.com/app/apikey) |
| **Groq** | Lightning-fast Llama 3.3 70B & DeepSeek R1 | [Get Groq Key ↗](https://console.groq.com/keys) |
| **Nvidia NIM** | Free trial credits for frontier models | [Get Nvidia NIM Key ↗](https://build.nvidia.com/) |
| **OpenRouter** | Access to various free open-source models | [Get OpenRouter Key ↗](https://openrouter.ai/keys) |
| **Cerebras** | High-throughput Llama inference | [Get Cerebras Key ↗](https://cloud.cerebras.ai/) |
| **GitHub Models** | Free playground keys for GPT-4o & DeepSeek | [Get GitHub Key ↗](https://github.com/marketplace/models) |

---

## 🚀 Quick Start

### Option A: Using `npx` (Node required)
```bash
npx infinite-like-token
```

### Option B: Download Standalone Windows Executable
1. Download `InfiniteLiteToken-win-x64.zip` from the Releases page.
2. Extract and run `InfiniteLiteToken.UI.exe`.
3. Navigate to **Setup & Runtime Wizard** and click **⚡ 1-Click Auto-Install & Repair Everything**.
4. Add your free API keys in **Infinite Key Pool**, and start coding in **Monster Coding Chat**!

---

## 📜 License

Distributed under the MIT License.
