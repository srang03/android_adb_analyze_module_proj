# Preprocessing Normalizers

This directory is reserved for additional value normalizer implementations.

## Purpose
While the `TimestampNormalizer` handles the crucial task of normalizing date and time formats, other fields may also require normalization. This directory will house classes responsible for converting varied data formats into a standardized, canonical form.

## Potential Implementations
- `PackageNameNormalizer`: To clean up and standardize application package names.
- `DeviceModelNormalizer`: To map various device model strings to a consistent name.
- `LogLevelNormalizer`: To map different log level indicators (e.g., "V", "Verbose", "DEBUG") to a standard enum.

**This is a placeholder for future extensions.** Currently, only timestamp normalization is implemented at the root of the `Preprocessing` directory.
