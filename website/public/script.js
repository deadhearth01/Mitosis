const GITHUB_OWNER = 'deadhearth01';
const REPO_URL = `https://github.com/${GITHUB_OWNER}/Mitosis`;
const THEME_KEY = 'mitosis-theme';

const commands = {
  curl: `curl -fsSL https://raw.githubusercontent.com/${GITHUB_OWNER}/Mitosis/main/scripts/install.sh | bash`,
  brew: `brew install --cask ${GITHUB_OWNER}/tap/mitosis`,
};

const setHref = (selector, href) => {
  const node = document.querySelector(selector);
  if (node) node.href = href;
};

const curl = document.querySelector('#curl-command');
const brew = document.querySelector('#brew-command');
if (curl) curl.textContent = commands.curl;
if (brew) brew.textContent = commands.brew;

setHref('[data-releases]', `${REPO_URL}/releases/latest`);
setHref('[data-repo]', REPO_URL);
setHref('[data-license]', `${REPO_URL}/blob/main/LICENSE`);
setHref('[data-repo-readme]', `${REPO_URL}#which-apps-work`);

for (const button of document.querySelectorAll('[data-copy]')) {
  button.addEventListener('click', async () => {
    const kind = button.dataset.copy;
    const status = document.querySelector('#copy-status');
    const value = commands[kind];
    if (!value) return;

    let copied = false;
    try {
      await navigator.clipboard.writeText(value);
      copied = true;
    } catch {
      copied = legacyCopy(value);
    }

    button.textContent = copied ? 'Copied' : 'Copy';
    if (status) {
      status.textContent = copied
        ? 'Install command copied.'
        : 'Could not copy. Select the command and copy it manually.';
    }
    window.setTimeout(() => { button.textContent = 'Copy'; }, 1800);
  });
}

function legacyCopy(value) {
  const field = document.createElement('textarea');
  field.value = value;
  field.setAttribute('readonly', '');
  field.style.position = 'fixed';
  field.style.top = '-1000px';
  document.body.append(field);
  field.select();
  let ok = false;
  try {
    ok = document.execCommand('copy');
  } catch {
    ok = false;
  }
  field.remove();
  return ok;
}

/* Theme: follows the system until the reader picks a side. */
const root = document.documentElement;
const darkQuery = window.matchMedia('(prefers-color-scheme: dark)');

const readTheme = () => {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    return stored === 'light' || stored === 'dark' ? stored : null;
  } catch {
    return null;
  }
};

const activeTheme = () => root.dataset.theme || (darkQuery.matches ? 'dark' : 'light');

function paintTheme() {
  const isDark = activeTheme() === 'dark';
  for (const label of document.querySelectorAll('[data-theme-label]')) {
    label.textContent = isDark ? 'Switch to light theme' : 'Switch to dark theme';
  }
  for (const button of document.querySelectorAll('[data-theme-toggle]')) {
    button.setAttribute('title', isDark ? 'Switch to light theme' : 'Switch to dark theme');
  }
  /* A hand-picked theme must also pick the matching app screenshot. */
  for (const source of document.querySelectorAll('picture source[data-screen="dark"]')) {
    source.media = isDark ? 'all' : 'not all';
  }
}

const saved = readTheme();
if (saved) root.dataset.theme = saved;
paintTheme();

for (const button of document.querySelectorAll('[data-theme-toggle]')) {
  button.addEventListener('click', () => {
    const next = activeTheme() === 'dark' ? 'light' : 'dark';
    root.dataset.theme = next;
    try {
      localStorage.setItem(THEME_KEY, next);
    } catch {
      /* Private mode: the choice lasts for this page only. */
    }
    paintTheme();
  });
}

darkQuery.addEventListener('change', () => {
  if (!root.dataset.theme) paintTheme();
});
