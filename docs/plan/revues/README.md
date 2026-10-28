# Auto-revues aux verrous

À chaque verrou `R-nn`, Claude CLI relit en lecture seule le code du segment depuis sa branche de base. Codex vérifie les
constats, corrige ceux qui sont confirmés, relance les vérifications et consigne la revue et son traitement dans
`R-nn-autovalidation.md`. Les fichiers `R-nn-demande.md` et `R-nn-revue.md` déjà présents restent des éléments historiques.

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

## Revue Claude CLI
- **Commande et base** : ...
- **Portée** : ...
- **Constats** : aucun / liste des constats avec gravité et chemin:ligne.
- **Vérification et traitement par Codex** : ...

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
