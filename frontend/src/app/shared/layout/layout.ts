import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavBar } from '../top-nav-bar/top-nav-bar';
import { SideNavBar } from '../side-nav-bar/side-nav-bar';
import { ConfirmDialog } from '../confirm-dialog/confirm-dialog';
import { ToastContainer } from '../toast/toast';
import { LayoutService } from '../../services/layout.service';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, TopNavBar, SideNavBar, ConfirmDialog, ToastContainer],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class Layout {
  readonly layout = inject(LayoutService);
  readonly collapsed = this.layout.collapsed;
}
