# GersangStation support website

Applies to `Website/` only. React/TypeScript/Vite site; desktop build and release rules do not apply.

From this directory:

```powershell
npm ci
npm run dev
npm run build
npm run lint
```

`package.json` owns the commands; `package-lock.json` owns resolved dependency versions.

- `src/app/page.tsx`: support page. `public/answers/*.md`: user-facing help content, not AI instructions.
- `vite.config.ts` uses `/GersangStation` as the asset base; `src/App.tsx` uses `VITE_REPOSITORY_NAME` for the router basename. Keep these aligned when changing hosting paths.
- `npm run deploy` builds and publishes `dist` to GitHub Pages. `.github/workflows/build-deploy-website.yml` invokes it for matching pushes to `master` or manual dispatch. Use it only for requested publication, not local verification.
