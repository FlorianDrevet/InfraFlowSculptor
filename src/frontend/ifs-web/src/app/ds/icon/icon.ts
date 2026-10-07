import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

export type IconName =
  | 'home'
  | 'folder'
  | 'layers'
  | 'zap'
  | 'users'
  | 'sliders'
  | 'search'
  | 'plus'
  | 'minus'
  | 'check'
  | 'x'
  | 'alert'
  | 'chevron-right'
  | 'chevron-down'
  | 'lock'
  | 'globe'
  | 'key'
  | 'list'
  | 'graph'
  | 'file'
  | 'download'
  | 'upload'
  | 'clock'
  | 'branch'
  | 'refresh'
  | 'grid'
  | 'fit'
  | 'star'
  | 'copy'
  | 'trash'
  | 'external-link';

export type IconPart =
  | { kind: 'path'; d: string }
  | { kind: 'circle'; cx: number; cy: number; r: number }
  | { kind: 'rect'; x: number; y: number; width: number; height: number; rx: number };

const path = (d: string): IconPart => ({ kind: 'path', d });
const circle = (cx: number, cy: number, r: number): IconPart => ({ kind: 'circle', cx, cy, r });
const rect = (x: number, y: number, width: number, height: number, rx: number): IconPart => ({
  kind: 'rect',
  x,
  y,
  width,
  height,
  rx,
});

export const ICON_PATHS: Record<IconName, readonly IconPart[]> = {
  home: [path('M3 11l9-7 9 7'), path('M5 10v10h14V10')],
  folder: [path('M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z')],
  layers: [path('M12 3l9 5-9 5-9-5z'), path('M3 13l9 5 9-5')],
  zap: [path('M13 2L4 14h7l-1 8 9-12h-7z')],
  users: [
    path('M2.5 20c.5-4 3.3-6 6.5-6s6 2 6.5 6'),
    path('M16 4.5a3.5 3.5 0 0 1 0 7'),
    path('M18 14c2 .7 3.3 2.6 3.5 6'),
    circle(9, 8, 3.5),
  ],
  sliders: [
    path('M4 6h9M17 6h3M4 12h3M11 12h9M4 18h11M19 18h1'),
    circle(15, 6, 2),
    circle(9, 12, 2),
    circle(17, 18, 2),
  ],
  search: [path('M20 20l-3.5-3.5'), circle(11, 11, 7)],
  plus: [path('M12 5v14M5 12h14')],
  minus: [path('M5 12h14')],
  check: [path('M5 12l5 5 9-10')],
  x: [path('M6 6l12 12M18 6L6 18')],
  alert: [path('M12 3.5l9 16H3z'), path('M12 10v4M12 17h.01')],
  'chevron-right': [path('M9 6l6 6-6 6')],
  'chevron-down': [path('M6 9l6 6 6-6')],
  lock: [path('M8 11V8a4 4 0 0 1 8 0v3'), rect(5, 11, 14, 10, 2)],
  globe: [path('M3 12h18M12 3c3 3 3 15 0 18M12 3c-3 3-3 15 0 18'), circle(12, 12, 9)],
  key: [path('M11 12l9-9M17 6l3 3'), circle(8, 15, 4)],
  list: [path('M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01')],
  graph: [
    path('M7.5 6h9M6.3 8.2l4.4 7.6M17.7 8.2l-4.4 7.6'),
    circle(5, 6, 2.5),
    circle(19, 6, 2.5),
    circle(12, 18, 2.5),
  ],
  file: [path('M14 3H6a1 1 0 0 0-1 1v16a1 1 0 0 0 1 1h12a1 1 0 0 0 1-1V8z'), path('M14 3v5h5')],
  download: [path('M12 4v11M7 10l5 5 5-5M5 20h14')],
  upload: [path('M12 20V9M7 14l5-5 5 5M5 4h14')],
  clock: [path('M12 7v5l3 2'), circle(12, 12, 9)],
  branch: [path('M6 7v10M18 9c0 4-4 4-10 8'), circle(6, 5, 2), circle(6, 19, 2), circle(18, 7, 2)],
  refresh: [
    path('M20 11a8 8 0 0 0-14.6-4.5L4 8M4 4v4h4M4 13a8 8 0 0 0 14.6 4.5L20 16M20 20v-4h-4'),
  ],
  grid: [
    rect(3, 3, 7, 7, 1.5),
    rect(14, 3, 7, 7, 1.5),
    rect(3, 14, 7, 7, 1.5),
    rect(14, 14, 7, 7, 1.5),
  ],
  fit: [path('M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5')],
  star: [path('M12 3l2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9z')],
  copy: [path('M16 8V5a1 1 0 0 0-1-1H5a1 1 0 0 0-1 1v10a1 1 0 0 0 1 1h3'), rect(8, 8, 12, 12, 2)],
  trash: [path('M4 7h16M10 11v6M14 11v6M6 7l1 13h10l1-13M9 7V4h6v3')],
  'external-link': [
    path('M14 4h6v6M20 4l-9 9M18 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1h5'),
  ],
};

@Component({
  selector: 'app-ds-icon',
  standalone: true,
  templateUrl: './icon.html',
  styleUrl: './icon.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DsIcon {
  readonly name = input<IconName>('file');
  readonly size = input(16);
  readonly strokeWidth = input(1.75);
  readonly label = input<string | undefined>();
  protected readonly paths = computed(() => ICON_PATHS[this.name()] ?? ICON_PATHS.file);
}
