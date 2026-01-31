---
stepsCompleted: [1, 2, 3, 4, 5, 6]
status: complete
inputDocuments:
  - README.md
date: 2026-01-31
author: Jerome
---

# Product Brief: d365fo-data-migration

<!-- Content will be appended sequentially through collaborative workflow steps -->

## Executive Summary

d365fo-data-migration is a .NET 8 command-line tool that transforms D365FO data migration from a weeks-long manual burden into an unattended overnight operation. In a typical D365FO implementation, migrating 100+ entities through the Data Management Framework UI takes a full week of skilled consultant time -- and that migration must be repeated 5 to 10 times across mock runs, testing cycles, and cutover rehearsals. This tool automates the entire pipeline -- SQL extraction, XML transformation, dependency-ordered packaging, and direct API-based import -- so migrations run unattended on nights and weekends while the team focuses on higher-value work. Deployed across multiple D365FO implementations, it serves as a proprietary practice accelerator that no competing consultancy has, recovering weeks of consultant capacity per engagement and enabling more competitive project timelines and pricing.

---

## Core Vision

### Problem Statement

Microsoft Dynamics 365 Finance and Operations' Data Management Framework is designed for interactive, single-entity operations -- not bulk data migration. Yet every D365FO implementation project requires exactly that: migrating large volumes of data across dozens to hundreds of entities, each with strict dependency ordering. The DMF provides no built-in way to automate this at scale, forcing integration teams into a manual workflow of uploading, monitoring, and verifying each entity one at a time through the UI. A single migration cycle takes approximately one week of dedicated consultant effort, and projects typically require 5 to 10 full cycles across mock migrations, testing, and cutover rehearsals.

### Problem Impact

- **Massive time sink:** Each manual migration cycle consumes approximately one week of skilled consultant time. Repeated 5-10 times per project, that's 5-10 weeks of senior consultant capacity spent on repetitive clicking and monitoring -- potentially hundreds of hours per engagement.
- **Critical path blocker:** Manual migrations must run during business hours, competing for calendar time with configuration, testing, and UAT. Migration becomes a scheduling bottleneck that extends project timelines.
- **Costly failures:** Manually managing dependency order across 100+ entities is error-prone. A single out-of-order import can cascade into failures that require hours of diagnosis and rework, potentially invalidating an entire week's migration run.
- **Misallocated expertise:** Senior D365FO consultants -- whose value lies in system configuration, business process design, and testing -- are instead trapped babysitting data uploads. Junior team members assigned to the task face a steep learning curve in entity dependencies.
- **No parallelization:** The manual process is inherently serial. One person, one entity at a time. There is no way to scale the effort horizontally.

### Why Existing Solutions Fall Short

No tooling exists that automates end-to-end D365FO data migration from SQL Server sources. The industry has collectively accepted the manual DMF workflow as "just how D365FO works." Microsoft's DMF was built for ongoing data management operations, not project-phase bulk migration -- and no third-party ISV has addressed this gap. Teams build internal checklists and Excel trackers to manage migration order and progress, but these are manual coordination aids, not automation. The real competitor is the status quo: budgeting weeks of consultant time per project as unavoidable cost.

### Proposed Solution

A command-line tool that replaces the entire manual migration workflow with an automated, unattended pipeline:
1. **Extract** data from SQL Server using configurable queries per entity
2. **Transform** results into D365FO-compatible XML format with proper manifest and package header files
3. **Package** data into ZIP archives ready for D365FO import
4. **Resolve** entity dependencies automatically via topological sorting, eliminating the most common source of import failures
5. **Import** packages directly into D365FO via the Data Management REST API
6. **Scale** through parallel processing and automatic file splitting for large datasets

Configure once, kick off on a Friday evening, and review results Monday morning. Migration is removed from the critical path entirely.

### Key Differentiators

