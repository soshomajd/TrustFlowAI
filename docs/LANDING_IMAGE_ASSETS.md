# Landing image assets — handoff for Claude

Ten original illustrations were created with the built-in `image_gen` tool on 2026-10-13 after reading the current project and landing page. Palette: navy `#060a11`, cobalt `#2f6bff`, cyan `#18c8ff`. Each image is 1536 × 1024, with no marketing copy embedded.

## Locations

- PostgreSQL: local `trustflow_dev`, table `LandingImageAssets`. `Content` stores the optimized WebP bytes; `OriginalContent` stores the full original PNG bytes. Metadata includes `Slug`, `Title`, `AltText`, `SuggestedPlacement`, dimensions, WebP SHA-256, generation prompt, and creation time.
- Original PNG files and exact generation prompts: `output/imagegen/landing/`, `manifest.json`.
- WebP files exported from the database: `frontend/trustflow-web/public/images/landing/`.
- Web asset paths, recommended placement, alt text, dimensions and checksums: `output/imagegen/landing/web-assets.json`.
- Open `output/imagegen/landing/gallery.html` for a visual gallery and downloads.

## Assets

| Slug | Suggested use |
| --- | --- |
| `01-trust-bridge-hero` | Hero illustration |
| `02-protected-project-budget` | Protected budget / escrow |
| `03-milestone-path` | Ordered milestone payments |
| `04-client-project-planning` | How it works for clients |
| `05-freelancer-creative-work` | How it works for freelancers |
| `06-user-owned-wallet` | User-owned wallet / signing |
| `07-specialists-one-project` | Several freelancers, independent escrows |
| `08-ai-advice-for-delivery` | AI delivery review / advice |
| `09-human-dispute-review` | Human evidence review |
| `10-completed-collaboration` | Final CTA / completion |

## Using these in the landing page

The landing page has not yet been changed to display this collection. Read `frontend/trustflow-web/AGENTS.md` and the relevant installed Next.js documentation before integrating. The landing source is `src/features/landing/components/landing-page.tsx`. Static URLs are `/images/landing/<slug>.webp`; use the alt text and 1536 × 1024 dimensions from `web-assets.json`. Preserve readable foreground text and the early-access/test-network copy. These are conceptual AI-generated illustrations, not customer photos or testimonials. The human owns wallet actions; AI advises; each freelancer has a separate escrow. Use the images where they support the existing sections.

No HTTP asset endpoint was added. The exported public files are ready for direct use; the database remains the stored source of both image formats.

## Import or export again

`backend/TrustFlow.LandingAssetTool` is an explicit command-line tool, not an application startup task. Set `ConnectionStrings__DefaultConnection` using the existing local development secrets, without printing the connection string or committing credentials. It only accepts localhost/127.0.0.1, port 5433, database `trustflow_dev`.

```powershell
dotnet run --project backend/TrustFlow.LandingAssetTool --configuration Release -- import .
dotnet run --project backend/TrustFlow.LandingAssetTool --configuration Release -- export .
```

Import reads the ten PNGs and manifest, encodes WebP at quality 88, explicitly applies only a pending `AddLandingImageAssets` migration, imports all ten in a transaction, and verifies the bytes read back from PostgreSQL. Re-import preserves identical existing rows and rejects different bytes under the same slug. Both commands export WebP files from the database; differing existing image files are not overwritten. Export does not require the original PNG files.

To inspect metadata without downloading binary columns:

```sql
SELECT "Slug", "Title", "SuggestedPlacement", "PixelWidth", "PixelHeight",
       octet_length("Content") AS webp_bytes,
       octet_length("OriginalContent") AS png_bytes
FROM "LandingImageAssets"
ORDER BY "Slug";
```
