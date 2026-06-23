# Vault — Infrastructure

Server/NAS sizing, scale path, and the Autodesk Desktop Connector boundary.
**[decided]** items are settled; verify current vendor configs/pricing before buying.

## Topology
- **API + PostgreSQL** run as containers on one always-on **LAN host** (the "app server").
- **UniFi UNAS Pro** holds the file blobs only. It **cannot run Docker/containers** — it is storage-only — so it is mounted into the API over **NFS**, not used as the compute host. **[decided]**
- Clients (WPF app, Explorer client) are thin and talk only to the API over the LAN. No client touches PostgreSQL or the NAS directly.
- The API runs `Database.Migrate()` on startup; the schema self-provisions once the EF migration exists.

```
Engineers / Assemblers ──HTTP──> [ App server: Vault.Api + PostgreSQL (Docker) ]
                                          │ NFS (blobs)
                                          ▼
                                 [ UniFi UNAS Pro (file store) ]
```

## App-server hardware **[decided]**
Workload is **light** (≈10 engineers + ≈30 read-only assemblers; load is file I/O and a small
DB, not heavy compute). Size for **reliability**, not horsepower.

- **Form factor:** 1U rack (e.g. Dell PowerEdge R360) or tower (T360) — pick to match the rack/closet.
- **CPU:** single Xeon E-2400 class (entry server CPU is plenty).
- **RAM:** 64 GB ECC.
- **Disk:** 2 × ~1 TB SSD in **RAID 1** (OS + PostgreSQL + container images). CAD blobs live on the NAS, not here.
- **Network:** **10 GbE NIC, matched to the NAS link** — the dominant load is many concurrent file downloads, so NAS↔server throughput matters more than CPU.
- **Resilience:** redundant PSU, out-of-band mgmt (iDRAC), on a **UPS**.
- **Host OS / virtualization:** Proxmox or Windows Server + Hyper-V; run Vault as containers in a VM so the box is snapshot-/backup-friendly.
- **Indicative cost:** ~$3–6k for the server. **Verify current configs and pricing** before purchase.

Backups: PostgreSQL dump + the NAS blob volume on a schedule; keep the hidden `file_revision_backups` archive in the same backup set (it is the rollback history).

## Scale path (when/if needed)
1. **Vertical first** — more RAM / faster SSD / more cores on the single host. Covers a long way for this user count.
2. **Horizontal** — the API is **stateless** (all state in PostgreSQL + `IFileStore`), so run 2+ API instances behind a load balancer. *Keep it stateless: no in-process session/file state.*
3. **HA** — managed/replicated PostgreSQL (primary + standby); multiple API nodes.
4. **Storage** — NAS → S3/MinIO by swapping the `IFileStore` implementation (the adapter exists for exactly this); no domain changes.
5. **Second vault** — the documentation vault is a `Repository` row, not new infrastructure (see `data-model.md`).

**Don't:** Kubernetes, dual-socket servers, or cloud-hosting the whole stack for this scale — all over-built. Add complexity only when a real bottleneck shows up.

## Autodesk Desktop Connector (ADC) boundary **[decided]**
ADC and Vault are **separate systems over different storage** — do **not** try to integrate them, and do **not** disable ADC.

- ADC only mirrors **Autodesk cloud** sources (Fusion/Forma/Drive) into Explorer and **caches on C: with no option for another drive** — it **cannot point at the NAS**. (Autodesk docs; ADC is explicitly "not a data-transfer tool.")
- Vault is a **separate Explorer mount** over the NAS, owned by the Vault Explorer client. The two coexist as different mount points.
- **Workflow boundary (recommended hybrid/release model):**
  - Engineers **design in Fusion** (cloud + ADC, as today).
  - At controlled **release**, the design is brought **into Vault** (export `.f3d`/`.f3z` + publish a drawing **PDF**). Vault then owns the controlled revision, the approval workflow, and read-only assembler distribution.
  - Changes to a released item are **checked out of Vault**, edited, checked back in, re-approved.
- **Assembler viewing:** assemblers consume **published derivatives** — drawing **PDF** + 3D in **Autodesk's free web viewer** — never the source `.f3d`, so no Fusion seats are needed for ~30 viewers.
- **Feasibility risk:** Fusion is cloud-native (`.f3d` is an export) and assemblies carry **component references / where-used**. Whether those survive the export/round-trip into a local vault is the project's go/no-go — validate in the Phase 0 spike (`TASKS.md`).
