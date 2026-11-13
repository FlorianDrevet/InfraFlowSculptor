# J0-23 — Résolution du format des exceptions ImageScan

Date : 2026-11-13
Décision confirmée par Florian : chemin relatif vers un fichier `.trivyignore` versionné dans le dépôt.

## Décision

`ImageScan.exceptions` est une chaîne vide lorsqu’aucune exception n’est définie. Sinon, elle contient le chemin relatif au dépôt d’un fichier nommé `.trivyignore`, par exemple `security/.trivyignore`. Le template vérifie que le chemin reste dans le checkout, que le fichier existe et qu’il n’est pas un lien symbolique, puis transmet le chemin absolu à Trivy avec `--ignorefile`. Les chemins absolus, les segments `.` et `..`, les chemins avec séparateurs Windows et les fichiers absents sont refusés.

Le choix suit les fonctions stables de Trivy 0.74.0, version épinglée par le pilote : le format `.trivyignore` permet les identifiants et dates d’expiration, et l’option CLI `--ignorefile` accepte le chemin configuré. Le format `.trivyignore.yaml` reste expérimental dans cette version. Sources officielles : [filtrage Trivy 0.74](https://trivy.dev/docs/v0.74/guide/configuration/filtering/) et [options `trivy image` 0.74](https://trivy.dev/docs/v0.74/guide/references/configuration/cli/trivy_image/).

## Vérifications effectuées

- L’arbitrage manquant était uniquement le format de la valeur `exceptions`; Florian a demandé une recommandation, puis a confirmé explicitement le choix du fichier `.trivyignore` versionné dans le dépôt.
- Les documents officiels de la version Trivy épinglée confirment le format stable `.trivyignore` et l’option `--ignorefile`; le format YAML d’exclusions est indiqué comme expérimental.
- `Invoke-IfsImageScan.ps1` valide le chemin avant le scan et transmet un argument `--ignorefile` distinct; les cas de chemin vide, valide avec espace, absolu, Windows, traversal, absent et mauvais nom passent dans Pester (9/9).
- La suite backend Release séquentielle passe : 753 tests réussis, 1 test Bicep CLI ignoré par défaut; build strict `--warnaserror` : 0 avertissement/erreur. `Test-ReferencePipelines.ps1` valide les 26 YAML; déterminisme et manifeste passent sur 80 fichiers.
- Après le commit J0-23, la suite Pester complète passe sur l’arbre propre : 149/149, dont le contrôle de publication P-08 qui exige des sources de référence commitées.

Blocage résolu : CONFIRMÉ
