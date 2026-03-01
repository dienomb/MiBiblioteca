const REPO = 'dienomb/MiBiblioteca';
const WORKFLOW = 'scrape.yml';

function triggerScrape() {
    // Triggering requires GitHub authentication; open GitHub Actions directly.
    window.open(
        `https://github.com/${REPO}/actions/workflows/${WORKFLOW}`,
        '_blank',
        'noopener,noreferrer'
    );
}
