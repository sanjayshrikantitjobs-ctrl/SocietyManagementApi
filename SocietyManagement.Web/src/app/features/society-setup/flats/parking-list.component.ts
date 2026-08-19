import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { ParkingSlot } from '../../../core/models/society.model';
import { ToastService } from '../../../core/services/toast.service';
import { DataTableComponent } from '../../../shared/components/data-table/data-table.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../services/society.service';

const PARKING_TYPE_LABELS: Record<number, string> = { 1: 'Two Wheeler', 2: 'Four Wheeler', 3: 'Visitor' };
const PARKING_STATUS_LABELS: Record<number, string> = { 1: 'Vacant', 2: 'Allocated', 3: 'Reserved' };

@Component({
  selector: 'app-parking-list',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, MatTableModule, DataTableComponent, PageHeaderComponent],
  templateUrl: './parking-list.component.html'
})
export class ParkingListComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly societyId = signal(0);
  readonly loading = signal(true);
  readonly slots = signal<ParkingSlot[]>([]);
  readonly displayedColumns = ['slotNumber', 'type', 'status', 'allocatedFlatNumber', 'actions'];
  readonly typeLabels = PARKING_TYPE_LABELS;
  readonly statusLabels = PARKING_STATUS_LABELS;

  ngOnInit(): void {
    this.societyId.set(Number(this.route.snapshot.paramMap.get('societyId')));
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.societyService.getParkingSlots(this.societyId()).subscribe((data) => {
      this.slots.set(data);
      this.loading.set(false);
    });
  }

  add(): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: 'Add Parking Slot',
        fields: [
          { key: 'slotNumber', label: 'Slot Number', type: 'text' },
          { key: 'type', label: 'Type', type: 'select', options: [
            { value: 1, label: 'Two Wheeler' }, { value: 2, label: 'Four Wheeler' }, { value: 3, label: 'Visitor' }
          ]}
        ]
      }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createParkingSlot(this.societyId(), result.slotNumber, Number(result.type)).subscribe(() => {
        this.toast.success('Parking slot added.');
        this.load();
      });
    });
  }

  allocate(slot: ParkingSlot): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: { title: `Allocate ${slot.slotNumber}`, fields: [{ key: 'flatId', label: 'Flat Id', type: 'number' }] }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.allocateParkingSlot(slot.id, Number(result.flatId)).subscribe(() => {
        this.toast.success('Parking slot allocated.');
        this.load();
      });
    });
  }

  vacate(slot: ParkingSlot): void {
    this.societyService.allocateParkingSlot(slot.id, null).subscribe(() => {
      this.toast.success('Parking slot vacated.');
      this.load();
    });
  }

  remove(slot: ParkingSlot): void {
    this.confirmDialog.confirm({
      title: 'Delete Parking Slot', destructive: true, message: `Delete slot "${slot.slotNumber}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteParkingSlot(slot.id).subscribe(() => {
        this.toast.success('Parking slot deleted.');
        this.load();
      });
    });
  }
}
