# Two-hour RC1 qualification window — 2026-11-17

Starting implementation revision4df84e5b5394c98aa0fa1125ae12e744e152b61f.
User offered this computer for approximately two hours. Work starts about
2026-11-18T01:10:44Z (5:10 p.m. Pacific); finish foreground compute by
03:10:44Z (7:10 p.m. Pacific), with cleanup/review reserve. Do not change or
restart the independently running eighteen-hour integer qualification.

The numerical API implementation is complete for SN-018-FIXED/SN-019-DECIMAL;
whole RC1 still has integration, architecture and evidence limitations. Use the
window to improve relevant evidence, rather than treating elapsed time as work.

1. Add a reusable nonshipping CSharp/CsCheck expanded numeric campaign, covering
   owned fixed arithmetic against BigInteger and all three safe decimals against
   independent PeterO references. Reuse pinned verification dependencies. Retain
   reproducible seeds, minimized failures and explicit runtime/source provenance.
   Challenge finite, exponent-distributed, special/raw encodings and precision-loss
   behavior. Validate CLI negatives and a short real run before the longer run.
2. Run up to75 minutes, one worker, within the wall-clock reserve. Logs and bounded
   summary artifacts go to a unique verified E:/Artifacts run. Do not impose
   general memory caps. Observe actual completion before recording success.
3. In parallel, reuse/provision only the missing isolated NET11 x86 runtime and
   attempt meaningful32-bit execution. Keep outputs separate. Do not replace
   installed runtimes or claim Native AOT x86 support. Fix discovered defects with
   regression tests; otherwise report actual32-bit results or concrete blockers.
4. Inspect the outstanding SharpUnits integration requirement using actual package
   or source information; preserve unrelated checkouts. Only implement a bounded
   integration if evidence identifies a suitable supported API and the remaining
   time allows meaningful verification.
5. Review new harness/changes, run relevant coverage/negative/property checks and
   numerical checks on changed code. Record exact source hashes, versions, seeds,
   counts, failures and limits in StrictDoc/evidence. Commit/push verified milestones
   and verify CI. No release/tag/publication is implied; macOS remains disabled.

Stop early enough to leave a usable checkout and evidence if a step is blocked.
Any time extension requires a new user instruction; two hours is availability,
not permission to leave jobs running past the window.
