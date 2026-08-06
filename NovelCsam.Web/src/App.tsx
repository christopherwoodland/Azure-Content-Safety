import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from 'react';

type WizardStep = 'setup' | 'upload' | 'review' | 'monitor' | 'results' | 'detail' | 'history';
type Severity = 'none' | 'low' | 'medium' | 'high';

type StepState = {
  id: WizardStep;
  label: string;
  description: string;
};

type WizardForm = {
  containerName: string;
  containerDirectory: string;
  extractedFramesDirectory: string;
  frameIntervalSeconds: number;
  pollingIntervalSeconds: number;
  getSummary: boolean;
  getChildYesNo: boolean;
  archiveSourceOnSuccess: boolean;
};

type StartResponse = {
  id?: string;
  instanceId?: string;
  statusQueryGetUri?: string;
  StatusQueryGetUri?: string;
  statusQueryGetUrl?: string;
  managementUrls?: {
    statusQueryGetUri?: string;
    StatusQueryGetUri?: string;
  };
};

type DurableStatus = {
  runtimeStatus: string;
  customStatus?: Record<string, unknown> | null;
  output?: unknown;
};

type Manifest = {
  JobId: string;
  Status: string;
  TotalFrames: number;
  ProcessedFrames: number;
  SuccessfulFrames: number;
  FailedFrames: number;
  SkippedFrames: number;
  StartedAtUtc: string;
  CompletedAtUtc: string;
  FrameResultBlobs: string[];
  SkippedItems: string[];
};

type ResultRow = {
  id: string;
  frame: string;
  frameResultBlobPath: string;
  severity: Severity;
  score: number;
  childCheck: string;
  summary: string;
  imageBase64: string;
  hate: number;
  selfHarm: number;
  violence: number;
  sexual: number;
  contentSafetyObserved: boolean;
  summaryObserved: boolean;
  childCheckObserved: boolean;
};

type LogItem = {
  at: string;
  message: string;
};

type RunHistoryItem = {
  runId: string;
  instanceId: string;
  status: string;
  startedAtUtc: string;
  completedAtUtc: string;
  totalFrames: number;
  processedFrames: number;
  successfulFrames: number;
  failedFrames: number;
  skippedFrames: number;
  analyzableFrames: number;
  contentSafetyObserved: number;
  summaryObserved: number;
  childCheckObserved: number;
  getSummary: boolean;
  getChildYesNo: boolean;
  containerName: string;
  containerDirectory: string;
};

const FUNCTION_BASE_URL = (import.meta.env.VITE_FUNCTION_BASE_URL as string | undefined) ?? (import.meta.env.PROD ? '' : 'http://localhost:7092');
const START_PATH = (import.meta.env.VITE_DURABLE_START_PATH as string | undefined) ?? '/api/AnalyzeFrames_HttpStart';
const FUNCTION_CODE = (import.meta.env.VITE_FUNCTION_CODE as string | undefined) ?? '';
const STORAGE_ACCESS_PATH = '/api/storage/access';
const RESULTS_CONTAINER_NAME = (import.meta.env.VITE_RESULTS_CONTAINER_NAME as string | undefined) ?? 'results';
const TERMINAL_STATUSES = new Set(['Completed', 'Failed', 'Terminated', 'Canceled']);
const RUN_HISTORY_STORAGE_KEY = 'novelcsam.runHistory.v1';
const MAX_HISTORY_ITEMS = 30;

const steps: StepState[] = [
  { id: 'setup', label: 'Step 1', description: 'Source and analysis options' },
  { id: 'upload', label: 'Step 2', description: 'Upload source videos' },
  { id: 'review', label: 'Step 3', description: 'Confirm payload and launch' },
  { id: 'monitor', label: 'Step 4', description: 'Track durable orchestration' },
  { id: 'results', label: 'Step 5', description: 'Inspect frame outputs' },
  { id: 'detail', label: 'Step 6', description: 'Frame detail page' },
  { id: 'history', label: 'Step 7', description: 'Review past runs' }
];

const initialForm: WizardForm = {
  containerName: 'videos',
  containerDirectory: 'input',
  extractedFramesDirectory: 'extracted',
  frameIntervalSeconds: 2,
  pollingIntervalSeconds: 8,
  getSummary: true,
  getChildYesNo: true,
  archiveSourceOnSuccess: true
};

