import { CommonModule } from '@angular/common';
import {
  AfterViewInit, Component, ElementRef, HostListener, input, output, signal, viewChild
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';

interface Point { x: number; y: number; }

/** Hand-rolled 4-point perspective-crop overlay — deliberately not a library
 * (ngx-image-cropper's peer-dependency range is a real risk at this app's
 * Angular ^22.0.0 pin, and it only does axis-aligned rectangles anyway).
 * The user drags each corner independently onto the plate's actual corners
 * (order TopLeft/TopRight/BottomRight/BottomLeft) — plates are rarely
 * photographed dead-on, so a plain rectangle crop leaves the text itself
 * slanted, which hurts OCR. VehiclePlateOcrService perspective-warps
 * whatever quad the user marks, straightening the text before OCR runs. */
@Component({
  selector: 'app-plate-crop',
  standalone: true,
  imports: [CommonModule, MatButtonModule],
  template: `
    <div class="crop-frame">
      <!-- Shrink-wraps exactly to the image's own rendered size (JS-set,
           not CSS-derived) so absolutely-positioned children below sit in
           the SAME coordinate space as corner.x/corner.y — no separate
           "image is centered within a wider frame" offset to account for. -->
      <div class="img-wrap" [style.width.px]="imgSize().w" [style.height.px]="imgSize().h">
        <img #img [src]="imageUrl()" draggable="false" alt="" />
        @if (ready()) {
          <svg class="overlay" [attr.width]="imgSize().w" [attr.height]="imgSize().h">
            <polygon [attr.points]="polygonPoints()" />
          </svg>
          @for (corner of corners(); track $index; let i = $index) {
            <div class="handle" [style.left.px]="corner.x" [style.top.px]="corner.y"
                 (pointerdown)="startDrag($event, i)"></div>
          }
        }
      </div>
    </div>
    <p class="hint">Drag each corner onto the plate.</p>
    <div class="crop-actions">
      <button mat-button type="button" (click)="skip.emit()">Skip</button>
      <button mat-flat-button color="primary" type="button" [disabled]="!ready()" (click)="confirmCrop()">Use This Region</button>
    </div>
  `,
  styles: [`
    .crop-frame { width: 100%; max-height: 320px; overflow: hidden; border-radius: 12px;
      background: var(--app-surface-alt); display: flex; align-items: center; justify-content: center; touch-action: none; }
    .img-wrap { position: relative; }
    .img-wrap img { display: block; width: 100%; height: 100%; user-select: none; }
    .overlay { position: absolute; top: 0; left: 0; pointer-events: none; }
    .overlay polygon { fill: rgba(255,255,255,0.08); stroke: var(--app-primary); stroke-width: 2; }
    .handle { position: absolute; width: 20px; height: 20px; margin: -10px 0 0 -10px;
      background: var(--app-primary); border: 2px solid #fff; border-radius: 50%; touch-action: none; cursor: grab; }
    .hint { text-align: center; font-size: 12px; color: var(--app-text-muted); margin: 8px 0; }
    .crop-actions { display: flex; justify-content: flex-end; gap: 8px; }
  `]
})
export class PlateCropComponent implements AfterViewInit {
  imageUrl = input.required<string>();
  /** Corners in the photo's own NATURAL pixel space, order
   * TopLeft/TopRight/BottomRight/BottomLeft — matches VehiclePlateOcrService's
   * expected order for Cv2.GetPerspectiveTransform. */
  cropped = output<Point[]>();
  skip = output<void>();

  private readonly img = viewChild.required<ElementRef<HTMLImageElement>>('img');

  readonly ready = signal(false);
  readonly imgSize = signal({ w: 0, h: 0 });
  readonly corners = signal<Point[]>([]);

  private scaleX = 1;
  private scaleY = 1;
  private dragIndex: number | null = null;

  ngAfterViewInit(): void {
    // Deliberately not the (load) event, requestAnimationFrame polling, or
    // HTMLImageElement.decode() — all three proved unreliable: (load) can
    // fire on an already-cached image before Angular attaches the listener
    // on this freshly-created element (parent recreates this component via
    // @if every time the crop step reopens, not display:none); rAF callbacks
    // and decode()'s completion both depend on the tab/pane actually being
    // composited/visible and can stall indefinitely otherwise (observed
    // directly: a backgrounded pane never resolved decode()). Polling
    // naturalWidth via setTimeout doesn't depend on paint/compositing at
    // all — the browser still parses image headers into naturalWidth/Height
    // as soon as decoding finishes, regardless of visibility.
    this.waitForImageDecode();
  }

  private waitForImageDecode(): void {
    if (this.img().nativeElement.naturalWidth > 0) {
      this.computeDefault();
    } else {
      setTimeout(() => this.waitForImageDecode(), 16);
    }
  }

  private computeDefault(): void {
    const el = this.img().nativeElement;
    // Read natural-vs-container-capped size BEFORE img-wrap is sized (it
    // starts at 0x0), via the image's own intrinsic aspect ratio capped to
    // the frame's available box.
    const frame = el.closest('.crop-frame') as HTMLElement;
    const maxW = frame.clientWidth;
    const maxH = 320;
    const aspect = el.naturalWidth / el.naturalHeight;
    let w = maxW;
    let h = w / aspect;
    if (h > maxH) { h = maxH; w = h * aspect; }

    this.scaleX = el.naturalWidth / w;
    this.scaleY = el.naturalHeight / h;
    this.imgSize.set({ w, h });

    // Default: centered rectangle, 60% width, a plate-ish 3:1 aspect —
    // the user reshapes it into a trapezoid if the photo is angled.
    const boxW = w * 0.6;
    const boxH = Math.min(h * 0.9, boxW / 3);
    const x0 = (w - boxW) / 2;
    const y0 = (h - boxH) / 2;
    this.corners.set([
      { x: x0, y: y0 }, { x: x0 + boxW, y: y0 },
      { x: x0 + boxW, y: y0 + boxH }, { x: x0, y: y0 + boxH }
    ]);
    this.ready.set(true);
  }

  polygonPoints(): string {
    return this.corners().map((p) => `${p.x},${p.y}`).join(' ');
  }

  startDrag(event: PointerEvent, index: number): void {
    event.preventDefault();
    event.stopPropagation();
    this.dragIndex = index;
  }

  @HostListener('window:pointermove', ['$event'])
  onPointerMove(event: PointerEvent): void {
    if (this.dragIndex === null) return;
    const bounds = this.img().nativeElement.getBoundingClientRect();
    const x = clamp(event.clientX - bounds.left, 0, this.imgSize().w);
    const y = clamp(event.clientY - bounds.top, 0, this.imgSize().h);

    const next = [...this.corners()];
    next[this.dragIndex] = { x, y };
    this.corners.set(next);
  }

  @HostListener('window:pointerup')
  onPointerUp(): void {
    this.dragIndex = null;
  }

  confirmCrop(): void {
    this.cropped.emit(this.corners().map((p) => ({ x: p.x * this.scaleX, y: p.y * this.scaleY })));
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.max(min, Math.min(max, value));
}
