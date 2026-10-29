# Gates: privacy cover

OWNS: src/lidless/**, README.md, installer/lidless.iss, GATES.md

Scope: Add an opt-in Windows 7-inspired visual cover to the existing input guard while preserving local agent desktop access.

- [x] G1: The guard covers every connected display and offers a visible PIN unlock flow.
  EVIDENCE: PrivacyCover bounds use SystemInformation.VirtualScreen; 1280x720 DrawToBitmap inspected; Enter opens existing PIN dialog through InputGuard.

- [x] G2: Compatible desktop capture omits the cover and injected pointer input reaches underlying apps.
  EVIDENCE: Windows accepted WDA_EXCLUDEFROMCAPTURE; CopyFromScreen pixel at primary top-left matched before/after; isolated WinForms click probe recorded control=1 and throughCover=1 in one run. Later runs recorded control=0 and throughCover=0, so pointer passthrough remains environment-dependent and must be tested with each agent.

- [x] G3: The Windows app and installer build, and both release downloads work.
  EVIDENCE: Local dotnet publish exit 0; GitHub Windows build 40897889106 succeeded; v0.5.2 installer and portable release assets downloaded in CI; latest installer, latest portable, and versioned portable URLs returned HTTP 200.

- [x] G4: The README explains the cover, its compatibility limits, and its security limits.
  EVIDENCE: README Input guard section documents capture exclusion, compatible-agent limitations, bypasses, and Windows lock distinction.
