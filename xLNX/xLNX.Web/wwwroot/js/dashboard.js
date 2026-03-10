async function refreshData() {
    try {
        const resp = await fetch('/api/v1/state');
        const data = await resp.json();
        document.getElementById('running-count').textContent = data.counts.running;
        document.getElementById('retry-count').textContent = data.counts.retrying;
        document.getElementById('total-tokens').textContent = data.codex_totals.total_tokens.toLocaleString();
        document.getElementById('runtime').textContent = data.codex_totals.seconds_running.toFixed(1);

        const runTable = document.getElementById('running-table');
        runTable.innerHTML = data.running.map(r =>
            `<tr><td>${r.issue_identifier}</td><td>${r.state}</td><td>${r.turn_count}</td><td>${r.last_event || '-'}</td><td>${r.tokens.total_tokens}</td></tr>`
        ).join('');

        const retryTable = document.getElementById('retry-table');
        retryTable.innerHTML = data.retrying.map(r =>
            `<tr><td>${r.issue_identifier}</td><td>${r.attempt}</td><td>${r.due_at}</td><td>${r.error || '-'}</td></tr>`
        ).join('');

        document.getElementById('error').style.display = 'none';
    } catch (e) {
        document.getElementById('error').textContent = 'Failed to load state: ' + e.message;
        document.getElementById('error').style.display = 'block';
    }
}

async function triggerPoll() {
    try {
        await fetch('/api/v1/refresh', { method: 'POST' });
        setTimeout(refreshData, 1000);
    } catch (e) {
        document.getElementById('error').textContent = 'Failed to trigger poll: ' + e.message;
        document.getElementById('error').style.display = 'block';
    }
}

refreshData();
setInterval(refreshData, 5000);
