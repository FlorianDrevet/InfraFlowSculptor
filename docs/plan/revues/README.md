# Revues aux verrous

Un verrou `R-nn` produit deux fichiers ici :

| Fichier | Écrit par | Quand |
|---|---|---|
| `R-nn-demande.md` | Luna | En arrivant au verrou (skill [`demander-revue`](../../../.agents/skills/demander-revue/SKILL.md)) |
| `R-nn-revue.md` | Claude | Après relecture (skill [`revue-verrou`](../../../.claude/skills/revue-verrou/SKILL.md)) |
| `captures/R-nn/` | Luna | Captures 1440 et 390 des écrans du segment |

`tools/plan/gate.py` lit la ligne `Verdict : …` de la revue : **seul** `APPROUVÉ` lève le verrou.

## Modèle de demande (`R-nn-demande.md`)

```markdown
# Demande de revue R-nn — <titre du verrou>

- **Segment** : <première étape> → <dernière étape>
- **Branche / pull request** : `impl/<segment>` — <lien de la pull request>
- **Commits** : <premier>..<dernier> (une ligne par étape : ID, message, sha court)

## Vérifications exécutées
| Commande | Résultat |
|---|---|
| `dotnet build src/backend/InfraFlowSculptor.slnx` | 0 avertissement, 0 erreur |
| `dotnet test …` | N réussis, 0 échec, 0 ignoré |
| `npm run lint / test / build / e2e` | … |
| `python tools/plan/gate.py lint` | … |

## Écarts au plan
| Étape | Plan | Réalisé | Raison |
|---|---|---|---|

## Dette de test ouverte (`.github/test-debt.md`)

## Tests manuels préparés
Liste des 🧪 du segment, données de démonstration prêtes (`seed demo`), section de recette concernée.

## Captures
`captures/R-nn/<écran>-1440.png`, `…-390.png`

## Points d'attention pour la relecture
Ce que Luna sait fragile, surprenant ou incertain.
```

## Modèle de revue (`R-nn-revue.md`)

```markdown
# Revue R-nn — <titre du verrou>

Verdict : APPROUVÉ | CORRECTIONS

- **Relu** : commits <a>..<b>, demande du <date>, recette utilisateur du <date> (résultat)
- **Vérifications relancées** : <commandes et résultats>

## Constats
### BLOQUANT-1 — <titre>
- Fichier(s) : …
- Problème / impact : …
- Correction attendue : …

### MAJEUR-1, MINEUR-1 …

## Corrections demandées (si CORRECTIONS)
| ID | Correction | Fichier(s) | Test attendu |
|---|---|---|---|
| R-nn-C1 | … | … | … |

## Décisions prises pendant la revue
Nouvelles `DT-nn`, modifications du plan (étapes ajoutées, détaillées), mises à jour des specs (statuts de preuves).

## Pour la suite
Branche du segment suivant, points à surveiller.
```

Échelle de gravité : **BLOQUANT** (refuse l'approbation), **MAJEUR** (corrigé avant approbation sauf décision
explicite consignée), **MINEUR** (peut devenir une tâche d'une étape ultérieure, citée ici).
