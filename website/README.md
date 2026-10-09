# Mitosis website

This is a plain static page. Preview it from this directory with:

```sh
python3 -m http.server 8000
```

Open `http://localhost:8000/`.

To deploy, copy the contents of `website/` to the web root for `https://theavni.studio/labs/mitosis/`. All stylesheet, script, and image paths are relative, so the page works under `/labs/mitosis/`.

Before the 0.1 release, replace `GITHUB_OWNER = 'OWNER'` in `script.js` with the actual repository owner and verify the installer and Homebrew tap paths. The page marks these commands as going live with 0.1.
