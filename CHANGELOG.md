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
- Baseline test project `DisplayHub.Tests` with coverage for:
  - color temperature slider/kelvin/multiplier mapping
  - profile clamping/default values

### Changed
- About page wording updated to DisplayHub messaging
- Legacy root asset renamed from `KeyedColors image.png` to `legacy-reference-image.png`
- `DisplayHub.csproj` updated to exclude test project sources from main app compile

### Fixed
- Refactored color temperature mapping into reusable helper (`Helpers/ColorTemperatureMapper.cs`) so behavior can be tested and kept consistent
- Finalized settings smooth scrolling by attaching wheel animation to the `NavigationView` content `ScrollViewer` and preventing nested scroll-viewer wheel conflicts
