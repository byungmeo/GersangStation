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
- `npm run deploy` builds and publishes `dist` to GitHub Pages. `.github/workflows/build-deploy-website.yml` invokes it for matching pushes to `master` or manual dispatch.

Dependency compatibility overrides in `package.json`:

- `postcss-selector-parser` 7.1.6+ applies the selector parsing security fix while retaining the Tailwind 3/PostCSS configuration.
- `katex` 0.18.2 applies the trust restriction fix while supporting CI's Node 20 runtime. Recheck Node engine requirements before changing this pin; KaTeX 0.18.10 pulls in Commander 15, which requires Node 22.12+.
