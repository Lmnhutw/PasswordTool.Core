# YourSafe landing page

Static Vietnamese landing page: HTML, CSS, JavaScript, local fonts and a captioned repository screenshot. No build step, framework, package installation or backend.

## Preview

Open `index.html` directly, or serve this folder with an existing static server. For example, from the repository root, when Python is installed:

```powershell
python -m http.server 4173 --bind 127.0.0.1 --directory website
```

Open http://127.0.0.1:4173. Clipboard enhancement requires localhost or HTTPS; the visible command remains selectable everywhere. Navigation, FAQ, content and release links work without JavaScript.

## Publish on GitHub Pages

`website/` cannot be selected as a branch publishing folder: GitHub's branch publishing supports `/` or `/docs`. Use the included GitHub Actions workflow, which uploads only the three page files and assets.

1. Commit and push the website and `.github/workflows/website-pages.yml` to the repository's default branch when ready.
2. In the GitHub repository, open **Settings → Pages → Build and deployment → Source → GitHub Actions**.
3. In **Actions**, select **Publish YourSafe website**, then **Run workflow** on the default branch.
4. Open the URL reported by the deployment. For this repository the expected project-site address is https://lmnhutw.github.io/PasswordTool.Core/; the workflow output is authoritative.

The workflow is manual, so editing or pushing app code does not publish a website. It does not build or publish Windows packages. No repository settings or external publication were changed during implementation.

Reference: [GitHub publishing-source documentation](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).

## Content and releases

The public website uses the requested name **YourSafe**. Product claims follow the root `README.md` and `docs/release-operations.md`. Repository URLs, binary filenames and storage paths retain their actual existing names until the desktop/repository is renamed separately. Download buttons lead to the release list, without inventing a current version, available installer or signing status. The historical screenshot is explicitly labeled as predating the rename. Asset origins and font license are in `assets/PROVENANCE.md` and `assets/OFL.txt`.

## Verification

With an existing Playwright installation resolvable by Node (including through `NODE_PATH`):

```powershell
node --check website/script.js
node website/verify.cjs
git diff --check
```

`verify.cjs` uses installed Microsoft Edge on Windows, or Playwright Chromium elsewhere, to check 1440px desktop, 768px tablet, 390px mobile and 320px narrow screens, document overflow, assets, menu state and Escape handling, FAQ, clipboard success/failure, reduced motion, JavaScript-disabled behavior, and a `/PasswordTool.Core/` project base path. It writes desktop and mobile screenshots to `.impeccable/review/` and runs a temporary loopback-only server which is closed on completion. It makes no external requests and does not touch any vault or desktop application.
