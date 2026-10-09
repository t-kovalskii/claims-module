import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import {
  AddReserveDialogComponent,
  AddReserveDialogData,
  AddReserveDialogResult
} from './add-reserve-dialog.component';

describe('AddReserveDialogComponent', () => {
  let fixture: ComponentFixture<AddReserveDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<AddReserveDialogComponent, AddReserveDialogResult | undefined>>;

  function create(data: AddReserveDialogData = { pendingComponentTypes: [] }): void {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      imports: [AddReserveDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: data }
      ]
    });

    fixture = TestBed.createComponent(AddReserveDialogComponent);
    fixture.detectChanges();
  }

  it('is disabled until a valid amount is entered', () => {
    create();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(submitButton.disabled).toBe(true);

    fixture.componentInstance.form.controls.amount.setValue(5000);
    fixture.detectChanges();

    expect(submitButton.disabled).toBe(false);
  });

  it('shows the auto-approve tier preview for an amount at or below $10,000', () => {
    create();

    fixture.componentInstance.form.controls.amount.setValue(10000);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Auto-approved (≤ $10,000)');
  });

  it('shows the Supervisor-or-Manager tier preview for an amount between $10,000 and $100,000', () => {
    create();

    fixture.componentInstance.form.controls.amount.setValue(50000);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Requires Supervisor or Manager approval');
  });

  it('shows the Manager-only tier preview for an amount above $100,000', () => {
    create();

    fixture.componentInstance.form.controls.amount.setValue(150000);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Requires Manager approval');
  });

  it('rejects a non-positive amount for a non-SubrogationRecoverable component', () => {
    create();

    fixture.componentInstance.form.controls.componentType.setValue('Indemnity');
    fixture.componentInstance.form.controls.amount.setValue(-500);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Reserve amount must be greater than zero.');
    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(submitButton.disabled).toBe(true);
  });

  it('allows a negative amount for SubrogationRecoverable', () => {
    create();

    fixture.componentInstance.form.controls.componentType.setValue('SubrogationRecoverable');
    fixture.componentInstance.form.controls.amount.setValue(-8000);
    fixture.detectChanges();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(submitButton.disabled).toBe(false);
  });

  it('rejects a zero amount for SubrogationRecoverable', () => {
    create();

    fixture.componentInstance.form.controls.componentType.setValue('SubrogationRecoverable');
    fixture.componentInstance.form.controls.amount.setValue(0);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('SubrogationRecoverable amount must not be zero.');
  });

  it('disables a component type that already has a transaction pending approval', () => {
    create({ pendingComponentTypes: ['Indemnity'] });

    expect(fixture.componentInstance.isPending('Indemnity')).toBe(true);
    expect(fixture.componentInstance.isPending('Expense')).toBe(false);
  });

  it('blocks submission while the selected component type is pending approval, even with a valid amount', () => {
    create({ pendingComponentTypes: ['Indemnity'] });

    fixture.componentInstance.form.controls.componentType.setValue('Indemnity');
    fixture.componentInstance.form.controls.amount.setValue(5000);
    fixture.detectChanges();

    const submitButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(submitButton.disabled).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('retract it first');
  });

  it('closes with the entered values, trimming an empty reason to null', () => {
    create();

    fixture.componentInstance.form.setValue({ componentType: 'Expense', amount: 1200, changeReason: '  ' });
    fixture.detectChanges();

    fixture.nativeElement.querySelector('button[mat-raised-button]').click();

    expect(dialogRef.close).toHaveBeenCalledWith({ componentType: 'Expense', amount: 1200, changeReason: null });
  });

  it('closes with undefined on cancel', () => {
    create();

    fixture.nativeElement.querySelector('button[mat-button]').click();

    expect(dialogRef.close).toHaveBeenCalledWith(undefined);
  });
});
