---
name: appliquer-corrections
description: "Use when NEXT.md status is CORRECTIONS_DEMANDEES: apply the numbered corrections R-nn-Cn listed by Claude in docs/plan/revues/R-nn-revue.md, then request the review again."
---

# Appliquer les corrections d'une revue

1. Lis `docs/plan/revues/R-nn-revue.md` en entier, en particulier le tableau « Corrections demandées ».
2. Pour chaque correction `R-nn-Cn`, dans l'ordre : test qui prouve le défaut (rouge), correction (vert), vérifications,
   commit `fix(<portée>): <résumé>` avec le pied `Étape: R-nn-Cn`. Ne corrige **que** ce qui est demandé ; un autre
   défaut constaté va dans la nouvelle demande, pas dans le code.
3. Une correction impossible ou contradictoire : ne la contourne pas ; écris-le dans la nouvelle demande.
4. Complète `R-nn-demande.md` par une section « Tour n — corrections » (ID, commit, vérification), puis
   `python tools/plan/gate.py request R-nn`, commit `docs(plan): nouvelle demande de revue R-nn`, push, mise à jour de
   la pull request. **Arrête-toi** comme dans `demander-revue`.
