# Interactive package deep dive

Audience: C# developers evaluating when this package adds value over built-in
language/runtime facilities. Deliverable: one offline HTML document, no CDN,
server, tracking or external runtime dependencies. Preserve the current RC API.

Approaches considered: a static article cannot provide the requested exploration;
a live .NET browser runtime adds deployment/tooling weight. Use browser BigInt
semantic models, explicitly labeled, with fixtures exported by the actual package.
This is an implementation choice under the user's instruction to proceed without
routine confirmations, not an additional approved feature contract.

Sections: honest capability comparison; arithmetic/policy explorer (including
small-integer promotions); exact conversion; generated bounded queue and codec;
annotated compile-time diagnostics; conceptual SIMD lane/tail view; adoption guide,
copyable C# snippets, measured evidence and qualification limits.

Verification: generate real C# fixture outcomes across all 12 storage types and
policies; validate browser models against every fixture; malformed inputs and
large 128-bit values must remain exact. Browser checks cover every demo, keyboard
controls, narrow/wide layout and console errors. Preserve reproducible source and
commands. This browser model does not execute Roslyn, .NET or SIMD instructions.

Implementation: docs/interactive-deep-dive.html; reusable fixture export in
eng/DeepDiveEvidence; semantic requirement in docs/requirements.sdoc; README link.
Copy the standalone deliverable into this chat's outputs directory. Commit and
push after verification, then observe CI. Runtime library behavior is unchanged.