- **Repeatable by design:** The same configuration runs identically across 5-10 migration cycles per project -- nights, weekends, unattended. What took a week of manual effort per cycle now runs as an overnight batch, recovering 5-10 weeks of consultant time per engagement.
- **Zero human intervention:** Once configured, the entire migration runs without clicking, monitoring, or manual verification. The team goes home; the migration runs.
- **Automatic dependency resolution:** Topological sorting ensures entities import in the correct order, eliminating the most painful and time-consuming class of migration failures.
- **Off the critical path:** Migrations run outside business hours, freeing the project schedule for configuration, testing, and UAT during the workday.
- **Unchallenged in the market:** No other tool or consultancy capability provides end-to-end automated D365FO data migration from SQL Server sources.
- **Enterprise scale:** Parallel processing and large dataset splitting handle 100+ entities efficiently, turning serial manual work into a parallelized batch operation.

### Strategic Value

- **Per-project savings:** 5-10 weeks of senior consultant time recovered per D365FO implementation, representing a significant reduction in project cost.
- **Cross-engagement compounding:** Reused across every D365FO implementation, the savings multiply with each new engagement. The tool is a practice asset, not a project artifact.
- **Competitive positioning:** Competing consultancies carry 5-10 weeks of manual migration in their project estimates. Teams using this tool can offer lower bids, faster timelines, and more predictable outcomes.
- **Quality consistency:** Automated runs produce identical results every time, eliminating the human error variance inherent in manual migration cycles.

## Target Users

### Primary Users

#### Technical Consultant / Developer -- "The Migration Engineer"

**Role:** Configures, operates, and iteratively refines the migration tool across D365FO implementation projects.

**Profile:** A technical consultant or developer with SQL expertise and deep knowledge of D365FO entity structures. Responsible for writing extraction queries, defining entity dependencies, configuring connections, and managing the end-to-end migration pipeline. Typically the person who sets up the tool at project start and triggers migration runs on nights and weekends. Between cycles, they refine SQL queries and entity mappings based on feedback from the functional consultant -- adjusting transformations, adding filters, and tuning the configuration as data quality issues are identified.

**Problem Experience:** Before the tool, this person was either manually uploading entities through DMF themselves or coordinating with others to do so. Their expertise in entity structures and dependencies was consumed by repetitive mechanical work rather than solving technical challenges. They also carry critical configuration knowledge -- if they leave the project, the migration setup has to be rebuilt.

**Success Vision:** Configure the migration once, trigger it Friday evening, review clean results Monday morning. Between cycles, their work is focused on meaningful improvements -- refining queries based on data quality feedback, not clicking through DMF. When results arrive, they can quickly triage: technical failures (their problem) vs. data quality issues (hand off to the functional consultant).

---

#### Functional Consultant -- "The Data Quality Analyst"

**Role:** Interprets migration results through the lens of D365FO business rules and translates data issues into actionable reports for the source system team.

**Profile:** A D365FO functional consultant with deep understanding of business processes and data relationships. After each migration cycle, they don't just read error logs -- they interpret *why* data was rejected or would cause downstream issues, cross-referencing migration results against D365FO business logic. A customer record might import successfully but create problems in sales orders; this consultant catches those issues. They translate technical findings into business language the source system team understands, explaining not just what failed but what needs to change and why.

**Problem Experience:** When migrations took a full week of manual effort, the feedback loop was painfully slow. They could only analyze results once per week, and manually comparing errors between cycles consumed hours. Under constant pressure from the project director asking "are we ready?", they needed faster, more frequent data to give confident answers.

**Success Vision:** Migration cycles complete overnight, delivering fresh results every morning. They can quickly identify what's new since the last cycle vs. known issues awaiting source team fixes. The feedback loop with the source system team tightens from weekly to daily, accelerating data quality convergence and giving them confidence to report progress accurately.

---

#### Project Director -- "The Decision Maker"

**Role:** Monitors data quality progress across migration cycles and makes the go/no-go decision for cutover.

