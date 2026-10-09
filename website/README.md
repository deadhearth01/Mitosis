# Mitosis website

A plain static page in `public/`, served as static assets on [Cloudflare Workers](https://developers.cloudflare.com/workers/static-assets/) (no Worker script, so requests are free).

Preview locally:

```sh
npm install
npm run dev          # http://localhost:8787
```

Deploy (after `npx wrangler login` once):

```sh
npm run deploy
```

Pushes to `main` that touch `website/` deploy automatically through `.github/workflows/website.yml` once the repository has the `CLOUDFLARE_API_TOKEN` and `CLOUDFLARE_ACCOUNT_ID` secrets.

All paths in the page are relative, so it also works under a sub-path such as `/labs/mitosis/`.
