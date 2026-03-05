namespace xLNX.Web.Controllers;

/// <summary>
/// Generates the HTML dashboard for the / route.
/// See SPEC Section 13.7.1.
/// </summary>
public static class DashboardHtml
{
    public static string Render()
    {
        return """
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>Symphony xLNX Dashboard</title>
                <style>
                    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }
                    h1 { color: #333; }
                    .card { background: white; border-radius: 8px; padding: 16px; margin-bottom: 16px; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }
                    .card h2 { margin-top: 0; font-size: 1.1em; color: #555; }
                    .stat { font-size: 2em; font-weight: bold; color: #2563eb; }
                    .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
                    table { width: 100%; border-collapse: collapse; }
                    th, td { text-align: left; padding: 8px; border-bottom: 1px solid #eee; }
                    th { color: #666; font-weight: 600; }
                    #error { color: red; display: none; }
                    .refresh-btn { background: #2563eb; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; }
                    .refresh-btn:hover { background: #1d4ed8; }
                </style>
            </head>
            <body>
                <h1>Symphony xLNX Dashboard</h1>
                <p id="error"></p>
                <button class="refresh-btn" onclick="refreshData()">Refresh</button>
                <button class="refresh-btn" onclick="triggerPoll()">Trigger Poll</button>

                <div class="grid" style="margin-top: 16px;">
                    <div class="card">
                        <h2>Running</h2>
                        <div class="stat" id="running-count">-</div>
                    </div>
                    <div class="card">
                        <h2>Retrying</h2>
                        <div class="stat" id="retry-count">-</div>
                    </div>
                    <div class="card">
                        <h2>Total Tokens</h2>
                        <div class="stat" id="total-tokens">-</div>
                    </div>
                    <div class="card">
                        <h2>Runtime (s)</h2>
                        <div class="stat" id="runtime">-</div>
                    </div>
                </div>

                <div class="card">
                    <h2>Running Sessions</h2>
                    <table>
                        <thead><tr><th>Identifier</th><th>State</th><th>Turns</th><th>Last Event</th><th>Tokens</th></tr></thead>
                        <tbody id="running-table"></tbody>
                    </table>
                </div>

                <div class="card">
                    <h2>Retry Queue</h2>
                    <table>
                        <thead><tr><th>Identifier</th><th>Attempt</th><th>Due At</th><th>Error</th></tr></thead>
                        <tbody id="retry-table"></tbody>
                    </table>
                </div>

                <script>
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
                        await fetch('/api/v1/refresh', { method: 'POST' });
                        setTimeout(refreshData, 1000);
                    }

                    refreshData();
                    setInterval(refreshData, 5000);
                </script>
            </body>
            </html>
            """;
    }
}