**Profile:** A senior project leader responsible for the overall D365FO implementation. Needs a risk-based view of migration readiness -- not just error counts, but which entities are still problematic, whether remaining issues are blockers or acceptable for go-live, and whether the trend supports the planned cutover date. Ultimately the person who stands in a steering committee and says "yes, we're ready" or "we need another cycle."

**Problem Experience:** With manual week-long migration cycles, progress visibility was sparse -- perhaps one status update every 1-2 weeks. Making confident go/no-go decisions was difficult with stale data. When asked "will we make the cutover date?" in a steering committee, they lacked the evidence to answer with confidence.

**Success Vision:** With frequent overnight migrations, data quality trends are visible and current. They can see clear convergence -- errors dropping cycle over cycle -- and make evidence-based go/no-go decisions. When stakeholders ask about readiness, they answer with data, not guesswork.

### Secondary Users

#### Source System Team (Client-Side)

**Role:** Owns the legacy data and is responsible for correcting data quality issues identified during migration cycles.

**Interaction:** Does not use the tool directly. Receives error reports from the functional consultant detailing data issues found after each migration run -- written in business terms they understand, not technical jargon. Corrects data at the source so subsequent migration cycles show improved quality. Benefits significantly from faster migration cycles: instead of waiting a week between feedback rounds, they can receive daily updates and fix issues iteratively.

### User Journey

The migration process is an iterative feedback loop, not a linear sequence:

1. **Setup:** The technical consultant configures the tool at project start -- SQL queries, entity definitions, dependencies, D365FO connection. This is a one-time investment per project, with refinements between cycles.
2. **Migration Cycle:** The technical consultant triggers a run (typically overnight or over a weekend). The tool executes the full pipeline unattended.
3. **Triage:** The technical consultant reviews results the next morning. Technical failures (timeouts, auth issues, XML errors) are resolved directly. Data quality issues are handed off to the functional consultant.
4. **Analysis:** The functional consultant interprets data errors against D365FO business rules, identifies root causes, and prepares reports in business language for the source system team.
5. **Feedback:** The source system team corrects data at the source. The technical consultant adjusts SQL queries or mappings if transformation-level fixes are needed.
6. **Repeat:** Steps 2-5 repeat 5-10 times per project. Each cycle shows improved data quality as issues are progressively resolved.
7. **Progress Review:** The project director monitors error trends across cycles, assessing convergence toward go-live readiness.
8. **Go/No-Go:** Based on cumulative migration results, the project director makes the cutover decision with evidence-based confidence.

## Success Metrics

### User Success Metrics

#### Migration Engineer
- **Clean technical execution:** All entities import without errors in a single unattended run. Warnings are acceptable -- they indicate data quality issues for the functional consultant, not tool failures.
- **Reliable dependency ordering:** Zero import failures caused by incorrect entity sequencing. The topological sort handles all dependency resolution without manual intervention.
- **Configuration stability:** Once configured for a project, the tool runs consistently across cycles without requiring reconfiguration (only SQL query refinements based on data quality feedback).

#### Functional Consultant
- **Accelerated feedback loop:** Migration results available the morning after each run, enabling daily data quality analysis instead of weekly.
- **Progressive data quality convergence:** Error counts decrease measurably cycle over cycle as source data corrections take effect.
- **Clean run by cycle 5:** Data quality issues resolved to the point where a migration completes with warnings only (no blocking errors) within 5 migration cycles.

#### Project Director
- **Confident go/no-go:** Migration trend data supports an evidence-based cutover decision -- visible convergence toward clean runs across cycles.
- **On-schedule cutover:** Migration does not delay the project cutover date. Off-hours execution keeps migration off the critical path.

### Business Objectives

