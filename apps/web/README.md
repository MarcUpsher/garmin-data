# Garmin frontend

Angular and Angular Material UI, served by the .NET API in the production Docker image.
Use the [project README](../../README.md) for everyday startup or the
[development guide](../../docs/development.md) for running the API and Angular dev server separately.

## Source layout

- `src/main.ts` bootstraps the application shell.
- `src/app/app.config.ts` configures HTTP and routing.
- `src/app/app.routes.ts` declares page and drawer routes.
- `src/app/shell/` owns the sidebar, navigation and application-wide theme scope.
- `src/app/activities/` owns the list, filters, USB sync controls, detail view and hide dialog.
- `src/app/trends/` owns historical charts and their activity drill-down.
- `src/app/data-management/` owns the export import page.
- `src/app/shared/` contains the API client, API models, activity labels and theme preference.
- `src/styles.scss` defines global Material styling and shared notification styles.

Component templates, styles and tests live beside their TypeScript files. Activities, Trends and
Data & Imports render in the shell's router outlet. The activity list has its own child outlet for
the drawer, so opening `/activities/:id` preserves the list and its filters. `/activity/:id` opens
that activity as a dedicated page. Trends reuse the same detail component for their side panel.

## Commands

Run from the repository root:

```sh
npm ci --prefix apps/web
npm start --prefix apps/web
npm run build --prefix apps/web
npm test --prefix apps/web -- --watch=false
```

The development server runs on port 4200 and proxies `/api` to port 5080. Browser code uses relative
API URLs so the production UI and API can share one origin. Build output goes to `dist/garmin-web`,
with browser assets in its `browser/` directory. Build output and installed dependencies stay out of Git.
