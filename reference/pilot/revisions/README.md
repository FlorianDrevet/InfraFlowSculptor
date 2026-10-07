# Overlays ecrits pour la recette

`New-ProofRevision.ps1` applique les fichiers `changes.patch` de ce dossier au clone Azure Repos et recalcule son
manifeste. Ces patches sont des changements manuels conserves dans le depot ; ils ne sont pas produits par le moteur.
Leur base est le clone produit par `Publish-PilotReference.ps1`, avec le profil de coût P-08 applique ; ils ne s'appliquent
pas directement aux sources Bicep non profilees de `reference/pilot/bicep-azdo`.
Chaque recette P2/P3 utilise un clone neuf de revision 1. P4 utilise un seul clone et applique successivement rev3,
rev4 puis rev5.

Chaque manifeste publie porte une `proofLineage`. `New-ProofRevision.ps1` valide la filiation de depart declaree dans
`revision.json` avant d'appliquer un patch : les scenarios `p2` et `p3-role` restent independants, et `rev3` accepte
la fin du scenario P2 ou l'historique numerote. Les overlays suivants exigent l'historique numerote. Cela empeche
d'appliquer par erreur un patch de memes numéros sur un scenario different.

Les entrees `files[].sha256` du manifeste sont calculees sur le texte UTF-8 apres normalisation des fins de ligne en
LF. Un checkout Windows avec `core.autocrlf=true` garde donc la meme empreinte logique et les overlays restent
applicables apres checkout.

| Selection | Manifeste | Changement |
|---|---:|---|
| `2` | 2 | `core/infra/main.prd.bicepparam` : retention Log Analytics 90 -> 120 jours seulement |
| `3` | 3 | `orders` ajoute l'identite affectee `id extra` en dev et prd |
| `4` | 4 | `orders` retire `id extra` de la sortie geree |
| `5` | 5 | `orders` augmente `maxReplicas` de 1 à 2 en dev et prd après le profil de coût P-08 |
| `p2` | 2 | Scénario indépendant : `orders` retire le rôle Log Analytics Reader |
| `p3-role` | 2 | Scenario independant : `orders` remplace Log Analytics Reader par Log Analytics Data Reader |

Les revisions numerotees 2 a 5 suivent l'historique P4 ; `rev2` est aussi disponible comme fixture autonome du
critere 6, tandis que P3b provoque sa derive directement dans le portail. L'overlay `p2` sert aux preuves P2 et P7 ;
`p3-role` sert a P3a. Ces deux overlays sont des historiques independants repartant de la reference initiale. Aucun
overlay ne pousse ni ne commit le clone.