- **Consultant capacity recovery:** Eliminate 5-10 weeks of manual migration effort per D365FO implementation, freeing senior consultants for configuration, testing, and business process work.
- **Practice-wide reuse:** Tool deployed and reused across every D365FO implementation engagement without project-specific rebuilds.
- **Competitive delivery advantage:** D365FO projects delivered with shorter timelines and lower migration costs than competing consultancies using manual processes.

### Key Performance Indicators

| KPI | Target | Measurement |
|-----|--------|-------------|
| Technical migration success | All entities imported per run | Zero entity-level import errors (warnings acceptable) |
| Data quality convergence | Clean run by cycle 5 | Migration cycle where zero blocking errors achieved |
| Migration execution | Completes unattended overnight/weekend | No human intervention required during run |
| Practice adoption | Used on every D365FO engagement | Number of implementations using the tool |

## MVP Scope

### Core Features (Implemented)

The core feature set is complete and production-proven across multiple D365FO implementations:

1. **SQL Data Extraction:** Configurable SQL queries per entity, executed against source SQL Server databases. Supports per-entity connection string overrides for multi-source scenarios.
2. **XML Transformation:** Generates D365FO-compatible XML output files with proper structure for the Data Management Framework.
3. **Data Package Generation:** Creates complete ZIP packages including manifest and package header files, ready for D365FO import.
4. **Automatic Dependency Resolution:** Topological sorting of entities based on declared dependencies, ensuring correct import order without manual sequencing.
5. **Direct D365FO Import:** Uploads and imports packages directly to D365FO via the Data Management REST API, authenticated through Azure AD application credentials.
6. **Parallel Processing:** Configurable parallelism for improved throughput during extraction and packaging.
7. **Large Dataset Splitting:** Automatic splitting of large entity datasets into multiple files based on configurable record count thresholds.
8. **Azure Blob Integration:** Direct upload of packages to D365FO Azure Blob storage.
9. **Multiple Execution Modes:** Three CLI commands -- export to files (`export-file`), export to packages (`export-package`), and full import to D365FO (`import-d365`).

### Out of Scope for MVP

The following are explicitly not part of the current tool and are not planned for the immediate next phase:

- **GUI or web interface:** The tool is and will remain a CLI application. No UI is planned.
- **Built-in error reporting or dashboards:** The tool executes migrations; error analysis is performed by the functional consultant directly in D365FO.
- **Multi-tenant or SaaS deployment:** The tool runs locally or on a project server, configured per engagement.
- **Source systems other than SQL Server:** Only SQL Server sources are supported.

### MVP Success Criteria

The existing tool meets its success criteria:

- All configured entities import to D365FO without technical errors in a single unattended run
- Dependency ordering prevents sequencing failures across 100+ entities
- Migrations execute unattended on nights and weekends
- Tool is reused across multiple D365FO implementations without project-specific rebuilds
- Data quality convergence achieved within 5 migration cycles per project

### Future Vision

#### Near-Term: Hardening & Modernization
- **Upgrade to .NET 10:** Migrate from .NET 8 to .NET 10 before .NET 8 end-of-support. No feature changes -- runtime and SDK upgrade only.
- **Test coverage:** Introduce xUnit test suite with Shouldly assertions, starting from zero. Target comprehensive coverage across dependency sorting, XML generation, package creation, and API integration.
- **Documentation:** Improve documentation beyond the current README -- setup guides, configuration reference, entity definition authoring guide, and developer documentation for codebase maintenance.

#### Medium-Term: Operational Improvements
- **Selective entity runs:** Ability to run a subset of entities without reconfiguring, enabling targeted re-runs when only specific entities have updated source data.
- **Cycle-over-cycle error comparison:** Automated comparison of migration results between cycles, surfacing new errors vs. known issues awaiting source team fixes. Reduces the functional consultant's manual comparison effort.

#### Long-Term: Visibility & Reporting
- **Project director dashboard:** Aggregated data quality metrics across migration cycles -- error trends, entity-level status, convergence tracking -- giving the project director a risk-based view of migration readiness without relying on manual reports.
