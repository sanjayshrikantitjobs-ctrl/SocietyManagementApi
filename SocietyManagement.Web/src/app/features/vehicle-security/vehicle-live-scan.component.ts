import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnDestroy, OnInit, output, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { createWorker, PSM, type Worker } from 'tesseract.js';

export interface LiveScanAcceptedEvent {
  normalizedText: string;
  recognizedText: string;
  confidence: number;
  imageBase64: string;
}

/** Mirrors the backend's (now-removed) VehicleNumberNormalizer.Normalize —
 * strips everything but letters/digits and uppercases. */
function normalizePlate(text: string): string {
  return text.toUpperCase().replace(/[^A-Z0-9]/g, '');
}

/** Loose plausibility filter for an Indian plate, e.g. "MH04AB1234" or
 * "MH04A1234" — 2 state letters, 1-2 RTO digits, 1-3 series letters, 4
 * number digits. Deliberately permissive: this only gates which OCR reads
 * are worth counting toward consensus, never whether a plate is genuine —
 * the actual match/no-match decision is always the existing DB search. */
const PLATE_FORMAT = /^[A-Z]{2}\d{1,2}[A-Z]{1,3}\d{4}$/;

/** How many of the last WINDOW_SIZE reads must agree before we accept a
 * plate and stop scanning — a simple majority-vote debounce so one garble
 * frame can't misfire, without waiting for a long unanimous streak. */
const CONSENSUS_THRESHOLD = 2;
const WINDOW_SIZE = 4;
const SCAN_INTERVAL_MS = 900;

/** Continuous live-camera plate scan — replaces the old capture-photo ->
 * drag-to-crop -> server-OCR flow. OCR now runs entirely client-side via
 * Tesseract.js (WASM, free, no server deployment/native-binary dependency),
 * against the region marked by the on-screen guide box, once every
 * SCAN_INTERVAL_MS. A reading only counts once it plausibly looks like a
 * plate (PLATE_FORMAT) and is accepted only once the same normalized text
 * has come up CONSENSUS_THRESHOLD times in the last WINDOW_SIZE attempts —
 * this is what stands in for the old perspective-warp+sharpen pipeline's
 * accuracy: no single frame has to be perfect. Purely advisory like the
 * server flow was: the caller always gets the field back editable. */
