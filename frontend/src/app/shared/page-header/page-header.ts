import { Component, Input, ChangeDetectionStrategy } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './page-header.html',
})
export class PageHeader {
  @Input() title = '';
  @Input() subtitle?: string;
}
