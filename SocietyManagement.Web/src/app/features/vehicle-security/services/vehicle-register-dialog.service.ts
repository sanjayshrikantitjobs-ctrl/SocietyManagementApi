import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Observable, map, switchMap } from 'rxjs';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ToastService } from '../../../core/services/toast.service';
import { ResidentService } from '../../residents/services/resident.service';
import { SocietyService } from '../../society-setup/services/society.service';

/** Opens the *existing* add-vehicle prompt (same fields/validation
 * vehicles-list.component.ts uses), pre-filled with a scanned/not-registered
 * plate number — deliberately reuses VehicleFeature.cs's existing
 * POST /api/vehicles rather than adding a second create path. Only ever
 * invoked from a context already gated on Vehicles.Register (Admin/Super
 * Admin) — see vehicle-scan.component.ts / vehicle-search.component.ts. */
@Injectable({ providedIn: 'root' })
export class VehicleRegisterDialogService {
  private readonly dialog = inject(MatDialog);
  private readonly residentService = inject(ResidentService);
  private readonly societyService = inject(SocietyService);
  private readonly toast = inject(ToastService);

  /** Emits true if a vehicle was actually created, false if the dialog was
   * cancelled — lets the caller decide whether to refresh/re-run its scan. */
  open(societyId: number, registrationNumber: string): Observable<boolean> {
    return this.loadOptions(societyId).pipe(
      switchMap(({ memberOptions, flatOptions, parkingSlotOptions }) => {
        const ref = this.dialog.open(PromptDialogComponent, {
          width: '460px',
          data: {
            title: 'Register Vehicle',
            submitLabel: 'Register',
            fields: [
              { key: 'memberId', label: 'Owner (Member)', type: 'select' as const, required: false, options: [{ value: '', label: 'None' }, ...memberOptions], defaultValue: '' },
              { key: 'flatId', label: 'Flat', type: 'select' as const, required: false, options: [{ value: '', label: 'None' }, ...flatOptions], defaultValue: '' },
              { key: 'vehicleType', label: 'Vehicle Type', type: 'select' as const, options: [{ value: 1, label: 'Two Wheeler' }, { value: 2, label: 'Four Wheeler' }], defaultValue: 2 },
              { key: 'registrationNumber', label: 'Registration Number', type: 'text' as const, defaultValue: registrationNumber },
              { key: 'make', label: 'Make', type: 'text' as const, required: false, defaultValue: '' },
              { key: 'model', label: 'Model', type: 'text' as const, required: false, defaultValue: '' },
              { key: 'color', label: 'Color', type: 'text' as const, required: false, defaultValue: '' },
              { key: 'parkingSlotId', label: 'Parking Slot (optional)', type: 'select' as const, required: false, options: [{ value: '', label: 'None' }, ...parkingSlotOptions], defaultValue: '' }
            ]
          }
        });

        return ref.afterClosed().pipe(
          switchMap((result) => {
            if (!result) return [false];
            if (!result.memberId && !result.flatId) {
              this.toast.error('Assign the vehicle to an Owner (Member) or a Flat.');
              return [false];
            }
            return this.residentService.createVehicle({
              ...result, memberId: result.memberId ? Number(result.memberId) : null,
              flatId: result.flatId ? Number(result.flatId) : null, vehicleType: Number(result.vehicleType),
              parkingSlotId: result.parkingSlotId ? Number(result.parkingSlotId) : null,
              make: result.make || null, model: result.model || null, color: result.color || null
            }).pipe(map(() => true));
          })
        );
      })
    );
  }

  private loadOptions(societyId: number) {
    return this.residentService.getMembers({ societyId, pageSize: 500 }).pipe(
      switchMap((members) => this.societyService.getFlats({ societyId, pageSize: 500 }).pipe(
        switchMap((flats) => this.societyService.getParkingSlots(societyId).pipe(
          map((slots) => ({
            memberOptions: members.items.map((m) => ({ value: m.id, label: `${m.firstName} ${m.lastName}` })),
            flatOptions: flats.items.map((f) => ({ value: f.id, label: f.flatNumber })),
            parkingSlotOptions: slots.map((s) => ({ value: s.id, label: s.slotNumber }))
          }))
        ))
      ))
    );
  }
}
