import { Component, signal, ChangeDetectionStrategy } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionBar } from './shared/session-bar/session-bar';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, SessionBar],
  templateUrl: './app.html',
  changeDetection: ChangeDetectionStrategy.Eager,
  styleUrl: './app.css',
})
export class App {
  protected readonly title = signal('frontend');
}
