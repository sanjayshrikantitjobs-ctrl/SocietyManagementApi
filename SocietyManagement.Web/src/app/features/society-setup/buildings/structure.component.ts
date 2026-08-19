import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { switchMap, tap } from 'rxjs';
import { Building, Floor, Wing } from '../../../core/models/society.model';
import { ToastService } from '../../../core/services/toast.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PromptDialogComponent } from '../../../shared/components/prompt-dialog/prompt-dialog.component';
import { ConfirmDialogService } from '../../../shared/services/confirm-dialog.service';
import { SocietyService } from '../services/society.service';

interface BuildingNode extends Building {
  wings?: WingNode[];
  wingsLoaded?: boolean;
}
interface WingNode extends Wing {
  floors?: Floor[];
  floorsLoaded?: boolean;
}

/** Society Setup structure editor: Building -> Wing -> Floor as a nested
 * accordion. Each level adds/deletes its own children inline via the shared
 * PromptDialogComponent, avoiding a bespoke form per level. */
@Component({
  selector: 'app-structure',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatButtonModule, MatIconModule, MatExpansionModule,
    MatProgressSpinnerModule, PageHeaderComponent, EmptyStateComponent
  ],
  templateUrl: './structure.component.html',
  styleUrl: './structure.component.scss'
})
export class StructureComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly societyService = inject(SocietyService);
  private readonly dialog = inject(MatDialog);
  private readonly confirmDialog = inject(ConfirmDialogService);
  private readonly toast = inject(ToastService);

  readonly societyId = signal(0);
  readonly loading = signal(true);
  readonly buildings = signal<BuildingNode[]>([]);

  ngOnInit(): void {
    this.route.paramMap.pipe(
      tap((params) => this.societyId.set(Number(params.get('societyId')))),
      switchMap(() => this.societyService.getBuildings(this.societyId()))
    ).subscribe((data) => {
      this.buildings.set(data);
      this.loading.set(false);
    });
  }

  addBuilding(): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: { title: 'Add Building', fields: [{ key: 'name', label: 'Building Name', type: 'text' }] }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createBuilding(this.societyId(), result.name).subscribe(() => {
        this.toast.success('Building added.');
        this.reloadBuildings();
      });
    });
  }

  deleteBuilding(building: Building): void {
    this.confirmDialog.confirm({
      title: 'Delete Building', destructive: true, message: `Delete "${building.name}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteBuilding(building.id).subscribe(() => {
        this.toast.success('Building deleted.');
        this.reloadBuildings();
      });
    });
  }

  loadWings(building: BuildingNode): void {
    if (building.wingsLoaded) return;
    this.societyService.getWings(building.id).subscribe((wings) => {
      building.wings = wings;
      building.wingsLoaded = true;
    });
  }

  addWing(building: BuildingNode): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: { title: 'Add Wing', fields: [{ key: 'name', label: 'Wing Name', type: 'text' }] }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createWing(building.id, result.name).subscribe(() => {
        this.toast.success('Wing added.');
        building.wingsLoaded = false;
        this.loadWings(building);
      });
    });
  }

  deleteWing(building: BuildingNode, wing: Wing): void {
    this.confirmDialog.confirm({
      title: 'Delete Wing', destructive: true, message: `Delete "${wing.name}"?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteWing(wing.id).subscribe(() => {
        this.toast.success('Wing deleted.');
        building.wingsLoaded = false;
        this.loadWings(building);
      });
    });
  }

  loadFloors(wing: WingNode): void {
    if (wing.floorsLoaded) return;
    this.societyService.getFloors(wing.id).subscribe((floors) => {
      wing.floors = floors;
      wing.floorsLoaded = true;
    });
  }

  addFloor(wing: WingNode): void {
    this.dialog.open(PromptDialogComponent, {
      width: '420px',
      data: {
        title: 'Add Floor',
        fields: [
          { key: 'floorNumber', label: 'Floor Number', type: 'number' },
          { key: 'name', label: 'Floor Name (optional)', type: 'text', required: false }
        ]
      }
    }).afterClosed().subscribe((result) => {
      if (!result) return;
      this.societyService.createFloor(wing.id, Number(result.floorNumber), result.name).subscribe(() => {
        this.toast.success('Floor added.');
        wing.floorsLoaded = false;
        this.loadFloors(wing);
      });
    });
  }

  deleteFloor(wing: WingNode, floor: Floor): void {
    this.confirmDialog.confirm({
      title: 'Delete Floor', destructive: true, message: `Delete floor ${floor.floorNumber}?`
    }).subscribe((confirmed) => {
      if (!confirmed) return;
      this.societyService.deleteFloor(floor.id).subscribe(() => {
        this.toast.success('Floor deleted.');
        wing.floorsLoaded = false;
        this.loadFloors(wing);
      });
    });
  }

  private reloadBuildings(): void {
    this.societyService.getBuildings(this.societyId()).subscribe((data) => this.buildings.set(data));
  }
}
