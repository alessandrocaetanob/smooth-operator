import {
  AfterViewInit,
  Component,
  Input,
  Output,
  EventEmitter,
  HostListener,
  ChangeDetectionStrategy,
  ElementRef,
  ViewChild,
} from '@angular/core';
import { animate, style, transition, trigger } from '@angular/animations';
import { TranslatePipe } from '@ngx-translate/core';

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
  imports: [TranslatePipe],
  templateUrl: './drawer.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  animations: [drawerPanel, backdrop],
})
export class Drawer implements AfterViewInit {
  @Input() title = '';
  @Input() subtitle = '';
  @Output() closed = new EventEmitter<void>();
  @ViewChild('closeButton') closeButton?: ElementRef<HTMLButtonElement>;

  ngAfterViewInit(): void {
    queueMicrotask(() => this.closeButton?.nativeElement?.focus());
  }

  @HostListener('keydown.escape')
  onEscape(): void {
    this.closed.emit();
  }
}
