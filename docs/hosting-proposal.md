# FIRST Package Manager — Hosting & Download Infrastructure Proposal

## Goal

Downloads that work from as many school environments as possible and are fast.

## Current State — Why Current Downloads Are Fast

| Component | Source | Why It's Fast |
|-----------|--------|--------------|
| WPILib installer ISOs | **Cloudflare CDN** | Global edge caching |
| JDK, AdvantageScope, Elastic, extensions | **GitHub Releases** | GitHub's own CDN (Fastly) |
| VS Code | **Microsoft CDN** | Azure CDN, global |
| Maven artifacts | **frcmaven.wpi.edu** | Fine for small JARs/POMs |

**Key insight from Peter**: frcmaven.wpi.edu had slow download reports when tested for large binary distribution (beta ISOs). It's fine for JSON/small files, but large binaries need CDN distribution. WPILib already moved main downloads to Cloudflare for this reason.

**Austin is reaching out to JFrog** about boosting Artifactory speeds or using their CDN.

## The Two Problems

1. **School firewalls** block GitHub → JDK, tools, vendor JSON downloads fail
2. **Speed** → frcmaven can't handle large binary distribution at scale

These require different solutions:
- **Firewall problem** → need alternative domains that aren't blocked
- **Speed problem** → need CDN, not just a different server

## Revised Proposal — Registry + CDN Strategy

### Layer 1: Registry (small JSON files) — frcmaven.wpi.edu

**What**: Host `installer-index.json`, package manifests, and bundle definitions on frcmaven.

**Why**: Small files (<1 MB total), frcmaven handles this fine, `.edu` domain bypasses firewalls.

**Effort**: Low — CI job copies JSON files to Artifactory on merge.

```
frcmaven.wpi.edu/artifactory/installer/
  installer-index.json
  packages/wpilib/jdk-2026.json
  bundles/frc-java-starter.json
```

### Layer 2: Artifacts (large binaries) — CDN with fallback chain

**What**: Each package manifest lists multiple download URLs. The package manager tries them in order until one works.

**Strategy**: Put the fastest/most-accessible CDN first, fall back to others.

```json
{
  "url": "https://cdn.wpilib.org/packages/jdk/17.0.16/OpenJDK17U-jdk_x64_windows.zip",
  "mirrors": [
    "https://frcmaven.wpi.edu/artifactory/installer-artifacts/jdk/17.0.16/OpenJDK17U-jdk_x64_windows.zip",
    "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.16%2B8/OpenJDK17U-jdk_x64_windows_hotspot_17.0.16_8.zip"
  ]
}
```

**CDN options** (not mutually exclusive — can use multiple):

| Option | Cost | Speed | Firewall Bypass | Effort |
|--------|------|-------|-----------------|--------|
| **Cloudflare (existing)** | Free tier or current plan | Excellent | Good — cloudflare domains rarely blocked | Low — WPILib already uses it |
| **JFrog CDN** | Free if JFrog agrees (Austin outreach) | Good | Good — JFrog domains | Depends on JFrog response |
| **GitHub Releases** (current) | Free | Good | Medium — blocked in some schools | Already working |
| **Cloudflare R2** | Very cheap ($0.015/GB) | Excellent | Good | Medium — new setup |
| **AWS CloudFront + S3** | ~$0.085/GB | Excellent | Good | Medium — new setup |

### Layer 3: The Fallback Chain in the Package Manager

The package manager already supports this. `RegistryClient` tries multiple URLs. `DownloadManager` supports `mirrors[]`. What we need:

1. **Primary URL**: CDN (Cloudflare or JFrog) — fastest, works most places
2. **Mirror 1**: frcmaven.wpi.edu — `.edu` domain, good for firewalled schools
3. **Mirror 2**: GitHub Releases — works everywhere GitHub isn't blocked
4. **Local cache**: If all fail, use previously downloaded version

The package manager tries #1, falls to #2 if it fails, falls to #3, then local cache. First successful URL is remembered for future downloads in that session.

## Recommended Path

### Now (pre-Championships demo)
- **Registry on frcmaven**: Just the JSON files. CI pushes on merge. ✅ Already works with our fallback code.
- **Artifacts from existing sources**: GitHub Releases + original URLs. Works for demo.

### For 2027 Beta
- **CDN for artifacts**: Depends on JFrog response and Cloudflare capacity.
  - If JFrog CDN: artifacts served from JFrog edge network
  - If Cloudflare: upload artifacts to Cloudflare R2 or Pages, CI pushes on release
  - Either way: `cdn.wpilib.org` CNAME pointing to the CDN
- **Manifest URLs updated**: Primary URL points to CDN, mirrors include frcmaven + GitHub

### For 2027 Season
- **Custom domain**: `cdn.wpilib.org` or `packages.wpilib.org` — CNAME to whatever CDN
- **All WPILib artifacts on CDN**: JDK, VS Code extensions, tools, AdvantageScope, Elastic
- **Vendor artifacts**: Vendors keep their own hosting, but can opt-in to CDN mirroring

## Storage & Bandwidth

| What | Size per Season |
|------|----------------|
| Registry JSON files | ~500 KB |
| WPILib artifacts (all platforms) | ~3 GB |
| + Vendor tools (Phoenix Tuner, REV HW Client) | ~1.5 GB |
| **Total** | **~5 GB** |

Bandwidth estimate: ~5,000 teams × ~1 GB average install = **~5 TB per season peak**

Cloudflare R2 cost for this: ~$75/season (negligible)
Cloudflare free tier: May cover it entirely if under bandwidth limits

## Questions

1. **Austin/JFrog**: What did JFrog say? Free CDN tier available?
2. **Cloudflare**: Is the existing Cloudflare account/plan sufficient for artifact hosting, or just the installer ISO?
3. **Custom domain**: Can we get `cdn.wpilib.org` or `packages.wpilib.org` as a CNAME?
4. **CI pipeline**: Who manages the "push to CDN on release" automation?

## What's Already Built

The package manager has:
- ✅ Multiple fallback URLs (tries frcmaven → GitHub → local cache)
- ✅ `mirrors[]` in package manifest schema
- ✅ SHA-256 verification on all downloads
- ✅ HTTP resume for interrupted downloads
- ✅ Remembers last successful URL per session
