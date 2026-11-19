// Copyright (c) 2026 Jeff Dickson.
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;

// Tests check outward interval containment against independent exact references.
// No test hook or reflection-based dispatch is exposed in the public API.
[assembly: InternalsVisibleTo("SharpSafeNumerics.DecimalTests")]
