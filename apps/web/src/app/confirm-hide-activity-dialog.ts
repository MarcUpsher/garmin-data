import { Component, inject } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';

interface ConfirmHideActivityData {
  title: string;
}

@Component({
  selector: 'app-confirm-hide-activity-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Hide this activity?</h2>
    <mat-dialog-content>
      <p><strong>{{ data.title }}</strong> will be moved out of the visible activity list. You can restore it later from Hidden activities.</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button type="button" mat-dialog-close>Cancel</button>
      <button mat-flat-button type="button" [mat-dialog-close]="true">Hide activity</button>
    </mat-dialog-actions>
  `,
})
export class ConfirmHideActivityDialog {
  protected readonly data = inject<ConfirmHideActivityData>(MAT_DIALOG_DATA);
}