@Component({
  selector: 'app-vehicle-live-scan',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    <div class="live-scan">
      <div class="video-frame">
        <video #video autoplay playsinline muted></video>
        <div class="guide-box"></div>
        <canvas #canvas hidden></canvas>
      </div>

      @if (error()) {
        <p class="status error">{{ error() }}</p>
      } @else {
        <div class="status">
          <mat-spinner diameter="16" />
          <span>{{ statusText() }}</span>
        </div>
      }

      <p class="hint">Line up the plate inside the box — it fills in automatically.</p>
      <div class="actions">
        <button mat-button type="button" (click)="cancelled.emit()">Enter Manually Instead</button>
      </div>
    </div>
  `,
  styles: [`
    .live-scan { display: flex; flex-direction: column; align-items: center; gap: 12px; width: 100%; }
    .video-frame { position: relative; width: 100%; aspect-ratio: 4 / 3; border-radius: 12px; overflow: hidden; background: #000; }
    .video-frame video { width: 100%; height: 100%; object-fit: cover; display: block; }
    .guide-box { position: absolute; left: 10%; right: 10%; top: 40%; height: 20%;
      border: 2px solid #fff; border-radius: 6px; box-shadow: 0 0 0 999px rgba(0,0,0,0.35); pointer-events: none; }
    .status { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--app-text-muted); min-height: 20px; }
    .status.error { color: #b91c1c; }
    .hint { text-align: center; font-size: 12px; color: var(--app-text-muted); margin: 0; }
    .actions { display: flex; justify-content: center; }
  `]
})
export class VehicleLiveScanComponent implements OnInit, OnDestroy {
  accepted = output<LiveScanAcceptedEvent>();
  cancelled = output<void>();

  private readonly videoRef = viewChild.required<ElementRef<HTMLVideoElement>>('video');
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  readonly statusText = signal('Starting camera…');
  readonly error = signal<string | null>(null);

  private stream: MediaStream | null = null;
  private worker: Worker | null = null;
  private timer: ReturnType<typeof setInterval> | null = null;
  private busy = false;
  private stopped = false;
  private readonly recentReads: string[] = [];

  async ngOnInit(): Promise<void> {
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({
        video: { facingMode: 'environment', width: { ideal: 1280 }, height: { ideal: 720 } }
      });
    } catch {
      this.error.set("Couldn't access the camera — check permissions, or enter the plate manually.");
      return;
    }
    if (this.stopped) { this.releaseCamera(); return; }

    this.videoRef().nativeElement.srcObject = this.stream;
    this.statusText.set('Reading plate…');

    this.worker = await createWorker('eng');
    if (this.stopped) { await this.worker.terminate(); return; }
    await this.worker.setParameters({
      tessedit_char_whitelist: 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789',
      tessedit_pageseg_mode: PSM.SINGLE_LINE
    });

    this.timer = setInterval(() => this.tick(), SCAN_INTERVAL_MS);
  }

  ngOnDestroy(): void {
    this.stopped = true;
    if (this.timer) clearInterval(this.timer);
    this.releaseCamera();
    this.worker?.terminate();
  }

  private releaseCamera(): void {
    this.stream?.getTracks().forEach((t) => t.stop());
    this.stream = null;
  }

  private async tick(): Promise<void> {
    if (this.busy || !this.worker) return;
    const video = this.videoRef().nativeElement;
    if (video.videoWidth === 0) return;

    this.busy = true;
    try {
      const guideCanvas = this.extractGuideRegion(video);
      const { data } = await this.worker.recognize(guideCanvas);
      const recognizedText = (data.text || '').trim();
      const normalizedText = normalizePlate(recognizedText);

      if (PLATE_FORMAT.test(normalizedText)) {
        this.recentReads.push(normalizedText);
        if (this.recentReads.length > WINDOW_SIZE) this.recentReads.shift();

        const agreeing = this.recentReads.filter((r) => r === normalizedText).length;
        if (agreeing >= CONSENSUS_THRESHOLD) {
          this.acceptPlate(normalizedText, recognizedText, data.confidence, video);
          return;
        }
        this.statusText.set(`Reading plate… (${normalizedText})`);
      } else {
        this.statusText.set('Reading plate…');
      }
    } catch {
      // A single failed/garbled frame is expected often enough — just try
      // again on the next tick, same as the server flow's own quiet-failure
      // stance for a bad read.
    } finally {
      this.busy = false;
    }
  }

  private acceptPlate(normalizedText: string, recognizedText: string, confidence: number, video: HTMLVideoElement): void {
    if (this.timer) clearInterval(this.timer);
    this.timer = null;

    const fullFrame = document.createElement('canvas');
    fullFrame.width = video.videoWidth;
    fullFrame.height = video.videoHeight;
    fullFrame.getContext('2d')!.drawImage(video, 0, 0);
    const imageBase64 = fullFrame.toDataURL('image/jpeg', 0.85).split(',')[1];

    this.releaseCamera();
    this.accepted.emit({ normalizedText, recognizedText, confidence, imageBase64 });
  }

  /** Crops the video frame to the on-screen guide box (same relative
   * position/size as the .guide-box overlay), then upscales + grayscales +
   * boosts contrast — a lightweight stand-in for the old server pipeline's
   * perspective-warp/sharpen steps, cheap enough to run every tick. */
  private extractGuideRegion(video: HTMLVideoElement): HTMLCanvasElement {
    const vw = video.videoWidth;
    const vh = video.videoHeight;
    const sx = vw * 0.1;
    const sy = vh * 0.4;
    const sw = vw * 0.8;
    const sh = vh * 0.2;

    const canvas = this.canvasRef().nativeElement;
    const scale = 3;
    canvas.width = sw * scale;
    canvas.height = sh * scale;

    const ctx = canvas.getContext('2d')!;
    ctx.filter = 'grayscale(1) contrast(1.5)';
    ctx.drawImage(video, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
    return canvas;
  }
}
