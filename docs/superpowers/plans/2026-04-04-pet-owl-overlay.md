# Pet Owl Overlay Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the in-game pet overlay from a simple circle face into a readable owl-inspired companion that matches the approved concept art while preserving the existing pet state model.

**Architecture:** Keep the HTTP bridge and `PetOverlayViewModel` flow intact, but introduce a small, testable visual-spec mapping layer that converts `PetVisualState` into owl expression/pose data. Use that spec inside `PetBodyControl` so the production drawing logic stays localized to the overlay file and the tests can lock behavior without depending on Godot rendering APIs.

**Tech Stack:** C#/.NET 9, xUnit, Godot UI controls/drawing APIs

---

## Chunk 1: Visual Spec Mapping

### Task 1: Add a testable owl visual spec

**Files:**
- Modify: `McpMod.PetOverlay.cs`
- Test: `tests/STS2_MCP.Tests/PetOverlayViewModelTests.cs`

- [ ] **Step 1: Write failing tests for owl-specific state mapping**
- [ ] **Step 2: Run `dotnet test tests/STS2_MCP.Tests/STS2_MCP.Tests.csproj --filter PetOverlayViewModelTests` and confirm the new assertions fail**
- [ ] **Step 3: Add minimal pure C# visual-spec types and mapping helpers in `McpMod.PetOverlay.cs`**
- [ ] **Step 4: Re-run the same targeted test command and confirm the mapping tests pass**

## Chunk 2: Overlay Drawing Upgrade

### Task 2: Replace the placeholder blob with an owl-shaped draw routine

**Files:**
- Modify: `McpMod.PetOverlay.cs`
- Test: `tests/STS2_MCP.Tests/PetOverlayViewModelTests.cs`

- [ ] **Step 1: Add/adjust tests for view-model outputs that drive the owl drawing (face label visibility or equivalent state-derived data if needed)**
- [ ] **Step 2: Run the targeted tests and confirm failure if behavior changed**
- [ ] **Step 3: Update `PetOverlayController` and `PetBodyControl` to draw the owl head, body, wings, rune, eyes, beak, and state overlays**
- [ ] **Step 4: Re-run `dotnet test tests/STS2_MCP.Tests/STS2_MCP.Tests.csproj --filter PetOverlayViewModelTests` and confirm green**

## Chunk 3: Verification

### Task 3: Run the pet overlay regression suite

**Files:**
- Test: `tests/STS2_MCP.Tests/PetOverlayViewModelTests.cs`
- Test: `tests/STS2_MCP.Tests/PetStateStoreTests.cs`
- Test: `tests/STS2_MCP.Tests/PetBridgeServiceTests.cs`

- [ ] **Step 1: Run `dotnet test tests/STS2_MCP.Tests/STS2_MCP.Tests.csproj`**
- [ ] **Step 2: Confirm all pet overlay / bridge / state tests pass without new warnings or failures**
