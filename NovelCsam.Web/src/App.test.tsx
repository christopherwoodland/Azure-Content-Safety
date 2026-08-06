import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import App from './App';

describe('App wizard flow', () => {
  beforeEach(() => {
    vi.spyOn(globalThis.crypto, 'randomUUID').mockReturnValue('00000000-0000-4000-8000-000000000123');
    vi.spyOn(globalThis, 'fetch').mockImplementation((input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);

      if (url.includes('/api/AnalyzeFrames_HttpStart')) {
        return Promise.resolve(
          new Response(
            JSON.stringify({
              id: 'instance-1',
              statusQueryGetUri: 'http://localhost:7092/runtime/webhooks/durabletask/instances/instance-1'
            }),
            {
              status: 202,
              headers: { 'Content-Type': 'application/json' }
            }
          )
        );
      }

      if (url.includes('/runtime/webhooks/durabletask/instances/instance-1')) {
        return Promise.resolve(
          new Response(JSON.stringify({ runtimeStatus: 'Running' }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
          })
        );
      }

      if (url.includes('/api/storage/access')) {
        return Promise.resolve(
          new Response(JSON.stringify({ Url: 'https://storage.example/upload-target' }), {
            status: 200,
            headers: { 'Content-Type': 'application/json' }
          })
        );
      }

      if (url === 'https://storage.example/upload-target') {
        return Promise.resolve(new Response(null, { status: 201 }));
      }

      return Promise.resolve(new Response('{}', { status: 200, headers: { 'Content-Type': 'application/json' } }));
    });
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('renders setup screen and enforces required fields', async () => {
    render(<App />);

    expect(screen.getByRole('heading', { name: /guided wizard for durable video safety analysis/i })).toBeInTheDocument();

    const continueButton = screen.getByRole('button', { name: /continue/i });
    expect(continueButton).toBeEnabled();

    const containerNameInput = screen.getByLabelText(/container name/i);
    await userEvent.clear(containerNameInput);

    expect(continueButton).toBeDisabled();

    const archiveToggle = screen.getByLabelText(/archive input files after success/i);
    expect(archiveToggle).toBeChecked();
  });

  it('moves to review step and starts analysis with expected payload', async () => {
    render(<App />);

    await userEvent.clear(screen.getByLabelText(/frame interval in seconds/i));
    await userEvent.type(screen.getByLabelText(/frame interval in seconds/i), '5');
    await userEvent.click(screen.getByRole('button', { name: /continue/i }));

    expect(screen.getByRole('heading', { name: /upload videos to the evaluation container/i })).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /continue to launch/i }));

    expect(screen.getByRole('heading', { name: /review payload and launch durable flow/i })).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: /start analysis/i }));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        'http://localhost:7092/api/AnalyzeFrames_HttpStart',
        expect.objectContaining({ method: 'POST' })
      );
    });

    const startCall = vi.mocked(globalThis.fetch).mock.calls.find((call) => String(call[0]).includes('/api/AnalyzeFrames_HttpStart'));
    expect(startCall).toBeDefined();

    const body = JSON.parse(String(startCall?.[1]?.body ?? '{}')) as Record<string, unknown>;
    expect(body).toMatchObject({
      containerName: 'videos',
      containerDirectory: 'input',
      extractedFramesDirectory: 'extracted',
      frameIntervalSeconds: 5,
      runId: '00000000-0000-4000-8000-000000000123',
      getChildYesNo: true,
      getSummary: true,
      imageBase64ToDB: true,
      archiveSourceOnSuccess: true
    });

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /live durable orchestration status/i })).toBeInTheDocument();
    });

    expect(screen.getByText(/\[DURABLE\] submitted: runId=00000000-0000-4000-8000-000000000123/i)).toBeInTheDocument();
  });

  it('uploads a selected video when storage access returns PascalCase Url', async () => {
    render(<App />);

    await userEvent.click(screen.getByRole('button', { name: /continue/i }));
    const file = new File(['video-data'], 'sample.mp4', { type: 'video/mp4' });
    await userEvent.upload(screen.getByLabelText(/video files/i), file);
    await userEvent.click(screen.getByRole('button', { name: /upload selected/i }));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        'https://storage.example/upload-target',
        expect.objectContaining({ method: 'PUT', body: file })
      );
    });

    expect(await screen.findByText(/input\/00000000-0000-4000-8000-000000000123-sample.mp4/i)).toBeInTheDocument();
  });

  it('sends archiveSourceOnSuccess=false when archive toggle is disabled', async () => {
    render(<App />);

    await userEvent.click(screen.getByLabelText(/archive input files after success/i));
    await userEvent.click(screen.getByRole('button', { name: /continue/i }));
    await userEvent.click(screen.getByRole('button', { name: /continue to launch/i }));
    await userEvent.click(screen.getByRole('button', { name: /start analysis/i }));

    await waitFor(() => {
      expect(globalThis.fetch).toHaveBeenCalledWith(
        'http://localhost:7092/api/AnalyzeFrames_HttpStart',
        expect.objectContaining({ method: 'POST' })
      );
    });

    const startCall = vi.mocked(globalThis.fetch).mock.calls.find((call) => String(call[0]).includes('/api/AnalyzeFrames_HttpStart'));
    expect(startCall).toBeDefined();

    const body = JSON.parse(String(startCall?.[1]?.body ?? '{}')) as Record<string, unknown>;
    expect(body.archiveSourceOnSuccess).toBe(false);
  });
});
