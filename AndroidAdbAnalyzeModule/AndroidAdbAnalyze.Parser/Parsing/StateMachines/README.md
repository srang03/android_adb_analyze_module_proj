# State Machines for Parsing

This directory is reserved for state machine-based parsers.

## Purpose
Some complex log formats cannot be adequately parsed with simple regular expressions. State machines are useful for parsing logs where the meaning of a line depends on the lines that came before it.

## When to Use
- When parsing multi-line logs that represent a single logical event with state transitions.
- When dealing with nested or hierarchical log structures.
- When a simple regex becomes too complex and unmaintainable.

**This feature is not yet implemented.** When a suitable use case is identified, the relevant interfaces and implementations will be added here.
