import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/angular';
import { CodeHighlightService } from './code-highlight.service';
import { codeLanguageFromPath, CodeViewer } from './code-viewer';

describe('codeLanguageFromPath', () => {
  it.each([
    ['main.bicep', 'bicep'],
    ['main.bicepparam', 'bicep'],
    ['pipelines/release.yml', 'yaml'],
    ['settings.json', 'json'],
    ['scripts/deploy.ps1', 'powershell'],
    ['README.md', 'markdown'],
    ['manifest.unknown', 'text'],
  ] as const)('maps %s to %s', (path, language) => {
    expect(codeLanguageFromPath(path)).toBe(language);
  });
});

describe('CodeViewer', () => {
  it('uses Shiki for the file language and renders the highlighted code', async () => {
    const highlighter = {
      highlight: vi.fn().mockResolvedValue('<pre><code>highlighted</code></pre>'),
    };
    await render(CodeViewer, {
      inputs: { path: 'main.bicep', content: 'param location string' },
      providers: [{ provide: CodeHighlightService, useValue: highlighter }],
    });

    expect(highlighter.highlight).toHaveBeenCalledWith('param location string', 'bicep');
    expect(await screen.findByText('highlighted')).toBeTruthy();
  });

  it('shows plain text when syntax highlighting cannot be loaded', async () => {
    const highlighter = { highlight: vi.fn().mockRejectedValue(new Error('grammar unavailable')) };
    await render(CodeViewer, {
      inputs: { path: 'deploy.ps1', content: 'Write-Output ok' },
      providers: [{ provide: CodeHighlightService, useValue: highlighter }],
    });

    expect(await screen.findByText('Write-Output ok')).toBeTruthy();
  });
});
