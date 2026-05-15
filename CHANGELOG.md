# Changelog

All notable changes to this project will be documented in this file.

The format is based on Keep a Changelog and this project aims to follow Semantic Versioning.

## [Unreleased]

### Added
- Public repository scaffolding:
  - `README.md`
  - `CONTRIBUTING.md`
  - `CODE_OF_CONDUCT.md`
  - `SECURITY.md`
  - `SUPPORT.md`
  - `THIRD_PARTY_NOTICES.md`
- GitHub templates and CI workflow:
  - `.github/ISSUE_TEMPLATE/bug_report.yml`
  - `.github/ISSUE_TEMPLATE/feature_request.yml`
  - `.github/pull_request_template.md`
  - `.github/workflows/ci.yml`
- Baseline test project `DispHub.Tests` with coverage for:
  - color temperature slider/kelvin/multiplier mapping
  - profile clamping/default values

### Changed
- Renamed project from DisplayHub to DispHub
- Upgraded from .NET 8 to .NET 10 (LTS)
- About page wording updated to DispHub messaging
- `DispHub.csproj` updated to exclude test project sources from main app compile
- Removed page preview image placeholders
- Removed custom smooth scrolling behavior (`SmoothScrollBehavior.cs`)
- Removed legacy root assets (`legacy-reference-image.png`, `kofi-promo.jpg`)

### Fixed
- Refactored color temperature mapping into reusable helper (`Helpers/ColorTemperatureMapper.cs`) so behavior can be tested and kept consistent
