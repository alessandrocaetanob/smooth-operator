import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  OnDestroy,
  Output,
  ViewChild,
  inject,
  signal,
} from '@angular/core';
import Guacamole from 'guacamole-common-js';
import { Recording, RecordingsService } from '../../services/recordings.service';

/**
 * Modal drawer for in-browser playback of a captured .guac recording.
 *
 * Why we DON'T pass the Blob directly to Guacamole.SessionRecording:
 * guacamole-common-js@1.5.0 has a bug where the Blob branch never assigns
 * `recordingBlob = source`, so the internal parseBlob() call receives undefined
 * and the player throws. The supported workaround is to use the Tunnel branch,
 * which correctly initialises the internal buffer. We wrap our pre-fetched blob
 * in a minimal in-memory Tunnel that emits parsed instructions on connect().
 */
@Component({
  selector: 'app-recording-player',
  standalone: true,
  imports: [CommonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="fixed inset-0 z-50 flex bg-black/60 backdrop-blur-sm"
      role="presentation"
      (click)="onBackdropClick($event)"
    >
      <!-- Drawer panel: right-side when normal, full viewport when fullscreen -->
      <div
        [class]="
          'ml-auto h-full w-full flex flex-col bg-surface shadow-2xl border-l border-outline-variant/30 ' +
          (fullscreen() ? 'max-w-full' : 'max-w-3xl')
        "
        role="dialog"
        aria-modal="true"
        aria-labelledby="recording-player-title"
        tabindex="-1"
        (click)="$event.stopPropagation()"
        (keydown)="$event.stopPropagation()"
      >
        <header
          class="flex items-center justify-between px-4 py-3 border-b border-outline-variant/30 shrink-0"
        >
          <div class="min-w-0">
            <h2
              id="recording-player-title"
              class="text-base font-semibold text-on-surface truncate"
            >
              {{ recording?.connectionName }}
            </h2>
            <p class="text-xs text-on-surface-variant truncate">
              {{ recording?.startedAt | date: 'medium' }} · session {{ recording?.sessionId }}
            </p>
          </div>
          <div class="flex items-center gap-1 shrink-0">
            <button
              type="button"
              class="btn-ghost-icon"
              (click)="toggleFullscreen()"
              [attr.aria-label]="fullscreen() ? 'Exit fullscreen' : 'Fullscreen'"
              [title]="fullscreen() ? 'Exit fullscreen' : 'Fullscreen'"
            >
              <span class="material-symbols-outlined">
                {{ fullscreen() ? 'close_fullscreen' : 'open_in_full' }}
              </span>
            </button>
            <button
              type="button"
              class="btn-ghost-icon"
              (click)="close()"
              aria-label="Close player"
              title="Close (Esc)"
            >
              <span class="material-symbols-outlined">close</span>
            </button>
          </div>
        </header>

        <div class="flex-1 min-h-0 overflow-auto px-4 py-3 space-y-3">
          @if (loading()) {
            <div class="text-sm text-on-surface-variant flex items-center gap-2">
              <span class="material-symbols-outlined animate-spin text-base">
                progress_activity
              </span>
              Loading recording…
            </div>
          }
          @if (error()) {
            <div class="feedback-banner feedback-error">
              <span class="material-symbols-outlined text-base">error</span>
              <div class="flex-1">
                <p class="font-medium">In-browser playback failed</p>
                <p class="text-xs opacity-80 mt-1">{{ error() }}</p>
                <button type="button" class="btn-secondary mt-3" (click)="downloadGuac()">
                  <span class="material-symbols-outlined text-base">download</span>
                  Download raw .guac file
                </button>
              </div>
            </div>
          }
          @if (exporting()) {
            <div class="feedback-banner feedback-info">
              <span class="material-symbols-outlined animate-spin text-base"
                >progress_activity</span
              >
              <span>
                Exporting video — playing the recording in the background so the canvas can be
                captured. This takes the same time as the recording duration.
              </span>
            </div>
          }

          <div
            #displayContainer
            class="bg-black rounded overflow-auto flex items-center justify-center min-h-[320px]"
            [class.hidden]="!!error()"
          ></div>
        </div>

        @if (!error()) {
          <div
            class="px-4 py-3 border-t border-outline-variant/30 shrink-0 flex flex-wrap items-center gap-3"
          >
            <button
              type="button"
              class="btn-secondary"
              (click)="togglePlay()"
              [disabled]="!ready() || exporting()"
            >
              <span class="material-symbols-outlined text-base">
                {{ playing() ? 'pause' : 'play_arrow' }}
              </span>
              {{ playing() ? 'Pause' : 'Play' }}
            </button>
            <span class="text-xs text-on-surface-variant tabular-nums shrink-0">
              {{ positionLabel() }} / {{ durationLabel() }}
            </span>
            <input
              type="range"
              class="flex-1 min-w-[120px] accent-primary"
              min="0"
              [max]="duration()"
              [value]="position()"
              [disabled]="!ready() || exporting()"
              (input)="seek($any($event.target).valueAsNumber)"
              aria-label="Seek"
            />
            <button
              type="button"
              class="btn-secondary"
              (click)="exportVideo()"
              [disabled]="!ready() || exporting()"
              [title]="exportFormatLabel()"
            >
              <span class="material-symbols-outlined text-base">movie</span>
              Export {{ exportFormatLabel() }}
            </button>
            <button
              type="button"
              class="btn-ghost-icon"
              (click)="downloadGuac()"
              aria-label="Download raw .guac file"
              title="Download raw .guac"
            >
              <span class="material-symbols-outlined">download</span>
            </button>
          </div>
        }
      </div>
    </div>
  `,
})
export class RecordingPlayerComponent implements OnDestroy {
  private readonly recordings = inject(RecordingsService);

  @Input({ required: true }) recording: Recording | null = null;
  @Output() readonly closed = new EventEmitter<void>();

  @ViewChild('displayContainer', { static: false })
  set displayContainer(host: ElementRef<HTMLDivElement> | undefined) {
    if (host && !this.hostEl) {
      this.hostEl = host.nativeElement;
      if (this.recording) this.loadAndAttach(this.recording.id);
    }
  }

  private hostEl: HTMLDivElement | null = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private displayEl: any | null = null;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  private session: any | null = null;
  private positionTimer: number | null = null;
  private recorder: MediaRecorder | null = null;
  private recordedChunks: Blob[] = [];
  private exportPollTimer: number | null = null;
  private compositeCanvas: HTMLCanvasElement | null = null;
  private compositeCtx: CanvasRenderingContext2D | null = null;
  private compositeRaf: number | null = null;
  private readonly preferredExportMime = pickPreferredExportMime();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly ready = signal(false);
  readonly playing = signal(false);
  readonly fullscreen = signal(false);
  readonly exporting = signal(false);
  readonly duration = signal(0);
  readonly position = signal(0);

  positionLabel(): string {
    return this.fmt(this.position());
  }
  durationLabel(): string {
    return this.fmt(this.duration());
  }
  exportFormatLabel(): string {
    return this.preferredExportMime.includes('mp4') ? 'MP4' : 'WebM';
  }

  private fmt(ms: number): string {
    const total = Math.floor(ms / 1000);
    const m = Math.floor(total / 60);
    const s = total % 60;
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  toggleFullscreen(): void {
    this.fullscreen.update((v) => !v);
  }

  togglePlay(): void {
    if (!this.session) return;
    if (this.playing()) {
      this.session.pause();
      this.playing.set(false);
    } else {
      this.session.play();
      this.playing.set(true);
    }
  }

  seek(pos: number): void {
    if (!this.session) return;
    this.session.seek(pos, () => this.position.set(pos));
  }

  downloadGuac(): void {
    if (!this.recording) return;
    const link = document.createElement('a');
    link.href = this.recordings.streamUrl(this.recording.id);
    link.download = `${this.recording.sessionId}.guac`;
    document.body.appendChild(link);
    link.click();
    link.remove();
  }

  /**
   * Plays the recording from start to end while a MediaRecorder captures a
   * private canvas that we composite the Guacamole display into every frame.
   *
   * Why we composite instead of grabbing `display.getDefaultLayer().getCanvas()`
   * directly: Guacamole.Display is a stack of layered canvases (default layer,
   * cursor, scratch buffers) positioned in a DIV. `captureStream` only sees
   * paints on the single canvas it's attached to, so when content lives across
   * layers — or when Guacamole's internal flush cadence happens to skip the
   * default layer between frames — the output renders as a black video.
   * Drawing every layer into a fresh canvas on requestAnimationFrame gives the
   * recorder a steady stream of fully-composited frames.
   */
  exportVideo(): void {
    if (!this.session || !this.recording || this.exporting()) return;
    if (typeof MediaRecorder === 'undefined') {
      this.error.set(
        'This browser does not support video export. Download the .guac file instead.',
      );
      return;
    }
    if (!this.displayEl) {
      this.error.set('Recording display not ready yet — wait for load to finish, then try again.');
      return;
    }

    const { width, height } = this.measureDisplay();
    if (width === 0 || height === 0) {
      this.error.set('Recording has no visible frame yet — play it once before exporting.');
      return;
    }

    this.exporting.set(true);
    this.recordedChunks = [];

    const composite = document.createElement('canvas');
    composite.width = width;
    composite.height = height;
    const ctx = composite.getContext('2d');
    if (!ctx) {
      this.exporting.set(false);
      this.error.set('Failed to acquire 2D canvas context for export.');
      return;
    }
    this.compositeCanvas = composite;
    this.compositeCtx = ctx;

    let stream: MediaStream;
    try {
      stream = composite.captureStream(30);
    } catch (err) {
      this.cleanupExport();
      this.error.set('captureStream is not available: ' + (err as Error).message);
      return;
    }

    const recorder = new MediaRecorder(stream, {
      mimeType: this.preferredExportMime,
      // Explicit bitrate so the muxer doesn't pick a too-low default for
      // mostly-static screen content (which is what produces "valid file,
      // black playback" symptoms in MP4 viewers).
      videoBitsPerSecond: 4_000_000,
    });
    this.recorder = recorder;
    const recordingForFile = this.recording;
    recorder.ondataavailable = (e) => {
      if (e.data && e.data.size > 0) this.recordedChunks.push(e.data);
    };
    recorder.onstop = () => {
      const blob = new Blob(this.recordedChunks, { type: this.preferredExportMime });
      const ext = this.preferredExportMime.includes('mp4') ? 'mp4' : 'webm';
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `${recordingForFile.sessionId}.${ext}`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
      this.cleanupExport();
    };

    // Seek to start, kick off the compositor, start the recorder, then play.
    this.session.seek(0, () => {
      this.position.set(0);
      // Paint one frame immediately so the recorder's first chunk is non-empty.
      this.compositeFrame();
      this.startCompositeLoop();
      try {
        // 250 ms timeslice so chunks arrive incrementally — gives us partial
        // output even if the user closes the tab mid-export.
        recorder.start(250);
      } catch (err) {
        this.cleanupExport();
        this.error.set('Failed to start recorder: ' + (err as Error).message);
        return;
      }
      this.session.play();
      this.playing.set(true);
      this.startExportPolling();
    });
  }

  private measureDisplay(): { width: number; height: number } {
    const w = Number(this.session?.getWidth?.()) || 0;
    const h = Number(this.session?.getHeight?.()) || 0;
    if (w > 0 && h > 0) return { width: w, height: h };
    // Fallback: pull dimensions from the Guacamole display wrapper element.
    const root = this.displayEl as HTMLElement | null;
    return {
      width: root?.offsetWidth ?? 0,
      height: root?.offsetHeight ?? 0,
    };
  }

  private startCompositeLoop(): void {
    this.stopCompositeLoop();
    const tick = () => {
      if (!this.exporting()) return;
      this.compositeFrame();
      this.compositeRaf = window.requestAnimationFrame(tick);
    };
    this.compositeRaf = window.requestAnimationFrame(tick);
  }

  private stopCompositeLoop(): void {
    if (this.compositeRaf != null) {
      window.cancelAnimationFrame(this.compositeRaf);
      this.compositeRaf = null;
    }
  }

  private compositeFrame(): void {
    const ctx = this.compositeCtx;
    const canvas = this.compositeCanvas;
    const root = this.displayEl as HTMLElement | null;
    if (!ctx || !canvas || !root) return;
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    // Walk every <canvas> the Guacamole display has stacked inside its wrapper
    // and draw them in DOM order. Layer offsets live on the parent <div>'s
    // inline styles (Guacamole positions each layer absolutely).
    const layers = root.querySelectorAll('canvas');
    layers.forEach((layer) => {
      const parent = layer.parentElement as HTMLElement | null;
      const x = parent ? parseFloat(parent.style.left || '0') || 0 : 0;
      const y = parent ? parseFloat(parent.style.top || '0') || 0 : 0;
      try {
        ctx.drawImage(layer, x, y);
      } catch {
        /* layer might be tainted or zero-sized — skip */
      }
    });
  }

  private cleanupExport(): void {
    this.stopExportPolling();
    this.stopCompositeLoop();
    this.compositeCanvas = null;
    this.compositeCtx = null;
    this.recordedChunks = [];
    this.recorder = null;
    this.exporting.set(false);
  }

  private startExportPolling(): void {
    this.stopExportPolling();
    this.exportPollTimer = window.setInterval(() => {
      if (!this.exporting() || !this.recorder) return;
      // Stop once the player has reached the recording's end.
      if (this.duration() > 0 && this.position() >= this.duration()) {
        try {
          this.recorder.stop();
        } catch {
          /* already stopped */
        }
      }
    }, 250);
  }

  private stopExportPolling(): void {
    if (this.exportPollTimer != null) {
      window.clearInterval(this.exportPollTimer);
      this.exportPollTimer = null;
    }
  }

  onBackdropClick(event: MouseEvent): void {
    if (event.target === event.currentTarget) this.close();
  }

  @HostListener('document:keydown.escape')
  onEsc(): void {
    if (this.fullscreen()) {
      this.fullscreen.set(false);
      return;
    }
    this.close();
  }

  close(): void {
    this.cleanup();
    this.closed.emit();
  }

  ngOnDestroy(): void {
    this.cleanup();
  }

  private cleanup(): void {
    if (this.positionTimer != null) {
      window.clearInterval(this.positionTimer);
      this.positionTimer = null;
    }
    try {
      this.recorder?.stop();
    } catch {
      /* already stopped */
    }
    this.cleanupExport();
    try {
      this.session?.pause?.();
      this.session?.disconnect?.();
    } catch {
      /* swallow — closing anyway */
    }
    this.session = null;
    this.displayEl = null;
  }

  private clearHost(host: HTMLDivElement): void {
    while (host.firstChild) host.removeChild(host.firstChild);
  }

  private loadAndAttach(id: string): void {
    this.loading.set(true);
    this.error.set(null);

    this.recordings.fetchStream(id).subscribe({
      next: (buffer) => this.initPlayer(buffer),
      error: (err) => {
        this.error.set(this.toMessage(err) || 'Failed to fetch recording.');
        this.loading.set(false);
      },
    });
  }

  private initPlayer(buffer: ArrayBuffer): void {
    try {
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const G = Guacamole as any;
      const blob = new Blob([buffer], { type: 'application/octet-stream' });

      // Bug workaround: feed via Tunnel branch, not Blob branch.
      const tunnel = new BlobReplayTunnel(G, blob);
      const session = new G.SessionRecording(tunnel);
      this.session = session;

      const display = session.getDisplay();
      const displayEl = display.getElement?.() ?? display.getDefaultLayer?.()?.getCanvas?.();
      if (displayEl && this.hostEl) {
        this.clearHost(this.hostEl);
        this.hostEl.appendChild(displayEl);
      }
      // Cache the display wrapper element for video export — we composite all
      // of its child layer canvases into a single capture canvas on RAF.
      this.displayEl = displayEl ?? null;

      session.onload = () => {
        this.loading.set(false);
        this.ready.set(true);
        this.duration.set(session.getDuration());
      };
      session.onerror = (msg: string) => {
        this.error.set(msg || 'Recording playback error.');
        this.loading.set(false);
      };
      session.onprogress = (duration: number) => this.duration.set(duration);
      session.onseek = (pos: number) => this.position.set(pos);
      session.onpause = () => this.playing.set(false);
      session.onplay = () => this.playing.set(true);

      this.positionTimer = window.setInterval(() => {
        if (this.playing()) {
          this.position.update((p) => Math.min(p + 250, this.duration()));
        }
      }, 250);

      // Kick the tunnel into reading (Guacamole's tunnel API).
      tunnel.connect('');
    } catch (err) {
      this.error.set('Failed to initialise player: ' + (err as Error).message);
      this.loading.set(false);
    }
  }

  private toMessage(err: unknown): string {
    if (!err) return '';
    const anyErr = err as { error?: { message?: string }; message?: string };
    return anyErr.error?.message ?? anyErr.message ?? '';
  }
}

/**
 * Picks the best MediaRecorder output format the current browser supports.
 * MP4 first (Chrome 130+ on desktop), then WebM/VP9, then WebM default. WebM
 * is universally writable; users can transcode externally with ffmpeg if MP4
 * is required by their compliance tooling.
 */
function pickPreferredExportMime(): string {
  const candidates = [
    'video/mp4;codecs=avc1.42E01F',
    'video/webm;codecs=vp9',
    'video/webm;codecs=vp8',
    'video/webm',
  ];
  if (typeof MediaRecorder === 'undefined') return 'video/webm';
  for (const mime of candidates) {
    try {
      if (MediaRecorder.isTypeSupported(mime)) return mime;
    } catch {
      /* fall through */
    }
  }
  return 'video/webm';
}

/**
 * Minimal in-memory tunnel that satisfies the Guacamole.Tunnel duck-typed
 * interface used by SessionRecording's tunnel branch. Reads the supplied .guac
 * blob once, parses it with Guacamole.Parser, and emits each instruction
 * synchronously via `oninstruction` — then transitions to CLOSED so the
 * SessionRecording fires `onload` and becomes playable.
 *
 * This sidesteps the v1.5.0 bug where the Blob branch passes an unassigned
 * `recordingBlob` to parseBlob.
 */
class BlobReplayTunnel {
  oninstruction: ((opcode: string, args: string[]) => void) | null = null;
  onerror: ((err: unknown) => void) | null = null;
  onstatechange: ((state: number) => void) | null = null;
  receiveTimeout = 0;
  unstableTimeout = 0;
  uuid: string | null = null;
  state: number;

  constructor(
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    private readonly G: any,
    private readonly blob: Blob,
  ) {
    this.state = G.Tunnel?.State?.CONNECTING ?? 1;
  }

  isConnected(): boolean {
    return this.state === (this.G.Tunnel?.State?.OPEN ?? 0);
  }

  async connect(_data: string): Promise<void> {
    this.setState(this.G.Tunnel.State.OPEN);
    try {
      const text = await this.blob.text();
      const parser = new this.G.Parser();
      parser.oninstruction = (opcode: string, args: string[]) => {
        this.oninstruction?.(opcode, args);
      };
      parser.receive(text);
      this.setState(this.G.Tunnel.State.CLOSED);
    } catch (err) {
      this.onerror?.({ message: (err as Error).message ?? 'Parse error' });
      this.setState(this.G.Tunnel.State.CLOSED);
    }
  }

  disconnect(): void {
    this.setState(this.G.Tunnel.State.CLOSED);
  }

  sendMessage(): void {
    /* no-op — playback tunnel doesn't accept upstream input */
  }

  private setState(state: number): void {
    if (this.state === state) return;
    this.state = state;
    this.onstatechange?.(state);
  }
}
