# FIRST Package Manager — Hosting & Download Infrastructure Proposal

## Goal

Downloads that work from as many school environments as possible and are fast.

## Current State — How WPILib Downloads Work Today

The current ecosystem uses a mix of sources:

| Component | Source | Speed | School Firewall Risk |
|-----------|--------|-------|---------------------|
| Maven artifacts (WPILib libs, GradleRIO) | frcmaven.wpi.edu (Artifactory) | Fast | **Low** — WPI domain |
| VS Code | update.code.visualstudio.com | Fast | Low — Microsoft CDN |
| VS Code extensions | GitHub Releases + open-vsx.org | Fast | **Medium** — GitHub blocked in some schools |
| JDK (Adoptium) | GitHub Releases | Fast | **Medium** — GitHub |
| AdvantageScope | GitHub Releases | Fast | **Medium** — GitHub |
| Elastic Dashboard | GitHub Releases | Fast | **Medium** — GitHub |
| Vendor JSONs | GitHub raw content | Fast | **Medium** — GitHub |
| Gradle wrapper | frcmaven.wpi.edu | Fast | **Low** — WPI domain |

**Key insight**: frcmaven.wpi.edu is already fast and rarely blocked (it's a `.edu` domain). Most download reliability issues come from GitHub being blocked by school firewalls.

## The Problem

The new package manager needs to fetch:
1. **Registry index** (installer-index.json) — currently on GitHub
2. **Package manifests** — currently on GitHub  
3. **Package artifacts** (JDK, VS Code, tools, vendor libs) — various sources

Schools that block GitHub (or rate-limit it) will have trouble with #1, #2, and any artifact hosted on GitHub Releases.

## Proposal — Three Tiers of WPI Support

### Tier 1: Minimal (Registry Mirror Only)

**What WPI provides**: Host the `installer-index.json` and package manifest JSONs on frcmaven.wpi.edu alongside existing vendordep data.

**Effort**: Low — add a directory to existing Artifactory instance, CI job pushes from vendor-json-repo on merge.

**What this solves**: Registry discovery works even when GitHub is blocked. Package artifacts still download from their original sources (GitHub Releases, Microsoft CDN, vendor Maven repos).

**School firewall impact**: 
- Registry: ✅ Always works (WPI domain)
- JDK, AdvantageScope, Elastic, extensions: Still from GitHub (may be blocked)
- VS Code: Microsoft CDN (works)
- Maven artifacts: frcmaven.wpi.edu (works)

**Implementation**:
```
frcmaven.wpi.edu/artifactory/installer/
  installer-index.json           # Registry index
  packages/wpilib/jdk-2026.json  # Individual manifests
  bundles/frc-java-starter.json  # Bundle definitions
```
CI pipeline: On vendor-json-repo merge → push to Artifactory via REST API.

---

### Tier 2: Moderate (Registry + Artifact Proxy)

**What WPI provides**: Everything in Tier 1, plus proxy/cache WPILib-owned artifacts through frcmaven.wpi.edu.

**Effort**: Medium — set up Artifactory remote repositories that proxy GitHub Releases for WPILib-owned tools.

**What this solves**: All WPILib-controlled downloads go through frcmaven.wpi.edu. Only third-party vendor artifacts (CTRE Phoenix Tuner, REV Hardware Client) still come from external sources.

**School firewall impact**:
- Registry: ✅ Works
- JDK, AdvantageScope, Elastic, VS Code extensions: ✅ Works (proxied through WPI)
- VS Code itself: ✅ Works (Microsoft CDN)
- WPILib Maven artifacts: ✅ Works (already on frcmaven)
- Vendor Maven artifacts: ✅ Works (many already proxied via frcmaven)
- Vendor standalone tools (Phoenix Tuner, REV HW Client): ⚠️ Still from vendor servers

**Implementation**:
```
frcmaven.wpi.edu/artifactory/installer/
  installer-index.json
  packages/...
  bundles/...
  
frcmaven.wpi.edu/artifactory/installer-artifacts/
  wpilib/jdk/17.0.16+8/
    OpenJDK17U-jdk_x64_windows_hotspot_17.0.16_8.zip
  wpilib/advantagescope/26.0.0/
    advantagescope-26.0.0-win-x64.zip
  wpilib/elastic/2026.1.1/
    elastic-2026.1.1-win-x64.zip
```
Artifactory "Remote Repository" type — proxies and caches from GitHub Releases. First request fetches from GitHub, subsequent requests served from cache.

---

### Tier 3: Full (Single-Domain Experience)

**What WPI provides**: Everything goes through frcmaven.wpi.edu. All artifacts mirrored, including vendor tools.

**Effort**: Higher — need vendor cooperation to allow artifact mirroring, plus storage costs for ~2-5 GB of artifacts per season per platform.

**What this solves**: Every download from a single `.edu` domain. Maximum school firewall compatibility. Potentially faster (Artifactory has built-in CDN support).

**School firewall impact**: ✅ Everything works from a single domain.

**Implementation**: Same as Tier 2, plus:
- Vendor coordination: CTRE, REV, PathPlanner, etc. grant permission to mirror
- Or vendors push their artifacts to frcmaven directly (some already do for Maven JARs)
- Storage: ~5 GB per platform × 5 platforms = ~25 GB per season (manageable)

**Additional option**: CloudFlare or AWS CloudFront CDN in front of frcmaven for global edge caching.

---

## Recommendation

**Start with Tier 1 now** (minimal effort, solves the registry problem), **plan for Tier 2 by Championships**.

Tier 1 can be set up in an afternoon — it's just copying JSON files to an Artifactory directory with a CI job. The package manager already has fallback URL support, so it tries frcmaven first and falls back to GitHub.

Tier 2 is the sweet spot for 2027 season. Artifactory's remote repository feature means WPI doesn't need to manually manage artifacts — Artifactory proxies and caches automatically.

Tier 3 is nice-to-have but requires vendor buy-in and more storage. Could be a 2028 goal.

## What the Package Manager Already Has

The `RegistryClient` currently tries multiple URLs in order:
1. `frcmaven.wpi.edu/...` (primary)
2. `github.com/jasondaming/vendor-json-repo/...` (fallback)

It remembers which URL last succeeded and tries it first next time. If all URLs fail, it uses the local cache. This means **Tier 1 works today** — we just need the files hosted on frcmaven.

## Storage & Bandwidth Estimates

| What | Size | Frequency |
|------|------|-----------|
| Registry index + manifests + bundles | ~500 KB | Updated per-release (~monthly) |
| WPILib artifacts (Tier 2) | ~3 GB per platform | Per-season (annual) |
| All artifacts including vendors (Tier 3) | ~8 GB per platform | Per-season + mid-season updates |
| Bandwidth per team install | ~500 MB - 2 GB | ~5,000 teams × 1-3 installs/season |

Estimated peak bandwidth: ~5-15 TB per season. frcmaven already handles this scale for Maven artifact resolution.

## Questions for WPI

1. Can we add an `installer/` directory to the existing Artifactory instance? (Tier 1)
2. Is there capacity for proxying GitHub Release artifacts? (Tier 2)  
3. What's the current Artifactory storage/bandwidth capacity?
4. Is there interest in CloudFlare/CDN fronting?
5. Who has admin access to configure this?
