import { ChangeDetectionStrategy, Component, effect, inject, input, signal } from '@angular/core';
import { CodeHighlightService, CodeLanguage } from './code-highlight.service';

export function codeLanguageFromPath(path: string): CodeLanguage {
  const extension = path.toLowerCase().split('.').pop();
  const languages: Record<string, CodeLanguage> = {
    bicep: 'bicep',
    bicepparam: 'bicep',
    yaml: 'yaml',
    yml: 'yaml',
    json: 'json',
    ps1: 'powershell',
    psm1: 'powershell',
    md: 'markdown',
  };
  return (extension && languages[extension]) || 'text';
}

@Component({
  selector: 'app-code-viewer',
  standalone: true,
  template: `
    @if (html(); as highlightedCode) {
      <div class="code-viewer__highlighted" [innerHTML]="highlightedCode"></div>
    } @else {
      <pre class="code-viewer__plain"><code>{{ content() }}</code></pre>
    }
  `,
  styleUrl: './code-viewer.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CodeViewer {
  readonly path = input.required<string>();
  readonly content = input.required<string>();
  private readonly highlighter = inject(CodeHighlightService);
  protected readonly html = signal<string | null>(null);
  private renderId = 0;

  private readonly renderCode = effect(() => {
    const code = this.content();
    const language = codeLanguageFromPath(this.path());
    const renderId = ++this.renderId;
    void this.highlighter.highlight(code, language).then(
      (html) => {
        if (renderId === this.renderId) this.html.set(html);
      },
      () => {
        if (renderId === this.renderId) this.html.set(null);
      },
    );
  });
}
