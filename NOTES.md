# Developer Notes & Architecture Guide

## 1. How to Add New Free Providers
To add a new AI provider to the system:
1. **Update `ProviderType` Enum**: Add the provider identifier in `src/InfiniteLikeToken.Core/Models/ProviderType.cs`.
2. **Update Validation Logic**: In `KeyVaultService.cs`, add a new case in `BuildValidationRequest` pointing to the provider's `/models` endpoint and parsing routine in `ParseModelsFromResponse`.
3. **Update Config Builder**: In `LiteLlmConfigBuilder.cs`, add the corresponding model mappings inside `AppendDeploymentsForKey` for each model tier (`tier-pro`, `tier-flash`, `tier-lite`, `tier-rea`).

## 2. How `router_middleware.py` Scores & Routes Prompts
The custom LiteLLM pre-call logger (`InfiniteTaskClassifier` in `engine/router_middleware.py`) inspects incoming chat completion payloads:
- **REA Check**: If any tool name or prompt text matches reverse engineering keywords (`ghidra`, `ilspy`, `decompile`, `disassemble`, `apk`, `wasm`, `binary`), it routes to `tier-rea`.
- **Token & Complexity Check**: If the estimated prompt length exceeds 4,000 tokens or contains architectural keywords (`refactor whole project`, `memory leak`, `deadlock`), it routes to `tier-pro` (DeepSeek-R1 / Gemini Pro).
- **Lite Check**: If estimated tokens < 500 and matches quick edit keywords (`fix typo`, `syntax error`), it routes to `tier-lite`.
- **Default**: Defaults to `tier-flash` for high-throughput everyday coding.

## 3. Legal & Ethical Guidelines for REA Reverse Engineering
The REA Studio integration (`ReaMcpService.cs`) is designed for authorized security auditing, interoperability research, legacy source code recovery, and educational debugging of binaries you own or have explicit permission to inspect:
- **Respect Copyright**: Do not use decompilation tools to bypass copy protection, DRM, or software licensing restrictions unlawfully.
- **Sandboxed Execution**: All analysis commands run through safe static metadata parsing (PE headers, zip inspection, CIL reflection) without executing untrusted machine code directly on the host.
