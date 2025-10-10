# Plugin Interfaces

This directory is reserved for plugin-related interfaces.

## Purpose
To allow for extending the parser's functionality without modifying the core library. This will enable third-party developers to add support for new log formats or complex parsing logic.

## Key Interfaces (Planned)
- `ICustomLogParser`: For complex, non-regex-based parsing logic.
- `ILogTypeAdapter`: To adapt new log types (e.g., `battery`, `power`) into the parsing pipeline.

Refer to `Docs/02_Architecture/PluginArchitecture.md` for the detailed design.

**This feature is not yet implemented.** The interfaces will be defined here when the plugin system development begins.
