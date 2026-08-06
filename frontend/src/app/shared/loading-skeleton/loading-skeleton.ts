import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-loading-skeleton',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './loading-skeleton.html',
})
export class LoadingSkeleton {
  @Input() rows = 3;
  @Input() type: 'text' | 'card' | 'table-row' = 'text';

  get rowArray(): number[] {
    return new Array(this.rows).fill(0);
  }
}
