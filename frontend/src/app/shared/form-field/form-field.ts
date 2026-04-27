import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-form-field',
  standalone: true,
  templateUrl: './form-field.html',
})
export class FormField {
  @Input() label = '';
  @Input() controlId = '';
  @Input() error?: string;
}
