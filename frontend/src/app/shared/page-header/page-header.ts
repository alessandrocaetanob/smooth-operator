import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-page-header',
  standalone: true,
  templateUrl: './page-header.html',
})
export class PageHeader {
  @Input() title = '';
  @Input() subtitle?: string;
}
