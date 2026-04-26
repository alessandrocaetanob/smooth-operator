import { Component, Input, OnChanges, SimpleChanges } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-mascot',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './mascot.html',
  styleUrl: './mascot.css',
})
export class Mascot implements OnChanges {
  @Input() isPasswordFocused = false;
  @Input() typingLength = 0;
  @Input() isSmall = false;

  eyeOffsetX = 0;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['typingLength'] && !this.isPasswordFocused) {
      // Calculate eye movement based on typing length, clamp to a max offset
      const maxOffset = 15;
      this.eyeOffsetX = Math.min(this.typingLength * 2, maxOffset) - maxOffset / 2;
    }
  }
}
