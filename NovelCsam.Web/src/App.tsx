type JobState = 'draft' | 'uploading' | 'queued' | 'analyzing' | 'completed' | 'failed';

type Stage = {
  name: string;
  status: 'pending' | 'active' | 'complete';
  progress: number;
};

type JobSummary = {
  id: string;
  sourceName: string;
  state: JobState;
  framesTotal: number;
  framesProcessed: number;
  framesFailed: number;
  percentComplete: number;
  stages: Stage[];
};

type ResultRow = {
  frame: string;
  severity: 'none' | 'low' | 'medium' | 'high';
  childCheck: string;
  summary: string;
};

const demoJob: JobSummary = {
  id: 'job-24f8c9',
  sourceName: 'sample-video.mp4',
  state: 'analyzing',
  framesTotal: 128,
  framesProcessed: 92,
  framesFailed: 2,
  percentComplete: 72,
  stages: [
    { name: 'Upload', status: 'complete', progress: 100 },
    { name: 'Queue', status: 'complete', progress: 100 },
    { name: 'Frame extraction', status: 'complete', progress: 100 },
    { name: 'Safety analysis', status: 'active', progress: 72 },
    { name: 'Finalize', status: 'pending', progress: 0 }
  ]
};

const demoResults: ResultRow[] = [
  {
    frame: 'frames/000041.jpg',
    severity: 'low',
    childCheck: 'No',
    summary: 'Indoor scene with two adults seated near a desk and bright overhead lighting.'
  },
  {
    frame: 'frames/000066.jpg',
    severity: 'medium',
    childCheck: 'No',
    summary: 'A close indoor shot with a partially visible person and several high-contrast objects.'
  },
  {
    frame: 'frames/000089.jpg',
    severity: 'high',
    childCheck: 'Yes',
    summary: 'Uncertain image requiring human review because the automated model flagged elevated risk.'
  }
];

const statusCopy: Record<JobState, { label: string; tone: string }> = {
  draft: { label: 'Draft', tone: 'neutral' },
  uploading: { label: 'Uploading', tone: 'info' },
  queued: { label: 'Queued', tone: 'warn' },
  analyzing: { label: 'Analyzing', tone: 'accent' },
  completed: { label: 'Completed', tone: 'success' },
  failed: { label: 'Failed', tone: 'danger' }
};

function App() {
  return (
    <div className="app-shell">
      <header className="hero">
        <div>
          <p className="eyebrow">Novel CSAM Review</p>
          <h1>Review jobs with a status-first workflow.</h1>
          <p className="hero-copy">
            Upload media, monitor processing stages, watch batch progress, and inspect results without leaving the browser.
          </p>
        </div>
        <div className="hero-card">
          <div className={`badge badge-${statusCopy[demoJob.state].tone}`}>{statusCopy[demoJob.state].label}</div>
          <div className="hero-meta">
            <div>
              <span>Job ID</span>
              <strong>{demoJob.id}</strong>
            </div>
            <div>
              <span>Source</span>
              <strong>{demoJob.sourceName}</strong>
            </div>
          </div>
          <ProgressBar label="Overall progress" value={demoJob.percentComplete} accent />
        </div>
      </header>

      <main className="grid">
        <section className="panel upload-panel">
          <div className="panel-header">
            <div>
              <p className="panel-kicker">Create job</p>
              <h2>Upload and configure</h2>
            </div>
            <span className="subtle-chip">Container-safe flow</span>
          </div>

          <div className="dropzone">
            <strong>Drop files here</strong>
            <p>Videos or image batches upload directly to Blob Storage through the API.</p>
          </div>

          <div className="form-grid">
            <label>
              <span>Analysis profile</span>
              <select defaultValue="standard">
                <option value="standard">Standard moderation</option>
                <option value="detailed">Detailed review</option>
                <option value="child-check">Child presence check</option>
              </select>
            </label>
            <label>
              <span>Result format</span>
              <select defaultValue="json">
                <option value="json">Blob JSON</option>
                <option value="json-csv">Blob JSON + CSV export</option>
              </select>
            </label>
          </div>

          <div className="button-row">
            <button className="primary">Start analysis</button>
            <button className="secondary">Save draft</button>
          </div>
        </section>

        <section className="panel status-panel">
          <div className="panel-header">
            <div>
              <p className="panel-kicker">Job status</p>
              <h2>Live pipeline window</h2>
            </div>
            <span className={`badge badge-${statusCopy[demoJob.state].tone}`}>{statusCopy[demoJob.state].label}</span>
          </div>

          <div className="status-counters">
            <Metric label="Frames processed" value={`${demoJob.framesProcessed}/${demoJob.framesTotal}`} />
            <Metric label="Failed frames" value={String(demoJob.framesFailed)} />
            <Metric label="Complete" value={`${demoJob.percentComplete}%`} />
          </div>

          <div className="stage-list">
            {demoJob.stages.map((stage) => (
              <div key={stage.name} className={`stage stage-${stage.status}`}>
                <div className="stage-top">
                  <strong>{stage.name}</strong>
                  <span>{stage.progress}%</span>
                </div>
                <ProgressBar value={stage.progress} />
              </div>
            ))}
          </div>

          <div className="status-footer">
            <div>
              <span>Last update</span>
              <strong>12 seconds ago</strong>
            </div>
            <div>
              <span>Queue depth</span>
              <strong>3 jobs</strong>
            </div>
          </div>
        </section>

        <section className="panel results-panel">
          <div className="panel-header">
            <div>
              <p className="panel-kicker">Results</p>
              <h2>Frame review table</h2>
            </div>
            <span className="subtle-chip">JSON ready</span>
          </div>

          <div className="result-window">
            {demoResults.map((row) => (
              <article key={row.frame} className="result-card">
                <div className="result-card-top">
                  <strong>{row.frame}</strong>
                  <span className={`severity severity-${row.severity}`}>{row.severity}</span>
                </div>
                <p>{row.summary}</p>
                <div className="result-card-meta">
                  <span>Child check: {row.childCheck}</span>
                  <span>Review status: open</span>
                </div>
              </article>
            ))}
          </div>
        </section>

        <section className="panel log-panel">
          <div className="panel-header">
            <div>
              <p className="panel-kicker">Activity</p>
              <h2>Status log window</h2>
            </div>
            <span className="subtle-chip">Auto-refresh</span>
          </div>

          <div className="log-stream">
            <LogEntry time="12:41:03" message="Upload completed and stored under uploads/job-24f8c9." />
            <LogEntry time="12:41:12" message="Durable orchestrator queued 128 frame-analysis activities." />
            <LogEntry time="12:42:09" message="Safety analysis running at 72% with 2 recoverable frame failures." />
          </div>
        </section>
      </main>
    </div>
  );
}

function ProgressBar({ label, value, accent = false }: { label?: string; value: number; accent?: boolean }) {
  return (
    <div className="progress-block">
      {label ? <span className="progress-label">{label}</span> : null}
      <div className={`progress-track ${accent ? 'progress-track-accent' : ''}`}>
        <div className="progress-fill" style={{ width: `${Math.min(100, Math.max(0, value))}%` }} />
      </div>
      <span className="progress-value">{value}%</span>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="metric-card">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function LogEntry({ time, message }: { time: string; message: string }) {
  return (
    <div className="log-entry">
      <time>{time}</time>
      <p>{message}</p>
    </div>
  );
}

export default App;