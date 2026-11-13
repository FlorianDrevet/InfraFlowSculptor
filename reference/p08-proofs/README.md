# Archive des preuves P-08

Ces fichiers conservent les paramètres de reprise, le module de release et les dépendances de test utilisés pour les preuves historiques P-08. Ils restent en dehors du dossier `reference/pilot/bicep-azdo`, qui représente la sortie livrée par les émetteurs J0-23.

`tests/IfsRelease.Tests.ps1` s'exécute contre `.ifs/templates/scripts/IfsRelease.psm1` archivé, avec les schémas, pins et fixtures correspondants. `tests/IfsInstall.Tests.ps1` conserve les tests du kit d'installation historique et s'appuie sur le module générique encore livré.
