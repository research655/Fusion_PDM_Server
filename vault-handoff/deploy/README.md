# Deploying Vault with a UniFi NAS

**Important:** the UniFi UNAS (Pro) is storage-only — it cannot run Docker or the
database. So:

- **UniFi NAS** holds the **file blobs** (shared over NFS to the API).
- A **separate always-on host on the LAN** runs the **API + PostgreSQL** containers
  (a mini-PC/NUC, an existing always-on Windows PC with Docker Desktop, or a small
  Linux box/VM).
- **Clients** (WPF + Explorer) point at that host.

> Prerequisite: deploy from a **bootstrapped, implemented** repo (after
> `scripts/bootstrap.ps1` and Cursor's Phase 1/2 + EF migration). The container builds
> from `Vault.sln` and `src/`. Deploying earlier still serves Swagger as an infra smoke
> test, but endpoints need the schema.

## 1. On the UniFi NAS: create the share + enable NFS
- Create a dedicated shared drive for the blobs (e.g. "vault-data").
- Enable **NFS** access on that share. Restrict it to the API host's IP.
- Give end users **no direct write access** — only the API host writes here.

## 2. On the API host: install Docker
- Linux: Docker Engine + compose plugin.
- Windows: Docker Desktop.

## 3. Find the real NFS export path
The path in the UniFi dashboard is not the NFS export path. From the API host:
```bash
showmount -e <UNAS_IP>
```
Use the returned path (looks like `/volume1/.srv/.unifi-drive/vault-data/.data`).

## 4. Get the repo + configure
```bash
git clone <your-repo> vault && cd vault/deploy
cp .env.example .env
# edit .env: POSTGRES_PASSWORD, UNAS_IP, UNAS_EXPORT_PATH (from step 3)
```

## 5. Launch + verify
```bash
docker compose up -d --build
docker compose ps                 # db healthy, api running
docker compose exec api ls /data/vault   # confirms the UniFi NFS mount is live
```
Then from any LAN PC open `http://<api-host-ip>:8080/swagger` (15 endpoints; 501s are
expected until the methods are implemented).

## 6. Point clients at the API host
Set the WPF + Explorer `VaultApiClient` base address to `http://<api-host-ip>:8080`.

## Alternative: SMB instead of NFS
If you'd rather use the SMB share, mount it on the host (Linux `cifs`, or a Windows
mapped drive with a service account that has write access), then replace the
`vault-blobs` NFS volume with a bind mount to that local mount point. NFS is simpler
for containers — no stored credentials — so it's the recommended path.

## TLS, backups, updates
- **TLS:** put the API behind a reverse proxy on the host (or a UniFi gateway) and point
  clients at the `https://` URL; the container stays HTTP internally.
- **Backups:** `docker compose exec db pg_dump -U postgres vault > vault-YYYYMMDD.sql`
  (schedule it); include the UniFi share in the NAS backup job.
- **Updates:** `git pull` on the host, then `docker compose up -d --build`.
- **Schema:** the API runs `Database.Migrate()` on startup, so it self-provisions once
  the EF migration exists.
