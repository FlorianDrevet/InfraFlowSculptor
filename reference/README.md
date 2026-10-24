# Référence et sorties générées

Ce dossier contient les émetteurs, leurs outils de validation, les épingles de dépendances et une sortie de référence produite par ces émetteurs.

## Règle de modification

La vérité du comportement se trouve dans les émetteurs et les modèles, pas dans leur copie publiée. Modifiez le code source de génération, régénérez `pilot/bicep-azdo`, puis vérifiez la sortie et ses empreintes. Une modification manuelle de `pilot/bicep-azdo` créerait un écart invisible entre la prochaine génération et ce qui a été relu ; toute exception doit donc être expliquée dans la demande de revue.

## Organisation

- `release-module/` : module PowerShell de release, schémas et tests Pester.
- `pilot/pins.json` : versions des outils et dépendances contrôlés.
- `pilot/bicep-azdo/` : snapshot généré du projet pilote, dont le manifeste de documentation `README.ifs.md`.
- `pilot/manifest.example.json` : exemple du manifeste de publication, conservé hors de la sortie générée.
- `tools/` : contrôles de déterminisme, pipelines et compilation Bicep.

Après une modification de la sortie, recalculez le manifeste d’exemple avec :

```powershell
pwsh ./reference/tools/Update-ManifestExample.ps1
```

Le manifeste d’exemple utilise une date et un commit fictifs fixes. Il montre la structure et les empreintes des fichiers ; le pipeline de publication doit produire le manifeste réel avec ses métadonnées de commit et de révision.