function App() {
  const [step, setStep] = useState<WizardStep>('setup');
  const [form, setForm] = useState<WizardForm>(initialForm);
  const [isStarting, setIsStarting] = useState(false);
  const [runId, setRunId] = useState('');
  const [instanceId, setInstanceId] = useState('');
  const [statusQueryUri, setStatusQueryUri] = useState('');
  const [durableStatus, setDurableStatus] = useState<DurableStatus | null>(null);
  const [manifest, setManifest] = useState<Manifest | null>(null);
  const [resultRows, setResultRows] = useState<ResultRow[]>([]);
  const [selectedResultId, setSelectedResultId] = useState('');
  const [errorMessage, setErrorMessage] = useState('');
  const [logItems, setLogItems] = useState<LogItem[]>([]);
  const [severityFilter, setSeverityFilter] = useState<'all' | Severity>('all');
  const [selectedFiles, setSelectedFiles] = useState<File[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedBlobPaths, setUploadedBlobPaths] = useState<string[]>([]);
  const [runHistory, setRunHistory] = useState<RunHistoryItem[]>([]);
  const [isLoadingHistoryRun, setIsLoadingHistoryRun] = useState(false);
  const normalizedContainerName = normalizeContainerName(form.containerName);
  const containerNameError = getContainerNameError(normalizedContainerName);
  const statusPollingSeconds = Math.max(1, Math.floor(form.pollingIntervalSeconds || 8));

  const statusLabel = durableStatus?.runtimeStatus ?? 'Not started';
  const isRunActive = step === 'monitor' && !TERMINAL_STATUSES.has(statusLabel);
  const monitorProgress = useMemo(() => {
    const custom = durableStatus?.customStatus as Record<string, unknown> | undefined;
    const processed = safeNumber(custom?.ProcessedFrames ?? custom?.processedFrames);
    const total = safeNumber(custom?.TotalFrames ?? custom?.totalFrames);
    if (total <= 0) {
      return 0;
    }

    return Math.min(100, Math.round((processed / total) * 100));
  }, [durableStatus]);

  const filteredRows = useMemo(() => {
    if (severityFilter === 'all') {
      return resultRows;
    }

    return resultRows.filter((row) => row.severity === severityFilter);
  }, [resultRows, severityFilter]);

  const selectedResult = useMemo(() => {
    if (!selectedResultId) {
      return resultRows[0] ?? null;
    }

    return resultRows.find((row) => row.id === selectedResultId) ?? resultRows[0] ?? null;
  }, [resultRows, selectedResultId]);

  const serviceStats = useMemo(() => {
    const total = resultRows.length;
    const contentSafetyObserved = resultRows.filter((row) => row.contentSafetyObserved).length;
    const summaryObserved = resultRows.filter((row) => row.summaryObserved).length;
    const childCheckObserved = resultRows.filter((row) => row.childCheckObserved).length;

    return {
      total,
      contentSafetyObserved,
      summaryObserved,
      childCheckObserved
    };
  }, [resultRows]);

  useEffect(() => {
    setRunHistory(loadRunHistory());
  }, []);

  useEffect(() => {
    if (typeof window === 'undefined') {
      return;
    }

    if (!hasLocalStorage(window)) {
      return;
    }

    window.localStorage.setItem(RUN_HISTORY_STORAGE_KEY, JSON.stringify(runHistory));
  }, [runHistory]);

  useEffect(() => {
    if (step !== 'monitor' || !statusQueryUri) {
      return;
    }

    let canceled = false;
    const interval = window.setInterval(async () => {
      if (canceled) {
        return;
      }

      try {
        const status = await getJson<DurableStatus>(statusQueryUri);
        if (canceled) {
          return;
        }

        setDurableStatus(status);
        addLogEvent(setLogItems, 'durable', 'runtime_status', status.runtimeStatus);

        if (TERMINAL_STATUSES.has(status.runtimeStatus)) {
          window.clearInterval(interval);
          if (status.runtimeStatus === 'Completed') {
            const hydrated = await hydrateResults(
              runId,
              form.containerName,
              form.containerDirectory,
              setManifest,
              setResultRows,
              setErrorMessage,
              setLogItems
            );
            if (hydrated) {
              upsertRunHistory(
                setRunHistory,
                buildRunHistoryItem(
                  runId,
                  instanceId,
                  form.containerName,
                  form.containerDirectory,
                  form.getSummary,
                  form.getChildYesNo,
                  hydrated.manifest,
                  hydrated.rows
                )
              );
              setStep('results');
            }
          } else {
            setErrorMessage(`Durable orchestration finished with status ${status.runtimeStatus}.`);
          }
        }
      } catch (err) {
        window.clearInterval(interval);
        setErrorMessage(getErrorMessage(err));
      }
    }, statusPollingSeconds * 1000);

    return () => {
      canceled = true;
      window.clearInterval(interval);
    };
  }, [step, statusQueryUri, runId, statusPollingSeconds]);

  useEffect(() => {
    if (!filteredRows.length) {
      setSelectedResultId('');
      return;
    }

    if (!selectedResultId || !filteredRows.some((row) => row.id === selectedResultId)) {
      setSelectedResultId(filteredRows[0].id);
    }
  }, [filteredRows, selectedResultId]);

  const isSetupValid =
    normalizedContainerName.length > 0 &&
    !containerNameError &&
    form.containerDirectory.trim().length > 0 &&
    form.frameIntervalSeconds > 0;

  async function uploadSelectedVideos(): Promise<void> {
    if (selectedFiles.length === 0) {
      setErrorMessage('Select one or more video files to upload.');
      return;
    }

    setIsUploading(true);
    setErrorMessage('');

    try {
      const uploadDirectory = normalizeDirectory(form.containerDirectory) || 'input';
      const uploadedPaths: string[] = [];

      for (const file of selectedFiles) {
        const blobName = `${uploadDirectory}/${crypto.randomUUID()}-${sanitizeFileName(file.name)}`;
        const uploadUrl = await getStorageAccessUrl('upload', normalizedContainerName, blobName);

        const response = await fetch(uploadUrl, {
          method: 'PUT',
          headers: {
            'x-ms-blob-type': 'BlockBlob',
            'Content-Type': file.type || 'application/octet-stream'
          },
          body: file
        });

        if (!response.ok) {
          const text = await response.text();
          throw new Error(`Upload failed for ${file.name}: ${response.status} ${response.statusText} ${text}`);
        }

        uploadedPaths.push(blobName);
      }

      setUploadedBlobPaths((prev) => [...uploadedPaths, ...prev]);
      setSelectedFiles([]);
      addLogEvent(setLogItems, 'upload', 'completed', `${uploadedPaths.length} file(s) to ${normalizedContainerName}/${uploadDirectory}`);
    } catch (err) {
      setErrorMessage(getErrorMessage(err));
    } finally {
      setIsUploading(false);
    }
  }

  async function startAnalysis(): Promise<void> {
    setIsStarting(true);
    setErrorMessage('');
    setManifest(null);
    setResultRows([]);
    setDurableStatus(null);
    setLogItems([]);

    try {
      const generatedRunId = crypto.randomUUID();
      const payload = {
        imageBase64ToDB: true,
        getSummary: form.getSummary,
        containerDirectory: form.containerDirectory.trim(),
        containerName: normalizedContainerName,
        getChildYesNo: form.getChildYesNo,
        runId: generatedRunId,
        frameIntervalSeconds: Math.max(1, Math.floor(form.frameIntervalSeconds)),
        extractedFramesDirectory: form.extractedFramesDirectory.trim() || 'extracted',
        archiveSourceOnSuccess: form.archiveSourceOnSuccess
      };

      const response = await fetch(getStartUrl(), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (!response.ok) {
        const text = await response.text();
        throw new Error(`Start call failed: ${response.status} ${response.statusText} ${text}`);
      }

      const rawStartBody = await response.text();
      const startData = tryParseStartResponse(rawStartBody);
      const resolvedStatusQueryUri = getStatusQueryUri(startData, response.headers);
      if (!resolvedStatusQueryUri) {
        throw new Error('Durable start response did not include a status query URL in body or headers.');
      }

      const resolvedInstanceId = getInstanceId(startData, response.headers, resolvedStatusQueryUri);

      setRunId(generatedRunId);
      setInstanceId(resolvedInstanceId || 'unknown');
      setStatusQueryUri(resolvedStatusQueryUri);
      setStep('monitor');
      addLogEvent(setLogItems, 'durable', 'submitted', `runId=${generatedRunId}`);
      addLogEvent(setLogItems, 'durable', 'instance', resolvedInstanceId || 'unknown');
      addLogEvent(
        setLogItems,
        'service',
        'call_plan',
        `contentSafety=enabled openAiSummary=${form.getSummary ? 'enabled' : 'disabled'} openAiChild=${form.getChildYesNo ? 'enabled' : 'disabled'} archiveOnSuccess=${form.archiveSourceOnSuccess ? 'enabled' : 'disabled'}`
      );
    } catch (err) {
      setErrorMessage(getErrorMessage(err));
    } finally {
      setIsStarting(false);
    }
  }

  function resetWizard(): void {
    setStep('setup');
    setRunId('');
    setInstanceId('');
    setStatusQueryUri('');
    setDurableStatus(null);
    setManifest(null);
    setResultRows([]);
    setSelectedResultId('');
    setErrorMessage('');
    setLogItems([]);
    setSeverityFilter('all');
    setSelectedFiles([]);
    setIsUploading(false);
    setUploadedBlobPaths([]);
  }

  async function openHistoryRun(item: RunHistoryItem): Promise<void> {
    setIsLoadingHistoryRun(true);
    setErrorMessage('');
    setRunId(item.runId);
    setInstanceId(item.instanceId);
    setForm((previous) => ({
      ...previous,
      containerName: item.containerName || previous.containerName,
      containerDirectory: item.containerDirectory || previous.containerDirectory,
      getSummary: item.getSummary,
      getChildYesNo: item.getChildYesNo
    }));

    try {
      const hydrated = await hydrateResults(
        item.runId,
        item.containerName,
        item.containerDirectory,
        setManifest,
        setResultRows,
        setErrorMessage,
        setLogItems
      );
      if (hydrated) {
        setStep('results');
        addLogEvent(setLogItems, 'history', 'loaded', item.runId);
      }
    } finally {
      setIsLoadingHistoryRun(false);
    }
  }

  function openResultDetail(rowId: string): void {
    setSelectedResultId(rowId);
    setStep('detail');
  }

  return (
    <div className="app-shell">
      <header className="hero">
        <div className="hero-copy-wrap">
          <p className="eyebrow">Content Safety Review</p>
          <h1>Guided wizard for durable video safety analysis.</h1>
          <p className="hero-copy">
            Configure the source path, launch the durable pipeline, track extraction and frame processing, then review structured JSON outputs in one guided flow.
          </p>
        </div>
        <div className="hero-aside">
          <div className="badge badge-accent">Wizard mode</div>
          <div className="hero-aside-grid">
            <Metric label="Current step" value={steps.findIndex((s) => s.id === step) + 1 + ` / ${steps.length}`} />
            <Metric label="Runtime" value={statusLabel} />
            <Metric label="Run ID" value={runId || 'Not started'} compact />
          </div>
        </div>
      </header>

      <nav className="stepper" aria-label="Wizard steps">
        {steps.map((item, index) => {
          const currentIndex = steps.findIndex((stepItem) => stepItem.id === step);
          const status = index < currentIndex ? 'complete' : index === currentIndex ? 'active' : 'pending';

          return (
            <button
              key={item.id}
              type="button"
              className={`step-card step-${status} step-nav-button`}
              aria-current={item.id === step ? 'step' : undefined}
              onClick={() => setStep(item.id)}
            >
              <span className="step-label">{item.label}</span>
              <strong>{item.description}</strong>
            </button>
          );
        })}
      </nav>

      <main className="main-grid">
        <section className="panel primary-panel">
          {step === 'setup' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 1</p>
              <h2>Choose source and analysis options</h2>

              <div className="form-grid">
                <label>
                  <span>Container name</span>
                  <input
                    value={form.containerName}
                    onChange={(event) => setForm((prev) => ({ ...prev, containerName: event.target.value }))}
                    onBlur={(event) => setForm((prev) => ({ ...prev, containerName: normalizeContainerName(event.target.value) }))}
                    placeholder="videos"
                  />
                </label>
                {containerNameError ? <p className="help-copy">{containerNameError}</p> : null}
                <label>
                  <span>Input directory</span>
                  <input
                    value={form.containerDirectory}
                    onChange={(event) => setForm((prev) => ({ ...prev, containerDirectory: event.target.value }))}
                    placeholder="input"
                  />
                </label>
                <label>
                  <span>Extracted frames directory</span>
                  <input
                    value={form.extractedFramesDirectory}
                    onChange={(event) => setForm((prev) => ({ ...prev, extractedFramesDirectory: event.target.value }))}
                    placeholder="extracted"
                  />
                </label>
                <label>
                  <span>Frame interval in seconds</span>
                  <input
                    type="number"
                    min={1}
                    value={form.frameIntervalSeconds}
                    onChange={(event) => setForm((prev) => ({ ...prev, frameIntervalSeconds: Number(event.target.value) }))}
                  />
                </label>
                <label>
                  <span>Status polling interval in seconds</span>
                  <input
                    type="number"
                    min={1}
                    value={form.pollingIntervalSeconds}
                    onChange={(event) => setForm((prev) => ({ ...prev, pollingIntervalSeconds: Number(event.target.value) }))}
                  />
                </label>
              </div>

              <div className="option-grid">
                <ToggleCard
                  label="Detailed summary"
                  description="Ask OpenAI summary for each analyzed frame"
                  checked={form.getSummary}
                  onChange={(checked) => setForm((prev) => ({ ...prev, getSummary: checked }))}
                />
                <ToggleCard
                  label="Child yes or no check"
                  description="Ask model to classify child presence for each frame"
                  checked={form.getChildYesNo}
                  onChange={(checked) => setForm((prev) => ({ ...prev, getChildYesNo: checked }))}
                />
                <ToggleCard
                  label="Archive input files after success"
                  description="Move analyzed source files from input to processed after a successful run"
                  checked={form.archiveSourceOnSuccess}
                  onChange={(checked) => setForm((prev) => ({ ...prev, archiveSourceOnSuccess: checked }))}
                />
              </div>

              <div className="button-row">
                <button className="primary" disabled={!isSetupValid} onClick={() => setStep('upload')}>Continue</button>
                <button className="secondary" onClick={resetWizard}>Reset</button>
              </div>
            </div>
          ) : null}

          {step === 'upload' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 2</p>
              <h2>Upload videos to the evaluation container</h2>

              <p className="help-copy">
                Upload one or more source videos into <strong>{normalizedContainerName || 'videos'}/{normalizeDirectory(form.containerDirectory) || 'input'}</strong> before launching the durable run.
              </p>

              <label>
                <span>Video files</span>
                <input
                  type="file"
                  accept="video/*"
                  multiple
                  onChange={(event) => setSelectedFiles(Array.from(event.target.files ?? []))}
                />
              </label>

              {selectedFiles.length > 0 ? (
                <div className="result-window">
                  {selectedFiles.map((file) => (
                    <article key={file.name + file.size} className="result-card">
                      <div className="result-card-top">
                        <strong>{file.name}</strong>
                        <span className="severity severity-none">{Math.round(file.size / 1024 / 1024)} MB</span>
                      </div>
                    </article>
                  ))}
                </div>
              ) : null}

              {uploadedBlobPaths.length > 0 ? (
                <div className="result-window">
                  {uploadedBlobPaths.map((path) => (
                    <article key={path} className="result-card">
                      <div className="result-card-top">
                        <strong>{path}</strong>
                        <span className="severity severity-low">Uploaded</span>
                      </div>
                    </article>
                  ))}
                </div>
              ) : null}

              <div className="button-row">
                <button className="secondary" onClick={() => setStep('setup')}>Back</button>
                <button className="secondary" disabled={isUploading || selectedFiles.length === 0} onClick={uploadSelectedVideos}>
                  {isUploading ? 'Uploading...' : 'Upload selected'}
                </button>
                <button className="primary" onClick={() => setStep('review')}>Continue to launch</button>
              </div>
            </div>
          ) : null}

          {step === 'review' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 3</p>
              <h2>Review payload and launch durable flow</h2>

              <pre className="payload-preview">{JSON.stringify(
                {
                  imageBase64ToDB: true,
                  getSummary: form.getSummary,
                  getChildYesNo: form.getChildYesNo,
                  containerName: normalizedContainerName,
                  containerDirectory: form.containerDirectory,
                  frameIntervalSeconds: form.frameIntervalSeconds,
                  extractedFramesDirectory: form.extractedFramesDirectory,
                  archiveSourceOnSuccess: form.archiveSourceOnSuccess,
                  statusPollingIntervalSeconds: statusPollingSeconds
                },
                null,
                2
              )}</pre>

              <div className="button-row">
                <button className="secondary" onClick={() => setStep('upload')}>Back</button>
                <button className="primary" disabled={isStarting || !isSetupValid} onClick={startAnalysis}>
                  {isStarting ? 'Starting...' : 'Start analysis'}
                </button>
              </div>
            </div>
          ) : null}

          {step === 'monitor' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 4</p>
              <h2>Live durable orchestration status</h2>
              <ProgressBar label="Pipeline progress" value={monitorProgress} accent isActive={isRunActive} />

              <div className="service-notes" role="status" aria-live="polite">
                <p className="service-notes-title">Service call expectations for this run</p>
                <p>Content Safety: called for each analyzable frame.</p>
                <p>OpenAI summary: {form.getSummary ? 'enabled' : 'disabled'} in this request.</p>
                <p>OpenAI child check: {form.getChildYesNo ? 'enabled' : 'disabled'} in this request.</p>
              </div>

              <div className="status-counters">
                <Metric label="Runtime status" value={statusLabel} />
                <Metric label="Instance ID" value={instanceId || 'Pending'} compact />
                <Metric label="Run ID" value={runId || 'Pending'} compact />
              </div>

              <p className="help-copy">Polling every {statusPollingSeconds} second{statusPollingSeconds === 1 ? '' : 's'} until terminal status is reached. When completed, the wizard loads manifest and frame JSON outputs automatically.</p>

              <div className="button-row">
                <button className="secondary" onClick={resetWizard}>Cancel and reset</button>
              </div>
            </div>
          ) : null}

          {step === 'results' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 5</p>
              <h2>Results and review output</h2>

              {manifest ? (
                <div className="status-counters">
                  <Metric label="Manifest status" value={manifest.Status} />
                  <Metric label="Processed" value={`${manifest.ProcessedFrames}/${manifest.TotalFrames}`} />
                  <Metric label="Successful" value={String(manifest.SuccessfulFrames)} />
                  <Metric label="Failed" value={String(manifest.FailedFrames)} />
                  <Metric label="Skipped" value={String(manifest.SkippedFrames)} />
                  <Metric label="Analyzable frames" value={String(manifest.FrameResultBlobs.length)} />
                </div>
              ) : null}

              {manifest && manifest.TotalFrames === 0 ? (
                <div className="service-notes" role="status" aria-live="polite">
                  <p className="service-notes-title">No source files were available for this run</p>
                  <p>
                    No supported image/video files were found under {form.containerName}/{form.containerDirectory} at run start.
                  </p>
                  <p>
                    This run is fully completed, but with zero work items. Add a file to that source path and run again.
                  </p>
                </div>
              ) : null}

              <div className="service-notes">
                <p className="service-notes-title">Observed service activity from frame results</p>
                <p>Content Safety observed: {serviceStats.contentSafetyObserved}/{serviceStats.total || 0} frame JSON items.</p>
                <p>
                  OpenAI summary observed: {serviceStats.summaryObserved}/{serviceStats.total || 0}{' '}
                  {form.getSummary ? '(requested)' : '(not requested)'}.
                </p>
                <p>
                  OpenAI child check observed: {serviceStats.childCheckObserved}/{serviceStats.total || 0}{' '}
                  {form.getChildYesNo ? '(requested)' : '(not requested)'}.
                </p>
              </div>

              <div className="filter-row">
                <label>
                  <span>Severity filter</span>
                  <select value={severityFilter} onChange={(event) => setSeverityFilter(event.target.value as 'all' | Severity)}>
                    <option value="all">All</option>
                    <option value="none">None</option>
                    <option value="low">Low</option>
                    <option value="medium">Medium</option>
                    <option value="high">High</option>
                  </select>
                </label>
              </div>

              <div className="result-window">
                {filteredRows.length === 0 ? <p className="help-copy">No frame rows loaded or matching this filter.</p> : null}
                {filteredRows.map((row) => (
                  <article
                    key={row.id}
                    className={`result-card ${selectedResult?.id === row.id ? 'result-card-selected' : ''}`}
                  >
                    <div className="result-card-top">
                      <strong>{row.frame}</strong>
                      <span className={`severity severity-${row.severity}`}>{row.severity} ({row.score})</span>
                    </div>
                    <p>{row.summary || 'No summary returned for this frame.'}</p>
                    <div className="result-card-meta">
                      <span>Child check: {row.childCheck || 'Not requested'}</span>
                      <button
                        type="button"
                        className="detail-link-button"
                        aria-label={`View details for ${row.frame}`}
                        onClick={() => openResultDetail(row.id)}
                      >
                        <span>View details</span>
                        <span className="detail-link-icon" aria-hidden="true">→</span>
                      </button>
                    </div>
                  </article>
                ))}
              </div>

              <div className="button-row">
                <button className="secondary" onClick={resetWizard}>Start new run</button>
                <button className="secondary" onClick={() => setStep('history')}>View history</button>
              </div>
            </div>
          ) : null}

          {step === 'detail' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 6</p>
              <h2>Frame details</h2>

              {selectedResult ? (
                <article className="detail-card" aria-live="polite">
                  <div className="detail-card-top">
                    <h3>{selectedResult.frame}</h3>
                    <span className={`severity severity-${selectedResult.severity}`}>
                      {selectedResult.severity} ({selectedResult.score})
                    </span>
                  </div>
                  <p className="help-copy">Blob path: {selectedResult.frameResultBlobPath}</p>
                  <div className="detail-metrics">
                    <Metric label="Hate" value={String(selectedResult.hate)} />
                    <Metric label="Self-harm" value={String(selectedResult.selfHarm)} />
                    <Metric label="Violence" value={String(selectedResult.violence)} />
                    <Metric label="Sexual" value={String(selectedResult.sexual)} />
                  </div>
                  {selectedResult.imageBase64 ? (
                    <div className="detail-image-wrap">
                      <img
                        className="detail-image"
                        src={toDataUrl(selectedResult.imageBase64)}
                        alt={`Frame preview for ${selectedResult.frame}`}
                        loading="lazy"
                      />
                    </div>
                  ) : (
                    <p className="help-copy">No image payload available for this frame.</p>
                  )}
                  <p>
                    <strong>Content Safety observed:</strong> {selectedResult.contentSafetyObserved ? 'Yes' : 'No'}
                  </p>
                  <p>
                    <strong>Summary observed:</strong> {selectedResult.summaryObserved ? 'Yes' : 'No'}
                  </p>
                  <p>
                    <strong>Child check observed:</strong> {selectedResult.childCheckObserved ? 'Yes' : 'No'}
                  </p>
                  <p>
                    <strong>Child check value:</strong> {selectedResult.childCheck || 'Not requested'}
                  </p>
                  <p>
                    <strong>Summary:</strong> {selectedResult.summary || 'No summary returned for this frame.'}
                  </p>
                </article>
              ) : (
                <p className="help-copy">No frame selected yet. Open a completed run and choose a frame from Results.</p>
              )}

              <div className="button-row">
                <button className="secondary" onClick={() => setStep('results')}>Back to results</button>
                <button className="secondary" onClick={() => setStep('history')}>View history</button>
              </div>
            </div>
          ) : null}

          {step === 'history' ? (
            <div className="wizard-panel fade-in">
              <p className="panel-kicker">Step 7</p>
              <h2>Past run history</h2>
              <p className="help-copy">This list is stored in your browser for quick run recall.</p>

              <div className="result-window">
                {runHistory.length === 0 ? <p className="help-copy">No completed runs in history yet.</p> : null}
                {runHistory.map((item) => (
                  <article key={`${item.runId}-${item.completedAtUtc}`} className="history-card">
                    <div className="result-card-top">
                      <strong>{item.runId}</strong>
                      <span className="severity severity-none">{item.status}</span>
                    </div>
                    <div className="result-card-meta">
                      <span>Container: {item.containerName}/{item.containerDirectory}</span>
                      <span>Completed: {formatDateTime(item.completedAtUtc)}</span>
                    </div>
                    <div className="result-card-meta">
                      <span>Frames: {item.processedFrames}/{item.totalFrames} (ok {item.successfulFrames}, fail {item.failedFrames}, skip {item.skippedFrames})</span>
                      <span>Summary observed: {item.summaryObserved}/{item.analyzableFrames}</span>
                    </div>
                    <div className="result-card-meta">
                      <span>Child observed: {item.childCheckObserved}/{item.analyzableFrames}</span>
                      <span>Content Safety observed: {item.contentSafetyObserved}/{item.analyzableFrames}</span>
                    </div>
                    <div className="button-row">
                      <button className="secondary" disabled={isLoadingHistoryRun} onClick={() => void openHistoryRun(item)}>
                        {isLoadingHistoryRun ? 'Loading...' : 'Open details'}
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            </div>
          ) : null}

          {errorMessage ? <div className="error-banner">{errorMessage}</div> : null}
        </section>

        <section className="panel side-panel">
          <div className="panel-header">
            <div>
              <p className="panel-kicker">Activity</p>
              <h2>Wizard event log</h2>
            </div>
            <span className="subtle-chip">Auto timeline</span>
          </div>

          <div className="log-stream">
            {logItems.length === 0 ? <p className="help-copy">Events will appear after you launch a run.</p> : null}
            {logItems.map((item, index) => (
              <LogEntry key={`${item.at}-${index}`} time={item.at} message={item.message} />
            ))}
          </div>
        </section>
      </main>
    </div>
  );
}

function getStartUrl(): string {
  const base = `${FUNCTION_BASE_URL.replace(/\/$/, '')}${START_PATH.startsWith('/') ? START_PATH : `/${START_PATH}`}`;
  if (!FUNCTION_CODE || base.includes('code=')) {
    return base;
  }

  return `${base}${base.includes('?') ? '&' : '?'}code=${encodeURIComponent(FUNCTION_CODE)}`;
}

async function getJson<T>(url: string): Promise<T> {
  const response = await fetch(url, { method: 'GET' });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Request failed: ${response.status} ${response.statusText} ${text}`);
  }

  return (await response.json()) as T;
}

async function hydrateResults(
  runId: string,
  containerName: string,
  containerDirectory: string,
  setManifest: (manifest: Manifest) => void,
  setRows: (rows: ResultRow[]) => void,
  setErrorMessage: (message: string) => void,
  setLogItems: Dispatch<SetStateAction<LogItem[]>>
): Promise<{ manifest: Manifest; rows: ResultRow[] } | null> {
  try {
    const manifestUrl = await getStorageAccessUrl('read', RESULTS_CONTAINER_NAME, `results/${runId}/job-result.json`);
    const manifest = await getJson<Manifest>(manifestUrl);
    setManifest(manifest);
    addLogEvent(setLogItems, 'results', 'manifest_loaded', `${manifest.FrameResultBlobs.length} frame result path(s)`);

    if (manifest.TotalFrames === 0) {
      addLogEvent(
        setLogItems,
        'orchestration',
        'no_source_files',
        `${containerName}/${containerDirectory}`
      );
    }

    const frameBlobPaths = manifest.FrameResultBlobs.filter((path) => path.toLowerCase().endsWith('.json'));
    const rows = await Promise.all(frameBlobPaths.slice(0, 120).map(async (blobPath, index) => {
      const frameResultUrl = await getStorageAccessUrl('read', RESULTS_CONTAINER_NAME, blobPath);
      const frameData = await getJson<Record<string, unknown>>(frameResultUrl);
      const nestedFrameResult = (frameData.FrameResult && typeof frameData.FrameResult === 'object')
        ? (frameData.FrameResult as Record<string, unknown>)
        : null;
      const framePayload = nestedFrameResult ?? frameData;
      const summaryText = String(framePayload.Summary ?? '');
      const childCheckText = String(framePayload.ChildYesNo ?? '');
      const imageBase64 = String(framePayload.ImageBase64 ?? framePayload.imageBase64 ?? '');
      const contentSafetyObserved =
        framePayload.Hate !== undefined ||
        framePayload.SelfHarm !== undefined ||
        framePayload.Violence !== undefined ||
        framePayload.Sexual !== undefined;
      const summaryObserved =
        summaryText.trim().length > 0 &&
        summaryText.toLowerCase() !== 'not requested' &&
        !summaryText.toLowerCase().startsWith('summary generation failed');
      const childCheckObserved =
        childCheckText.trim().length > 0 &&
        childCheckText.toLowerCase() !== 'not requested' &&
        !childCheckText.toLowerCase().startsWith('failed');
      const maxScore = Math.max(
        safeNumber(framePayload.Hate),
        safeNumber(framePayload.SelfHarm),
        safeNumber(framePayload.Violence),
        safeNumber(framePayload.Sexual)
      );
      const hate = safeNumber(framePayload.Hate);
      const selfHarm = safeNumber(framePayload.SelfHarm);
      const violence = safeNumber(framePayload.Violence);
      const sexual = safeNumber(framePayload.Sexual);

      return {
        id: `${blobPath}-${index}`,
        frame: String(framePayload.Frame ?? frameData.Frame ?? blobPath),
        frameResultBlobPath: blobPath,
        severity: toSeverity(maxScore),
        score: maxScore,
        childCheck: childCheckText,
        summary: summaryText,
        imageBase64,
        hate,
        selfHarm,
        violence,
        sexual,
        contentSafetyObserved,
        summaryObserved,
        childCheckObserved
      } satisfies ResultRow;
    }));

    setRows(rows);
    addLogEvent(setLogItems, 'results', 'frame_rows_loaded', `${rows.length} frame JSON row(s)`);
    addLogEvent(setLogItems, 'service', 'content_safety_observed', `${rows.filter((row) => row.contentSafetyObserved).length}/${rows.length}`);
    addLogEvent(setLogItems, 'service', 'openai_summary_observed', `${rows.filter((row) => row.summaryObserved).length}/${rows.length}`);
    addLogEvent(setLogItems, 'service', 'openai_child_observed', `${rows.filter((row) => row.childCheckObserved).length}/${rows.length}`);
    return { manifest, rows };
  } catch (err) {
    setErrorMessage(getErrorMessage(err));
    return null;
  }
}

async function getStorageAccessUrl(mode: 'upload' | 'read', containerName: string, blobPath: string): Promise<string> {
  const response = await fetch(`${FUNCTION_BASE_URL.replace(/\/$/, '')}${STORAGE_ACCESS_PATH}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ mode, containerName, blobPath })
  });

  if (!response.ok) {
    const text = await response.text();
    throw new Error(`Storage access request failed: ${response.status} ${response.statusText} ${text}`);
  }

  const access = await response.json() as { url?: string; Url?: string };
  const url = access.url ?? access.Url;
  if (!url) {
    throw new Error('Storage access response did not include a URL.');
  }

  return url;
}

