const REPO = 'dienomb/MiBiblioteca';
const WORKFLOW = 'scrape.yml';
// TRIGGER_TOKEN is injected at deploy time by deploy-pages.yml from GitHub Secrets.
// It is never committed to the repository.

let pollInterval = null;
let pollStart = null;
const POLL_TIMEOUT_MS = 5 * 60 * 1000; // 5 minutes

async function triggerScrape() {
    if (!window.confirm('¿Ejecutar el scraper ahora?')) return;

    const btn = document.getElementById('triggerBtn');
    btn.disabled = true;
    document.getElementById('btnIcon').textContent = '⏳';
    document.getElementById('btnText').textContent = 'Enviando...';

    showPanel();
    showStatus('Enviando solicitud al scraper...', 'info');

    try {
        // Uses repository_dispatch (requires contents:write) instead of
        // workflow_dispatch (requires actions:write) for broader token compatibility.
        const res = await fetch(
            `https://api.github.com/repos/${REPO}/dispatches`,
            {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${TRIGGER_TOKEN}`,
                    'Accept': 'application/vnd.github+json',
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ event_type: 'trigger-scrape' }),
            }
        );

        if (res.status === 204) {
            showStatus('Scraper iniciado. Esperando actualizacion de estado...', 'success');
            document.getElementById('btnIcon').textContent = '🔄';
            document.getElementById('btnText').textContent = 'Scraper en ejecucion...';
            startPolling();
        } else {
            const body = await res.text().catch(() => '');
            const msg = res.status === 401
                ? 'Token invalido (401). El despliegue puede necesitar actualizarse.'
                : res.status === 403
                ? 'Permisos insuficientes (403). El token necesita scope contents:write.'
                : `Error ${res.status}: ${body}`;
            showStatus(msg, 'error');
            resetBtn();
        }
    } catch (err) {
        showStatus(`Error de red: ${err.message}`, 'error');
        resetBtn();
    }
}

function startPolling() {
    pollStart = Date.now();
    clearInterval(pollInterval);
    // Short initial delay to let GitHub register the new run
    setTimeout(() => {
        pollStatus();
        pollInterval = setInterval(pollStatus, 5000);
    }, 2000);
}

async function pollStatus() {
    if (Date.now() - pollStart > POLL_TIMEOUT_MS) {
        clearInterval(pollInterval);
        showStatus('Tiempo de espera agotado. Revisa GitHub Actions manualmente.', 'error');
        resetBtn();
        return;
    }

    try {
        const res = await fetch(
            `https://api.github.com/repos/${REPO}/actions/workflows/${WORKFLOW}/runs?per_page=1`,
            {
                headers: { 'Accept': 'application/vnd.github+json' }
                // No auth token — public repo allows unauthenticated reads
            }
        );

        if (!res.ok) return;

        const data = await res.json();
        const run = data.workflow_runs?.[0];
        if (!run) return;

        showRunDetails(run);

        if (run.status === 'completed') {
            clearInterval(pollInterval);
            const success = run.conclusion === 'success';
            showStatus(
                success
                    ? 'Scraper completado con exito. Los datos han sido actualizados.'
                    : `Scraper finalizado con estado: ${run.conclusion}.`,
                success ? 'success' : 'error'
            );
            document.getElementById('btnIcon').textContent = success ? '✅' : '❌';
            document.getElementById('btnText').textContent = 'Ejecutar scraper ahora';
            document.getElementById('triggerBtn').disabled = false;
        }
    } catch (_) {
        // Silently ignore transient network errors during polling
    }
}

function showRunDetails(run) {
    const details = document.getElementById('runDetails');
    details.classList.add('visible');

    const statusLabel = statusBadge(run.status, run.conclusion);
    document.getElementById('runStatus').innerHTML = statusLabel;

    const started = new Date(run.created_at);
    document.getElementById('runStarted').textContent = started.toLocaleTimeString('es-ES');

    const elapsed = formatDuration(run.created_at, run.updated_at, run.status);
    document.getElementById('runDuration').textContent = elapsed;

    document.getElementById('runLink').innerHTML =
        `<a class="run-link" href="${run.html_url}" target="_blank" rel="noopener">Ver en GitHub →</a>`;
}

function statusBadge(status, conclusion) {
    if (status === 'completed') {
        const cls = conclusion === 'success' ? 'success' : conclusion === 'failure' ? 'failure' : 'cancelled';
        const label = conclusion === 'success' ? 'Completado ✓' : conclusion === 'failure' ? 'Fallido ✗' : 'Cancelado';
        return `<span class="run-badge ${cls}">${label}</span>`;
    }
    if (status === 'in_progress') return '<span class="run-badge in_progress">Ejecutando...</span>';
    return '<span class="run-badge queued">En cola...</span>';
}

function formatDuration(startedAt, updatedAt, status) {
    const start = new Date(startedAt).getTime();
    const end = status === 'completed' ? new Date(updatedAt).getTime() : Date.now();
    const ms = end - start;
    if (ms < 60000) return `${Math.round(ms / 1000)}s`;
    return `${Math.floor(ms / 60000)}m ${Math.round((ms % 60000) / 1000)}s`;
}

function showStatus(msg, type) {
    const el = document.getElementById('statusMessage');
    el.textContent = msg;
    el.className = `status-message ${type}`;
}

function showPanel() {
    document.getElementById('statusPanel').classList.add('visible');
}

function resetBtn() {
    const btn = document.getElementById('triggerBtn');
    btn.disabled = false;
    document.getElementById('btnIcon').textContent = '🔄';
    document.getElementById('btnText').textContent = 'Ejecutar scraper ahora';
}
