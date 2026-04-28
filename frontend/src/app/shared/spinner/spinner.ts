import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';

export type SpinnerSize = 'sm' | 'md' | 'lg';
export type SpinnerColor = 'primary' | 'white' | 'error';

/** Orbital ring spinner — two counter-rotating arcs in brand colors. */
@Component({
  selector: 'app-spinner',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './spinner.html',
  styleUrl: './spinner.css',
})
export class Spinner {
  @Input() size: SpinnerSize = 'md';
  @Input() color: SpinnerColor = 'primary';

  get px(): number {
    return { sm: 16, md: 24, lg: 40 }[this.size];
  }

  get strokeWidth(): number {
    return { sm: 2, md: 2.5, lg: 3 }[this.size];
  }

  get r(): number {
    return this.px / 2 - this.strokeWidth;
  }

  get cx(): number {
    return this.px / 2;
  }
}