function tryParseStartResponse(value: string): StartResponse {
  if (!value.trim()) {
    return {};
  }

  try {
    return JSON.parse(value) as StartResponse;
  } catch {
    return {};
  }
}

function getStatusQueryUri(startData: StartResponse, headers: Headers): string {
  const fromBody =
    startData.statusQueryGetUri ??
    startData.StatusQueryGetUri ??
    startData.statusQueryGetUrl ??
    startData.managementUrls?.statusQueryGetUri ??
    startData.managementUrls?.StatusQueryGetUri ??
    '';

  const value = fromBody || headers.get('location') || '';
  if (!value) {
    return '';
  }

  try {
    const statusUrl = new URL(value);
    const functionUrl = new URL(FUNCTION_BASE_URL || window.location.origin, window.location.origin);
    if (statusUrl.host === functionUrl.host && functionUrl.protocol === 'https:' && statusUrl.protocol === 'http:') {
      statusUrl.protocol = 'https:';
    }
    return statusUrl.toString();
  } catch {
    return value;
  }
}

function getInstanceId(startData: StartResponse, headers: Headers, statusQueryUri = ''): string {
  const value =
    startData.id ??
    startData.instanceId ??
    headers.get('x-ms-durabletask-instance-id') ??
    extractInstanceIdFromStatusUrl(statusQueryUri) ??
    '';
  return value.trim();
}

