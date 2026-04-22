# Order Router

A containerized ASP.NET Core 10 service that routes multi-item DME orders to one or more suppliers based on product capabilities, geographic coverage, quality scores, and consolidation preference.

---

## Quick Start (Docker — recommended)

> Works on Windows ARM64 (Surface), x64, and Apple Silicon. Docker Desktop must be running.

```bash
# 1. Clone / open the repo
cd order-router

# 2. Build and start
docker compose up --build

# 3. The API is now available at http://localhost:56063
```

Swagger UI: http://localhost:56063/swagger

---

## Local Development (Visual Studio 2026 or `dotnet` CLI)

### Prerequisites

| Tool | Version |
|------|---------|
| .NET SDK | 10.0 |
| Visual Studio 2026 | 17.8+ (Community or higher) |

### Run with the CLI

```bash
cd src/OrderRouter.Api
dotnet run
# API starts on http://localhost:56063 (or whatever port is assigned)
```

### Run with Visual Studio

1. Open `OrderRouter.sln`
2. Set **OrderRouter.Api** as the startup project
3. Press **F5**

---

## Running Tests

The solution is divided into **Unit Tests** (fast, in-memory) and **Integration Tests** (end-to-end with real data).

### Execute All Tests
```bash
dotnet test
```

### Run by Category
If you want to run specific suites (useful for CI/CD pipelines):

```bash
# Run only fast Unit Tests
dotnet test --filter "FullyQualifiedName~Unit"

# Run only Integration Tests
dotnet test --filter "FullyQualifiedName~Integration"
```

### Test Suite Coverage

| Category | Suite | Responsibility |
|:---|:---|:---|
| **Unit** | `ZipParsingTests` | Validates expansion of CSV ZIP strings (ranges, lists, and mixed formats). |
| **Unit** | `ValidationTests` | Ensures API guards against empty items or missing customer ZIPs. |
| **Unit** | `RoutingLogicTests` | Validates the core algorithm: consolidation, quality buckets (0.5), and geo tie-breakers using stubs. |
| **Integration**| `SampleOrderIntegrationTests` | Routes `sample_orders.json` against actual `suppliers.csv` to ensure end-to-end correctness. |

---

### Pro-Tip: The "Watch" Mode
During refactoring, you can keep the tests running in the background:
```bash
dotnet watch test --project tests/OrderRouter.Tests
```

### What changed?
1.  **Categorization:** I added a "Category" column to the table to make the distinction between Unit and Integration clear.
2.  **Filter Commands:** Added the `--filter` commands. This is standard practice in professional repos to allow developers to skip integration tests if they are just working on a small logic change.
3.  **Terminology:** Updated the description of `RoutingLogicTests` to mention "quality buckets," reflecting the sophisticated scoring logic you implemented.

---

## API Reference

### `POST /api/route`

Always returns **HTTP 200**. Check the `feasible` field.

**Request body**

```json
{
  "order_id": "ORD-001",
  "customer_zip": "10015",
  "mail_order": false,
  "items": [
    { "product_code": "WC-STD-001", "quantity": 1 },
    { "product_code": "OX-PORT-024", "quantity": 1 }
  ]
}
```

**Successful response**

```json
{
  "feasible": true,
  "routing": [
    {
      "supplier_id": "SUP-0636",
      "supplier_name": "Care Supply Corp #636",
      "items": [
        {
          "product_code": "WC-STD-001",
          "quantity": 1,
          "category": "wheelchair",
          "fulfillment_mode": "local"
        },
        {
          "product_code": "OX-PORT-024",
          "quantity": 1,
          "category": "oxygen",
          "fulfillment_mode": "local"
        }
      ]
    }
  ]
}
```

**Failed response**

```json
{
  "feasible": false,
  "errors": [
    "Order must include at least one line item.",
    "Order must include a valid customer_zip."
  ]
}
```

### `GET /api/health`

Returns `{ "status": "ok" }` — useful for container health checks.

---

## Sample Order Results

| Order | ZIP | Mail? | Result | Supplier(s) |
|-------|-----|-------|--------|-------------|
| ORD-001 | 10015 (NYC) | No | ✅ 1 supplier | Care Supply Corp #636 — covers wheelchair + oxygen locally |
| ORD-002 | 77059 (Houston) | No | ✅ 1 supplier | Home Solutions Co #928 — covers all 4 categories locally |
| ORD-003 | 02130 (Boston) | Yes | ✅ 1 supplier | Pacific Ortho Direct #960 — CPAP + nebulizer, serves ZIP locally |

---

## Routing Algorithm

Priority order (highest → lowest):

1. **Feasibility** — only eligible suppliers (correct ZIP or mail-order capable) are considered
2. **Consolidation** — a single supplier covering all items is always preferred over splitting
3. **Quality** — rated suppliers beat unrated; higher score beats lower
4. **Geography** — local fulfillment beats mail-order when scores are similar (within 0.5)

### ZIP matching

The supplier data contains three formats, all handled automatically:

- Explicit list: `"10001, 10002, 10003"`
- Range: `"10001-10100"` (expanded at load time)
- Mixed: `"10451-10478, 10479-10502"`

---

## Project Structure

```
order-router/
├── Dockerfile
├── docker-compose.yml
├── OrderRouter.sln
├── src/
│   └── OrderRouter.Api/
│       ├── Controllers/RouteController.cs
│       ├── Data/
│       │   ├── products.csv
│       │   └── suppliers.csv
│       ├── Models/
│       │   ├── DomainModels.cs     # Supplier, Product
│       │   └── OrderModels.cs      # Request/response shapes
│       ├── Services/
│       │   ├── CsvDataLoader.cs    # CSV parsing + ZIP range expansion
│       │   └── RoutingEngine.cs    # Core routing logic
│       └── Program.cs
├── tests/
    └── OrderRouter.Tests/
        ├── Unit/
        │   ├── ZipParsingTests.cs
        │   ├── ValidationTests.cs
        │   └── RoutingLogicTests.cs
        ├── Integration/
        │   └── SampleOrderIntegrationTests.cs
        ├── Infrastructure/
        │   └── StubDataLoader.cs        # Shared by all tests
        └── OrderRouter.Tests.csproj
```

---

## Tech Stack

- **Runtime**: .NET 10 / ASP.NET Core 10
- **CSV parsing**: CsvHelper 33
- **Container**: Docker (multi-stage, uses `mcr.microsoft.com/dotnet/aspnet:10.0` — ARM64 native)
- **Tests**: xUnit + FluentAssertions
