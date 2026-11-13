import { Injectable } from '@angular/core';
import type { BundledLanguage } from 'shiki';

export type CodeLanguage = BundledLanguage | 'text';
type Highlighter = Awaited<ReturnType<typeof import('shiki').createHighlighter>>;

@Injectable({ providedIn: 'root' })
export class CodeHighlightService {
  private highlighter?: Promise<Highlighter>;

  async highlight(code: string, language: CodeLanguage): Promise<string> {
    this.highlighter ??= import('shiki').then(({ createHighlighter }) =>
      createHighlighter({
        langs: ['bicep', 'yaml', 'json', 'powershell', 'markdown'],
        themes: ['github-dark'],
      }),
    );
    const highlighter = await this.highlighter;
    return highlighter.codeToHtml(code, { lang: language, theme: 'github-dark' });
  }
}
