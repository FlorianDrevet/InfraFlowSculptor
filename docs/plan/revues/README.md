# Auto-revues aux verrous

À chaque verrou `R-nn`, Codex relit le segment courant et consigne sa vérification dans
`R-nn-autovalidation.md`. Les fichiers `R-nn-demande.md` et `R-nn-revue.md` déjà présents restent des éléments historiques ;
aucune revue Claude ou validation d'un modèle externe n'est requise pour les prochains verrous.

Après avoir relancé les vérifications et les recettes, exécuter :

```powershell
python tools/plan/gate.py auto-review R-nn
```

La commande avance `NEXT.md` seulement si le rapport contient `Verdict : VALIDÉ` et les quatre rubriques ci-dessous.

## Modèle (`R-nn-autovalidation.md`)

```markdown
# Auto-validation R-nn — <titre du verrou>

Verdict : VALIDÉ

- **Segment** : <première étape> → <dernière étape>
- **Branche / pull request** : `impl/<segment>` — <lien ou « sans PR »>
- **Commits** : <premier>..<dernier>

## Vérifications exécutées
| Commande | Résultat observé |
|---|---|
| `python tools/plan/gate.py lint` | ... |
| <builds, tests, analyseurs> | ... |

## Recette exécutée
| Scénario | Action exécutée | Résultat observé | Preuve |
|---|---|---|---|
| ... | ... | PASS / FAIL | ... |

## Écarts
| Étape | Prévu | Réalisé | Raison |
|---|---|---|---|
| Aucun | — | — | — |

## Contrôles de coût et sécurité
- Coût réel / prévision / estimation incrémentale : ...
- Portée des changements externes : ...
- Secrets, permissions, approbations et nettoyage : ...

## Risques résiduels
- Aucun, ou liste précise des limites constatées.
```

Pour une recette locale sans coûts ni état externe, indiquer « sans objet » et expliquer pourquoi dans la rubrique coût et
sécurité. Ne jamais déclarer un scénario réussi sans l'avoir exécuté ni laisser un échec masqué derrière le verdict.
