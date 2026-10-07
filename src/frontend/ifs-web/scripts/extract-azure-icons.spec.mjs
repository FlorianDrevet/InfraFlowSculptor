import { describe, expect, it } from 'vitest';
import { buildAzureIconOutputs } from './extract-azure-icons.mjs';

function bundle({ iconName = 'azure-service', typeIcon = iconName } = {}) {
  const svg = '<svg xmlns="http://www.w3.org/2000/svg"><path d="M1 1" /></svg>';
  const dataUri = `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
  return {
    svg,
    source: `var AZURE_ICONS = { "${iconName}": ${JSON.stringify(dataUri)} };\nvar RESOURCE_TYPES = {\n  Sample: { label: 'Azure Sample', abbr: 'az', category: 'compute', icon: '${typeIcon}' },\n  FutureSample: { label: 'Future Sample', abbr: 'fs', category: 'platform', icon: 'not-yet-available', roadmap: true }\n};`,
  };
}

describe('Azure icon extraction', () => {
  it('preserves decoded SVG bytes and generates resource metadata', () => {
    const fixture = bundle();
    const output = buildAzureIconOutputs(fixture.source);

    expect(output.files.get('azure-service.svg')).toEqual(Buffer.from(fixture.svg));
    expect(output.resourceTypes).toContain(
      'Sample: { label: "Azure Sample", abbr: "az", category: "compute", file: "azure-service.svg" }',
    );
    expect(output.resourceTypes).toContain(
      'FutureSample: { label: "Future Sample", abbr: "fs", category: "platform", file: null, roadmap: true }',
    );
  });

  it('rejects invalid SVG data instead of committing a corrupt asset', () => {
    const fixture = bundle();
    const invalidSource = fixture.source.replace(
      /data:image\/svg\+xml;base64,[^"]+/u,
      'data:image/svg+xml;base64,Zm9v',
    );

    expect(() => buildAzureIconOutputs(invalidSource)).toThrow(
      'does not decode to an SVG document',
    );
  });

  it('rejects unsupported resource type syntax rather than silently dropping types', () => {
    const fixture = bundle();
    const invalidSource = fixture.source.replace("label: 'Azure Sample'", 'label: "Azure Sample"');

    expect(() => buildAzureIconOutputs(invalidSource)).toThrow('Unsupported RESOURCE_TYPES entry');
  });
});
