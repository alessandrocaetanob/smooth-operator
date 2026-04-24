import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { TopNavBar } from '../top-nav-bar/top-nav-bar';
import { SideNavBar } from '../side-nav-bar/side-nav-bar';

@Component({
  selector: 'app-layout',
  standalone: true,
  imports: [RouterOutlet, TopNavBar, SideNavBar],
  templateUrl: './layout.html',
  styleUrl: './layout.css',
})
export class Layout {}