function extractInstanceIdFromStatusUrl(statusQueryUri: string): string {
  if (!statusQueryUri) {
    return '';
  }

  try {
    const parsed = new URL(statusQueryUri);
    const match = parsed.pathname.match(/\/instances\/([^/]+)/i);
    return match?.[1] ? decodeURIComponent(match[1]) : '';
  } catch {
    const match = statusQueryUri.match(/\/instances\/([^/?#]+)/i);
    return match?.[1] ? decodeURIComponent(match[1]) : '';
  }
}

function appendQueryString(url: string, query: string): string {
  if (!query) {
    return url;
  }

  const trimmed = query.replace(/^\?/, '');
  return `${url}${url.includes('?') ? '&' : '?'}${trimmed}`;
}

function normalizeContainerName(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  try {
    const parsed = new URL(trimmed);
    const firstSegment = parsed.pathname.split('/').filter(Boolean)[0] ?? '';
    return firstSegment || trimmed;
  } catch {
    return trimmed;
  }
}

function getContainerNameError(value: string): string {
  if (!value) {
    return '';
  }

  if (value.length < 3 || value.length > 63) {
    return 'Container name must be 3-63 characters.';
  }

  const isValid = /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/.test(value);
  if (!isValid || value.includes('--')) {
    return 'Container name must be lowercase letters, numbers, and single hyphens only.';
  }

  return '';
}

function normalizeDirectory(value: string): string {
  return value.trim().replace(/^\/+|\/+$/g, '');
}

function sanitizeFileName(value: string): string {
  return value.replace(/[^a-zA-Z0-9._-]/g, '-');
}

function toSeverity(score: number): Severity {
  if (score >= 6) {
    return 'high';
  }

  if (score >= 4) {
    return 'medium';
  }

  if (score >= 1) {
    return 'low';
  }

  return 'none';
}

function addLog(setLogItems: Dispatch<SetStateAction<LogItem[]>>, message: string): void {
  const now = new Date();
  const entry = { at: now.toLocaleTimeString(), message };
  setLogItems((previous) => [entry, ...previous].slice(0, 100));
}

function addLogEvent(
  setLogItems: Dispatch<SetStateAction<LogItem[]>>,
  source: string,
  eventName: string,
  details: string
): void {
  addLog(setLogItems, `[${source.toUpperCase()}] ${eventName}: ${details}`);
}

function safeNumber(value: unknown): number {
  if (typeof value === 'number' && Number.isFinite(value)) {
    return value;
  }

  if (typeof value === 'string') {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  return 0;
}

function getErrorMessage(error: unknown): string {
  if (error instanceof Error) {
    return error.message;
  }

  return 'Unexpected error.';
}

function buildRunHistoryItem(
  runId: string,
  instanceId: string,
  containerName: string,
  containerDirectory: string,
  getSummary: boolean,
  getChildYesNo: boolean,
  manifest: Manifest,
  rows: ResultRow[]
): RunHistoryItem {
  const contentSafetyObserved = rows.filter((row) => row.contentSafetyObserved).length;
  const summaryObserved = rows.filter((row) => row.summaryObserved).length;
  const childCheckObserved = rows.filter((row) => row.childCheckObserved).length;

  return {
    runId,
    instanceId,
    status: manifest.Status,
    startedAtUtc: manifest.StartedAtUtc,
    completedAtUtc: manifest.CompletedAtUtc,
    totalFrames: manifest.TotalFrames,
    processedFrames: manifest.ProcessedFrames,
    successfulFrames: manifest.SuccessfulFrames,
    failedFrames: manifest.FailedFrames,
    skippedFrames: manifest.SkippedFrames,
    analyzableFrames: manifest.FrameResultBlobs.length,
    contentSafetyObserved,
    summaryObserved,
    childCheckObserved,
    getSummary,
    getChildYesNo,
    containerName,
    containerDirectory
  };
}

function upsertRunHistory(
  setHistory: Dispatch<SetStateAction<RunHistoryItem[]>>,
  item: RunHistoryItem
): void {
  setHistory((previous) => {
    const withoutCurrent = previous.filter((entry) => entry.runId !== item.runId);
    const merged = [item, ...withoutCurrent]
      .sort((a, b) => Date.parse(b.completedAtUtc || '') - Date.parse(a.completedAtUtc || ''))
      .slice(0, MAX_HISTORY_ITEMS);
    return merged;
  });
}

function loadRunHistory(): RunHistoryItem[] {
  if (typeof window === 'undefined') {
    return [];
  }

  if (!hasLocalStorage(window)) {
    return [];
  }

  const raw = window.localStorage.getItem(RUN_HISTORY_STORAGE_KEY);
  if (!raw) {
    return [];
  }

  try {
    const parsed = JSON.parse(raw) as RunHistoryItem[];
    if (!Array.isArray(parsed)) {
      return [];
    }

    return parsed.filter((item) => Boolean(item?.runId));
  } catch {
    return [];
  }
}

function hasLocalStorage(value: Window): boolean {
  try {
    return typeof value.localStorage !== 'undefined' && value.localStorage !== null;
  } catch {
    return false;
  }
}

function formatDateTime(value: string): string {
  const parsed = Date.parse(value);
  if (Number.isNaN(parsed)) {
    return 'Unknown';
  }

  return new Date(parsed).toLocaleString();
}

function toDataUrl(value: string): string {
  const trimmed = value.trim();
  if (!trimmed) {
    return '';
  }

  if (trimmed.startsWith('data:image/')) {
    return trimmed;
  }

  return `data:image/jpeg;base64,${trimmed}`;
}

function ProgressBar({
  label,
  value,
  accent = false,
  isActive = false
}: {
  label?: string;
  value: number;
  accent?: boolean;
  isActive?: boolean;
}) {
  const safeValue = Math.min(100, Math.max(0, value));
  const animated = isActive && safeValue < 100;
  return (
    <div className="progress-block">
      {label ? <span className="progress-label">{label}</span> : null}
      <div className={`progress-track ${accent ? 'progress-track-accent' : ''} ${animated ? 'progress-track-running' : ''}`}>
        <div className={`progress-fill ${animated ? 'progress-fill-running' : ''}`} style={{ width: `${safeValue}%` }} />
      </div>
      <span className="progress-value">{safeValue}%{animated ? ' • running' : ''}</span>
    </div>
  );
}

function Metric({ label, value, compact = false }: { label: string; value: string; compact?: boolean }) {
  return (
    <div className={`metric-card ${compact ? 'metric-compact' : ''}`}>
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

function ToggleCard({
  label,
  description,
  checked,
  onChange
}: {
  label: string;
  description: string;
  checked: boolean;
  onChange: (value: boolean) => void;
}) {
  return (
    <label className="toggle-card">
      <input type="checkbox" checked={checked} onChange={(event) => onChange(event.target.checked)} />
      <div>
        <strong>{label}</strong>
        <p>{description}</p>
      </div>
    </label>
  );
}

export default App;