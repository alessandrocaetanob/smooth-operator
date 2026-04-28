import {
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
  ChangeDetectionStrategy,
} from '@angular/core';
import { animate, style, transition, trigger } from '@angular/animations';

const drawerPanel = trigger('drawerPanel', [
  transition(':enter', [
    style({ transform: 'translateX(100%)' }),
    animate('300ms cubic-bezier(0.4,0,0.2,1)', style({ transform: 'translateX(0)' })),
  ]),
  transition(':leave', [
    animate('220ms cubic-bezier(0.4,0,0.2,1)', style({ transform: 'translateX(100%)' })),
  ]),
]);

const backdrop = trigger('backdrop', [
  transition(':enter', [style({ opacity: 0 }), animate('200ms ease', style({ opacity: 1 }))]),
  transition(':leave', [animate('200ms ease', style({ opacity: 0 }))]),
]);

@Component({
  selector: 'app-drawer',
  standalone: true,
  templateUrl: './drawer.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [drawerPanel, backdrop],
})
export class Drawer {
  @Input() title = '';
  @Input() subtitle = '';
  @Output() closed = new EventEmitter<void>();

  @HostListener('keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
