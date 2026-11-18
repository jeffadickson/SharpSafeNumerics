; Unshipped analyzer release
### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|------
SSN1001 | SharpSafeNumerics.Safety | Error | Reject unsafe built-in integer narrowing in StrictNumeric scopes
SSN1002 | SharpSafeNumerics.Safety | Error | Reject unchecked integer arithmetic and explicit unchecked overrides inside StrictNumeric scopes
SSN1003 | SharpSafeNumerics.Safety | Warning | Report discarded NumericResult, DecimalResult and IeeeDecimalResult values
SSN1004 | SharpSafeNumerics.Safety | Error | Reject ArithmeticPolicy.Throw inside StrictNumeric scopes
SSN1005 | SharpSafeNumerics.Safety | Error | Reject raw generic math saturation and truncation inside StrictNumeric scopes
SSN1006 | SharpSafeNumerics.Safety | Error | Reject detectable bounded domain range and status bypasses in StrictNumeric scopes
SSN1007 | SharpSafeNumerics.Safety | Warning | Flag evident managed allocation sites in StrictNumeric scopes
SSN1008 | SharpSafeNumerics.Safety | Error | Reject invalid bounded domain default construction in StrictNumeric scopes
