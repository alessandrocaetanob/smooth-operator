import {
  animate,
  animateChild,
  group,
  query,
  sequence,
  stagger,
  style,
  transition,
  trigger,
} from '@angular/animations';

/** Fade in from opacity 0 */
export const fadeIn = trigger('fadeIn', [
  transition(':enter', [
    style({ opacity: 0 }),
    animate('200ms ease', style({ opacity: 1 })),
  ]),
  transition(':leave', [
    animate('150ms ease', style({ opacity: 0 })),
  ]),
]);

/** Slide up + fade in */
export const slideUp = trigger('slideUp', [
  transition(':enter', [
    style({ opacity: 0, transform: 'translateY(12px)' }),
    animate('250ms cubic-bezier(0.4,0,0.2,1)', style({ opacity: 1, transform: 'translateY(0)' })),
  ]),
  transition(':leave', [
    animate('150ms ease', style({ opacity: 0, transform: 'translateY(8px)' })),
  ]),
]);

/** Scale + fade in — for dialogs and popovers */
export const scaleIn = trigger('scaleIn', [
  transition(':enter', [
    style({ opacity: 0, transform: 'scale(0.94)' }),
    animate('220ms cubic-bezier(0.34,1.56,0.64,1)', style({ opacity: 1, transform: 'scale(1)' })),
  ]),
  transition(':leave', [
    animate('150ms ease', style({ opacity: 0, transform: 'scale(0.96)' })),
  ]),
]);

/** Slide in from the right */
export const slideInFromRight = trigger('slideInFromRight', [
  transition(':enter', [
    style({ opacity: 0, transform: 'translateX(20px)' }),
    animate('250ms cubic-bezier(0.4,0,0.2,1)', style({ opacity: 1, transform: 'translateX(0)' })),
  ]),
  transition(':leave', [
    animate('150ms ease', style({ opacity: 0, transform: 'translateX(12px)' })),
  ]),
]);

/** Stagger children on enter — apply to a container */
export const listStagger = trigger('listStagger', [
  transition('* => *', [
    query(':enter', [
      style({ opacity: 0, transform: 'translateY(8px)' }),
      stagger('40ms', animate('200ms ease', style({ opacity: 1, transform: 'translateY(0)' }))),
    ], { optional: true }),
  ]),
]);

/** Page-level route transition */
export const routeFade = trigger('routeFade', [
  transition('* <=> *', [
    sequence([
      query(':leave', [animate('120ms ease', style({ opacity: 0 }))], { optional: true }),
      query(':enter', [
        style({ opacity: 0 }),
        animate('200ms ease', style({ opacity: 1 })),
      ], { optional: true }),
    ]),
  ]),
]);

/** Toast slide in from right */
export const toastEnter = trigger('toastEnter', [
  transition(':enter', [
    style({ opacity: 0, transform: 'translateX(100%) scale(0.9)' }),
    animate('300ms cubic-bezier(0.34,1.56,0.64,1)', style({ opacity: 1, transform: 'translateX(0) scale(1)' })),
  ]),
  transition(':leave', [
    animate('200ms ease', style({ opacity: 0, transform: 'translateX(50%) scale(0.95)' })),
  ]),
]);
