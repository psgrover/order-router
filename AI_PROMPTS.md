# AI Prompts

Significant prompts used during development, in order.

---

## 1. Initial Problem Analysis
**Prompt:**
> I'm given three files: `suppliers.csv` (1,100 rows, messy ZIP formats), `products.csv` (1,200 rows), and `sample_orders.json`. Build a containerized order-routing service in C#/.NET 10 on a Surface ARM device. The service must POST to `/api/route` and use routing priorities: feasibility → consolidation → quality → geography.

**Purpose:** Established the full scope. Surfaced key constraints: ARM64 target, messy data (ZIP ranges, typo `suplier_name`), and the business rule that mail-order is conditional.

---

## 2. ZIP Range Expansion Design
**Prompt:**
> The `service_zips` field contains values like `"00100-99999"`, `"10451-10478, 10479-10502"`, and `"11410"`. Write a C# method that expands these into a `HashSet<string>`. A segment is a range only if both sides have equal length to avoid misidentifying partial data as ranges.

**Purpose:** Solved the messy data problem. The equal-length guard prevents logic errors when parsing the mixed-format CSV strings.

---

## 3. Routing Priority Algorithm & Similar Ratings
**Prompt:**
> Implement the routing engine. Priority: (1) Feasibility (Zip/Mail eligibility); (2) Consolidation (Single supplier > Multi); (3) Quality (Rated > Unrated); (4) Geography (Local > Mail). Treat ratings within 0.5 points as "similar" so that geography acts as a tie-breaker for high-quality suppliers.

**Purpose:** This prompt refined the ranking logic. The "bucketing" logic ensures that a 9.1 rating doesn't automatically beat a 9.0 local supplier if they are effectively "similar" in quality.

---

## 4. Multi-stage Dockerfile for ARM64
**Prompt:**
> Write a Dockerfile for a .NET 10 Web API. Ensure it uses `mcr.microsoft.com/dotnet/sdk:10.0` and handles the `obj` folder correctly to avoid `MSB4018` errors when building on a Surface ARM host. Use `--platform=$BUILDPLATFORM` for performance.

**Purpose:** Addressed the specific build failures encountered on Windows ARM. Included a `.dockerignore` strategy to prevent local Windows binaries from poisoning the Linux container build.

---

## 5. Test Design for Edge Cases
**Prompt:**
> Write xUnit tests using FluentAssertions and a stub `IDataLoader`. Cover: single-supplier consolidation, multi-supplier split, higher score preferred, local preferred for similar scores, mail-order flags, and unknown product codes.

**Purpose:** Ensured 100% logic coverage without needing the physical CSV files. The "Generalist vs Specialists" test confirmed the consolidation rule works as intended.

---

## 6. Integration Testing
**Prompt:**
> Add integration tests that load real data from the `/Data` folder. Validate that the service correctly routes the three provided sample orders from `sample_orders.json`.

**Purpose:** Verified the end-to-end pipeline. Confirmed that local path resolution works within the test runner, ensuring the CSV loader can find the production data files.

---

## 7. Test Suite Refactoring
**Prompt:**
> Refactor the existing test project to separate unit tests from integration tests. Move the logic validation into a `Unit/` folder and the sample order verification into an `Integration/` folder. Extract the `StubDataLoader` into an `Infrastructure/` directory to share it across the suite without duplication.

**Purpose:** Transitioned the test architecture from a single-file "proof of concept" to a professional, scalable structure. This separation allows for faster CI/CD cycles by enabling the execution of lightweight logic tests independently from heavier, file-dependent integration tests.
