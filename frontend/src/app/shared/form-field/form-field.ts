import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-form-field',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './form-field.html',
})
export class FormField {
  @Input() label = '';
  @Input() controlId = '';
  @Input() error?: string;
}
