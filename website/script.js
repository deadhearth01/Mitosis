const GITHUB_OWNER = 'deadhearth01';
const REPO_URL = `https://github.com/${GITHUB_OWNER}/Mitosis`;

const commands = {
  curl: `curl -fsSL https://raw.githubusercontent.com/${GITHUB_OWNER}/Mitosis/main/scripts/install.sh | bash`,
};

document.querySelector('#curl-command').textContent = commands.curl;
document.querySelector('[data-repo]').href = REPO_URL;
document.querySelector('[data-license]').href = `${REPO_URL}/blob/main/LICENSE`;
document.querySelector('[data-repo-readme]').href = `${REPO_URL}#which-apps-work`;

for (const button of document.querySelectorAll('[data-copy]')) {
  button.addEventListener('click', async () => {
    const kind = button.dataset.copy;
    const status = document.querySelector('#copy-status');
    try {
      await navigator.clipboard.writeText(commands[kind]);
      button.textContent = 'Copied';
      status.textContent = 'Install command copied.';
      window.setTimeout(() => { button.textContent = 'Copy'; }, 1800);
    } catch {
      status.textContent = 'Could not copy. Select the command and copy it manually.';
    }
  });
}
