# Diagnostic System Migration Plan

## Phase 1: Core Infrastructure ✅
- [x] Create diagnostic base system
  - [x] Create #file:Descriptors.cs class with nested categories
  - [x] Implement IDiagnosticReporter interface
  - [x] Create DiagnosticReporter base class
- [x] Setup analyzer patterns
  - [x] Convert AutoNotifyAnalyzer
  - [x] Convert AutoCommandAnalyzer
  - [x] Document analyzer pattern

## Phase 2: Generator Integration 🚧
- [x] Standardize diagnostic reporting
  - [x] Move descriptors to appropriate categories in #file:Descriptors.cs
  - [x] Inject IDiagnosticReporter into generators
- [x] Update generator error handling
  - [x] Add diagnostic support to base generator

## Phase 3: Generator Updates
- [ ] Property Generation
  - [x] Move descriptors to #file:Descriptors.cs Descriptors.Generator.AutoNotify
  - [ ] Update validation pipeline
  - [ ] Implement error recovery
- [ ] Command Generation
  - [x] Move descriptors to #file:Descriptors.cs Descriptors.Generator.AutoCommand
  - [ ] Update validation pipeline
  - [ ] Implement error recovery

## Phase 4: Documentation
- [ ] Review and consolidate diagnostic categories
- [ ] Update diagnostic docs
  - [x] Document error codes
  - [ ] Add troubleshooting guide
  - [ ] Document recovery strategies
  - [ ] Document diagnostic reporting patterns
- [ ] Add implementation guides
  - [ ] Diagnostic reporter usage
  - [ ] Error recovery patterns
  - [ ] Validation examples

## Implementation Notes
- Use Descriptors.Category.Attribute pattern for creating DiagnosticDescriptors in the project
- Report through IDiagnosticReporter
- Add error recovery for critical failures
- Batch diagnostics where possible
- Keep existing diagnostic IDs
- Follow analyzer patterns
- Do not talk about tests
