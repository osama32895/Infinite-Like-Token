"""
Infinite-Lite-Token Intelligent AI Routing Middleware for LiteLLM
Classifies incoming requests to 'infinite-auto' into optimal model tiers:
  - tier-rea: Reverse engineering, binary analysis, decompilation, disassembly
  - tier-pro: High-complexity architecture, large code context (>4000 tokens), deep debugging
  - tier-lite: Syntax checks, minor edits, quick comments (<500 tokens)
  - tier-flash: General everyday coding, refactoring, and conversation
"""

import re
from litellm.integrations.custom_logger import CustomLogger

REA_KEYWORDS = [
    "ghidra", "ilspy", "decompil", "disassembl", "pe header", "opcode",
    "bytecode", "cil", "msil", "smali", "apk", "wasm", "elf", "binary",
    "reverse engineer", "symbol table", "rva", "ida pro", "dnspy"
]

PRO_KEYWORDS = [
    "architecture", "refactor whole project", "system design", "memory leak",
    "race condition", "deadlock", "algorithm optimization", "deep reasoning",
    "formal verification", "concurrency", "distributed system"
]

LITE_KEYWORDS = [
    "fix typo", "syntax error", "add comment", "rename variable",
    "quick edit", "format json", "regex for", "docstring"
]


class InfiniteTaskClassifier(CustomLogger):
    def __init__(self):
        super().__init__()

    async def async_pre_call_hook(self, user_api_key_dict, cache, data, call_type):
        requested_model = data.get("model", "")

        if requested_model == "infinite-auto":
            routed_tier = self.classify_request(data)
            data["model"] = routed_tier
            # Pass custom routing header in metadata
            if "metadata" not in data:
                data["metadata"] = {}
            data["metadata"]["infinite_selected_tier"] = routed_tier
            print(f"[InfiniteLiteToken] Request classified -> routed to tier: {routed_tier}")

        return data

    def classify_request(self, data: dict) -> str:
        messages = data.get("messages", [])
        tools = data.get("tools", [])

        # 1. Check for REA tools or REA keywords in system/user messages
        tool_names = [t.get("function", {}).get("name", "").lower() for t in tools if isinstance(t, dict)]
        if any(any(kw in tname for kw in ["rea", "ilspy", "ghidra", "decompile", "disasm"]) for tname in tool_names):
            return "tier-rea"

        total_chars = 0
        full_prompt_text = []

        for msg in messages:
            content = msg.get("content", "")
            if isinstance(content, str):
                full_prompt_text.append(content)
                total_chars += len(content)
            elif isinstance(content, list):
                for part in content:
                    if isinstance(part, dict) and "text" in part:
                        full_prompt_text.append(part["text"])
                        total_chars += len(part["text"])

        combined_text = "\n".join(full_prompt_text).lower()
        estimated_tokens = total_chars // 4

        # Match keywords
        for kw in REA_KEYWORDS:
            if kw in combined_text:
                return "tier-rea"

        # Token count & Pro complexity checks
        if estimated_tokens > 4000:
            return "tier-pro"

        for kw in PRO_KEYWORDS:
            if kw in combined_text:
                return "tier-pro"

        # Lite checks
        if estimated_tokens < 500:
            for kw in LITE_KEYWORDS:
                if kw in combined_text:
                    return "tier-lite"
            # Very short query default
            if estimated_tokens < 150:
                return "tier-lite"

        # Balanced default
        return "tier-flash"


# LiteLLM hooks lookup instance
router_classifier = InfiniteTaskClassifier()
