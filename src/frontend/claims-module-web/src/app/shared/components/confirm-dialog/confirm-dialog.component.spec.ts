import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { ConfirmDialogComponent, ConfirmDialogData, ConfirmDialogResult } from './confirm-dialog.component';

describe('ConfirmDialogComponent', () => {
  let fixture: ComponentFixture<ConfirmDialogComponent>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<ConfirmDialogComponent, ConfirmDialogResult | undefined>>;

  function create(data: ConfirmDialogData): void {
    dialogRef = jasmine.createSpyObj('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      imports: [ConfirmDialogComponent],
      providers: [
        provideNoopAnimations(),
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: data }
      ]
    });

    fixture = TestBed.createComponent(ConfirmDialogComponent);
    fixture.detectChanges();
  }

  it('renders the title and message', () => {
    create({ title: 'Transition to Open', message: 'Transition claim CLM-1 from Draft to Open?' });

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Transition to Open');
    expect(text).toContain('Transition claim CLM-1 from Draft to Open?');
  });

  it('closes with confirmed:true and no reason when no reason is required', () => {
    create({ title: 'Transition to Open', message: 'msg' });

    fixture.nativeElement.querySelector('button[mat-raised-button]').click();

    expect(dialogRef.close).toHaveBeenCalledWith({ confirmed: true, reason: null });
  });

  it('closes with undefined on cancel', () => {
    create({ title: 'Transition to Open', message: 'msg' });

    fixture.nativeElement.querySelector('button[mat-button]').click();

    expect(dialogRef.close).toHaveBeenCalledWith(undefined);
  });

  it('disables confirm until a reason is entered when requireReason is true', () => {
    create({ title: 'Reopen claim', message: 'msg', requireReason: true, reasonLabel: 'Reopen reason' });

    const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(confirmButton.disabled).toBe(true);

    fixture.componentInstance.reasonControl.setValue('New damage found.');
    fixture.detectChanges();

    expect(confirmButton.disabled).toBe(false);

    confirmButton.click();
    expect(dialogRef.close).toHaveBeenCalledWith({ confirmed: true, reason: 'New damage found.' });
  });

  it('treats a whitespace-only reason as empty', () => {
    create({ title: 'Reopen claim', message: 'msg', requireReason: true });

    fixture.componentInstance.reasonControl.setValue('   ');
    fixture.detectChanges();

    const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(confirmButton.disabled).toBe(true);
  });

  it('renders a satisfied/unmet checklist and disables confirm while any item is unmet', () => {
    create({
      title: 'Transition to Closed',
      message: 'msg',
      checklist: [
        { label: 'At least one Claimant party exists', satisfied: false },
        { label: 'No reserves pending approval', satisfied: true }
      ]
    });

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('At least one Claimant party exists');
    expect(text).toContain('No reserves pending approval');
    expect(text).toContain('All conditions above must be satisfied before continuing.');

    const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(confirmButton.disabled).toBe(true);
  });

  it('allows confirming once every checklist item is satisfied', () => {
    create({
      title: 'Transition to Closed',
      message: 'msg',
      checklist: [{ label: 'At least one Claimant party exists', satisfied: true }]
    });

    const confirmButton: HTMLButtonElement = fixture.nativeElement.querySelector('button[mat-raised-button]');
    expect(confirmButton.disabled).toBe(false);
  });
});
