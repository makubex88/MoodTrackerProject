import { Component, Input } from '@angular/core';

/**
 * A small face for each mood. Colour is never the only carrier of meaning: the mouth shape and the
 * text label always accompany it, so the scale survives greyscale and colour-vision deficiency.
 */
@Component({
  selector: 'app-mood-face',
  standalone: true,
  host: { class: 'mood-face', '[attr.data-rating]': 'rating' },
  template: `
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true" focusable="false">
      <circle cx="12" cy="12" r="10"></circle>
      <circle cx="9" cy="10" r="1.1" fill="currentColor" stroke="none"></circle>
      <circle cx="15" cy="10" r="1.1" fill="currentColor" stroke="none"></circle>
      <path [attr.d]="mouth" stroke-linecap="round"></path>
    </svg>
  `,
})
export class MoodFaceComponent {
  @Input({ required: true }) rating!: number;

  get mouth(): string {
    switch (this.rating) {
      case 1:
        return 'M7.6 16.2 Q12 11.6 16.4 16.2';
      case 2:
        return 'M8 15.2 H16';
      case 3:
        return 'M8 14.4 Q12 17.2 16 14.4';
      default:
        return 'M7.2 13.4 Q12 19 16.8 13.4';
    }
  }
}
