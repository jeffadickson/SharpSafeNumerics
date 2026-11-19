; Unshipped analyzer release
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SSNG001 | SharpSafeNumerics.Generator | Error | Bounded integer declaration must be partial
SSNG002 | SharpSafeNumerics.Generator | Error | Storage type must be a supported built-in integer
SSNG003 | SharpSafeNumerics.Generator | Error | Decimal bounds must parse and fit the selected storage width
SSNG004 | SharpSafeNumerics.Generator | Error | Declaration shape or instance state is unsupported
SSNG005 | SharpSafeNumerics.Generator | Error | A member name overlaps the generated API or storage
SSNG006 | SharpSafeNumerics.Generator | Error | Annotation option is invalid
SSNG007 | SharpSafeNumerics.Generator | Error | A zero-valid default requires zero in the domain
SSNG008 | SharpSafeNumerics.Generator | Error | Encoding width must be supported and represent the full range
