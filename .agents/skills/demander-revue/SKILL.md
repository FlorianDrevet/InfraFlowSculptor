---
name: demander-revue
description: "Demander une revue de code en lecture seule à Claude CLI à chaque verrou du plan, vérifier les constats et les consigner pour l'auto-revue Codex."
---

# Demander une revue de code à Claude CLI

À chaque verrou `R-nn`, demande à Claude CLI une revue en lecture seule du code changé depuis la base du segment (ou depuis
la base de la pull request du segment). Utilise la branche distante à jour comme base et limite la demande aux changements
de code du segment. Exemple PowerShell :

```powershell
$base = 'origin/main' # remplacer par la branche de base réelle du segment
git diff --no-ext-diff --unified=30 "$base...HEAD" |
  claude -p --restricted 'Relis le diff de code fourni en lecture seule. Signale seulement les défauts actionnables de correction, sécurité ou fiabilité, avec gravité et chemin:ligne. Ne modifie aucun fichier. Si tu ne trouves rien, indique-le et précise les lacunes de test pertinentes.'
```

Si le segment n'a changé aucun code, note « aucun changement de code à relire ». Si Claude CLI échoue, consigne l'erreur et
résous l'accès avant de déclarer le verrou terminé. Ne publie pas automatiquement le résultat sur GitHub.

## Traiter les constats

1. Vérifie chaque constat dans le code, les specs, le plan et les tests ; Claude ne tranche pas la conception.
2. Corrige les défauts confirmés dans l'étape concernée et relance les vérifications prescrites. Documente les faux positifs
   ou les constats non retenus avec leur raison.
3. Inscris dans `docs/plan/revues/R-nn-autovalidation.md` la commande et sa base, le périmètre, les constats de Claude,
   leur vérification et leur traitement.
4. Continue ensuite avec l'auto-revue Codex et `python tools/plan/gate.py auto-review R-nn`. L'avis Claude ne remplace ni le
   verdict Codex ni le contrôle du gate.
