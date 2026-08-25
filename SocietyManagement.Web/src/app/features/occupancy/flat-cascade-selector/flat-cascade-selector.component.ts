import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, inject, signal } from '@angular/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { Building, Flat, Floor, Wing } from '../../../core/models/society.model';
import { SocietyService } from '../../society-setup/services/society.service';

/** Building→Wing→Floor→Flat cascade, extracted from
 * QuickAddFlatDialogComponent's step 1 (minus the "+Add new..." inline
 * creators, which don't belong on a filter picker) — reused by the
 * Owners/Tenants/Occupancy History tabs so the cascade is built once. */
@Component({
  selector: 'app-flat-cascade-selector',
  standalone: true,
  imports: [CommonModule, MatFormFieldModule, MatSelectModule],
  template: `
    <div class="cascade">
      <mat-form-field appearance="outline">
        <mat-label>Building</mat-label>
        <mat-select [value]="buildingId()" (selectionChange)="onBuildingChange($event.value)">
          @for (b of buildings(); track b.id) { <mat-option [value]="b.id">{{ b.name }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Wing</mat-label>
        <mat-select [value]="wingId()" (selectionChange)="onWingChange($event.value)" [disabled]="!buildingId()">
          @for (w of wings(); track w.id) { <mat-option [value]="w.id">{{ w.name }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Floor</mat-label>
        <mat-select [value]="floorId()" (selectionChange)="onFloorChange($event.value)" [disabled]="!wingId()">
          @for (f of floors(); track f.id) { <mat-option [value]="f.id">{{ f.name || ('Floor ' + f.floorNumber) }}</mat-option> }
        </mat-select>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Flat</mat-label>
        <mat-select [value]="flatId()" (selectionChange)="onFlatChange($event.value)" [disabled]="!floorId()">
          @for (f of flats(); track f.id) { <mat-option [value]="f.id">{{ f.flatNumber }}</mat-option> }
        </mat-select>
      </mat-form-field>
    </div>
  `,
  styles: [`
    .cascade { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 20px; }
    mat-form-field { width: 200px; }
  `]
})
export class FlatCascadeSelectorComponent implements OnChanges {
  @Input() societyId!: number;
  @Input() initialFlatId?: number | null;
  @Output() flatSelected = new EventEmitter<number>();

  private readonly societyService = inject(SocietyService);

  readonly buildings = signal<Building[]>([]);
  readonly wings = signal<Wing[]>([]);
  readonly floors = signal<Floor[]>([]);
  readonly flats = signal<Flat[]>([]);

  readonly buildingId = signal(0);
  readonly wingId = signal(0);
  readonly floorId = signal(0);
  readonly flatId = signal(0);

  private pendingInitialFlatId: number | null = null;

  ngOnChanges(): void {
    if (!this.societyId) return;
    this.pendingInitialFlatId = this.initialFlatId ?? null;
    this.buildingId.set(0);
    this.wingId.set(0);
    this.floorId.set(0);
    this.flatId.set(0);
    this.wings.set([]);
    this.floors.set([]);
    this.flats.set([]);
    this.societyService.getBuildings(this.societyId).subscribe((buildings) => this.buildings.set(buildings));
  }

  onBuildingChange(buildingId: number): void {
    this.buildingId.set(buildingId);
    this.wingId.set(0);
    this.floorId.set(0);
    this.flatId.set(0);
    this.wings.set([]);
    this.floors.set([]);
    this.flats.set([]);
    this.societyService.getWings(buildingId).subscribe((wings) => this.wings.set(wings));
  }

  onWingChange(wingId: number): void {
    this.wingId.set(wingId);
    this.floorId.set(0);
    this.flatId.set(0);
    this.floors.set([]);
    this.flats.set([]);
    this.societyService.getFloors(wingId).subscribe((floors) => this.floors.set(floors));
  }

  onFloorChange(floorId: number): void {
    this.floorId.set(floorId);
    this.flatId.set(0);
    this.flats.set([]);
    this.societyService.getFlats({ floorId, pageSize: 100 }).subscribe((result) => {
      this.flats.set(result.items);
      if (this.pendingInitialFlatId && result.items.some((f) => f.id === this.pendingInitialFlatId)) {
        this.onFlatChange(this.pendingInitialFlatId);
        this.pendingInitialFlatId = null;
      }
    });
  }

  onFlatChange(flatId: number): void {
    this.flatId.set(flatId);
    if (flatId) this.flatSelected.emit(flatId);
  }
}
