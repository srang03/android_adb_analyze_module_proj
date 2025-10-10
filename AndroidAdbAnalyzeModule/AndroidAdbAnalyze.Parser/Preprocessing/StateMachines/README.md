# State Machines for Preprocessing

This directory is reserved for state machine-based preprocessing logic.

## Purpose
Some preprocessing tasks may require maintaining state across multiple log entries. For example, inferring a missing value in one log entry based on a previous entry.

## When to Use
- When a value needs to be inferred or calculated based on a sequence of events.
- For correcting or enriching data that requires contextual information from preceding logs.

**This feature is not yet implemented.** When a suitable use case is identified, the relevant implementations will be added here.
