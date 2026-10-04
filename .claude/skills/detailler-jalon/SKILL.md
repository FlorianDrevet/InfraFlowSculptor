---
name: detailler-jalon
description: "Use at the lock that closes a milestone (R-08, R-12, R-15, R-18) or when asked to detail the next milestone. Rewrites the next milestone file of docs/plan/ from 'découpé' to execution level, grounded in the real code, so Luna can execute it without any decision."
---

# Détailler le jalon suivant au niveau d'exécution

Le niveau d'exécution est celui de `docs/plan/02-jalon-0-pilote.md` : pour chaque étape, tableau (spécifications,
maquette avec zones retenues **et exclues**, dépendances, message de commit), 🎯, 🔧 numéroté avec chemins, types, routes,
migrations et commandes **exacts**, ✅ avec les tests à écrire et leurs noms, 🧪 avec utilisateurs de démonstration,
actions et résultats attendus, 🧠, 📌.

1. Lire le code réel (graphify d'abord), la mémoire, le jalon découpé, les specs et maquettes qu'il cite.
2. Découper en segments de 3 à 15 étapes séparés par des verrous `R-nn` (un verrou de sécurité dès qu'un segment touche
   droits, secrets ou écriture chez le client), chacun avec sa branche `impl/<jalon>-<segment>`.
3. Une étape = un commit raisonnable (une demi-journée à une journée d'agent). Si Luna devrait choisir un nom, une
   bibliothèque, un format ou un comportement, ce n'est pas fini : choisis, ou ajoute une `DT-nn`.
4. Écrire la recette du jalon (`docs/plan/recettes/NN-jalon-n.md`), une section par verrou.
5. Mettre à jour le tableau des phases de `docs/plan/README.md` et la colonne jalon de `docs/design/README.md`.
6. Retirer la ligne `> **Niveau : découpé.**` de l'en-tête du fichier (sinon `gate.py check` refuse ses étapes).
7. `python tools/plan/gate.py lint` vert.
