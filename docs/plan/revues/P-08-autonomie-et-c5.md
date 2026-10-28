# P-08 — Résolution R0, contrôle C5 et résultat D3

Date de contrôle : 2026-10-23, UTC.

Blocage résolu : CONFIRMÉ

## Vérifications effectuées

- **R0 #760 — fin sûre.** Azure DevOps a refusé le rejet de la demande d'approbation : l'unique membre du groupe était bloqué car
  auteur de N. Le run est `canceled`, et non `rejected`. Sa timeline montre `Preview_dev` réussi, `Deploy_dev` annulé avant toute
  tâche de déploiement, `Preview_prd` et `Deploy_prd` ignorés. Aucun changement de ressource Azure n'a été lancé par R0. Cette
  résolution est limitée à ce cas et conserve le statut réel du run.
- **C1/C2 — D2 livrée.** PR #75 a été fusionnée dans `main` au SHA
  `492dfa0df7f9c22f182f1fec443f90c041ed823d`. Le CI N #759 a réussi manuellement sur `refs/heads/main` et ce SHA. Avant D3,
  les huit définitions collatérales étaient suspendues ; après l'état terminal de #761, elles ont toutes été réactivées à la
  révision 11. CI 80 et release 82 restent activées. R0 #760 et l'essai D3 #761 sont terminés sans tâche d'écriture.
- **C3 — files.** Aucune exécution en cours ou en file n'est apparue parmi les dix définitions avant ou après l'essai. Aucun run
  inattendu n'est apparu.
- **C4 — préflight D3 en lecture seule.** L'artefact `infra` téléchargé depuis N #759 a été utilisé par une seule passe du script
  D1b, boucle à 1 et `AZURE_CORE_ONLY_SHOW_ERRORS=true` limité au processus, sur l'identité Azure CLI personnelle. Le préflight,
  généré le `2026-10-23T21:38:24.3519113Z` et validé le `2026-10-23T21:39:37.9562554Z`, donne l'empreinte attendue
  `77dc939ffd65b288a6534f5172191753ea05a06bb3d14bdad7d9626e112a87b1`. Le SHA-256 de `ifs-preview.json` est
  `98c9dc42f18c650c79e836706cb121aaa2f7169be1803817791eaeb6e6d770ba`. Son schéma est valide. Dossier local :
  `%LOCALAPPDATA%\InfraFlowSculptor\P08\D3-pre\N759-20261023T213712Z\D3-pre`.
- **Prévision C8 par le préflight.** La passe personnelle indique 0 création, 5 modifications, 0 recréation, 0 suppression,
  0 révocation, 1 opération en attente `Started` et 1 écriture de données prévue. L'aperçu `Preview_dev` publié par l'unique run
  D3 #761 a confirmé une opération `data-access` toujours `Started`. C8 est KO ; `Deploy_dev` a été annulé avant toute tâche de
  déploiement. Voir [la preuve détaillée de #761](P-08-C8-D3-run-761.md).
- **C5 — inventaire.** Le relevé complet en lecture seule et la reconstitution du snapshot de référence sont dans
  `%LOCALAPPDATA%\InfraFlowSculptor\P08\p08-c5-azure-inventory-20261023.json`. Le relevé actuel contient 163 ressources,
  26 groupes, 7 piles, 59 attributions de rôles et 7 budgets. Les deux attributions supplémentaires sont les rôles de lecture D1b
  consignés dans `NEXT.md`. Les deux seules différences d'ID hors Shop42 sont un certificat ajouté dans `rg-vpd-dev` et le site
  statique retiré dans `rg-sainte-fly`. Les IDs Shop42 correspondent exactement au snapshot de référence reconstitué ; aucune
  création ni suppression Shop42 n'est observée depuis #748. Resource Graph ne montre qu'une mise à jour de la base SQL orders
  Shop42, sans création ou suppression.
- **Coût observé.** Cost Management ActualCost a répondu le `2026-10-23T21:42:22Z` : 120,792393 EUR HT MTD, soit 120,79 EUR
  arrondi. La dernière prévision réussie est celle du `2026-10-23T20:50Z` : 40,89 EUR du 24 au 31 octobre et 161,68 EUR estimés
  fin octobre. La demande de prévision à 21:42Z a reçu HTTP 429 ; la valeur précédente est conservée. La prévision mensuelle de
  200 EUR est une alerte, pas une limite de dépenses.
- **Estimation de l'essai D3.** Les ressources existantes et le digest applicatif D2 sont réutilisés ; aucun nouveau service Azure
  n'est prévu. L'estimation pour un passage d'une heure est 6,4952 EUR HT (6,50 EUR arrondi), soit 7,79424 EUR dans un scénario
  illustratif avec 20 % de taxe. Elle comprend :

  | Poste | Hypothèse | EUR HT |
  |---|---|---:|
  | SQL serverless | 2 bases existantes, 1 h chacune à 1 vCore | 1,1478 |
  | Container Apps | 2 apps existantes, 1 h au taux observé | 0,14 |
  | Log Analytics | jusqu'à 1 Go dans chacun des 2 workspaces | 4,8574 |
  | Key Vault | jusqu'à 10 000 opérations | 0,03 |
  | Réserve stockage, sauvegarde et transfert | jusqu'à 1 Go par poste | 0,32 |
  | **Total estimé** | | **6,4952** |

  La projection d'octobre avec cet essai est 168,1752 EUR HT, soit environ 168,18 EUR, sous l'alerte de 200 EUR. Ce calcul n'est
  pas un plafond : le trafic utilisateur continu est exclu et l'ingestion Log Analytics peut dépasser l'hypothèse. En cas de C8
  négatif, la release est annulée avant l'écriture des ressources ; l'estimation complète sert de borne prudente au lancement,
  pas de permission à dépasser le contrôle C8.
- **C6 — digest applicatif.** Les apps DEV et PRD utilisent toutes deux le digest D2
  `sha256:4af4e78a6a7d90facd49f513593b9f5018513c7dfb1bd724984ca36d064fee7f`, relu dans Azure le 2026-10-23.
- **C7/C9 — autonomie et garde.** Florian autorise Codex à approuver exclusivement D3 après C8. Le contrôle `Pre-check approvals`
  est resté en ordre 1 avant `Exclusive Lock` en ordre 2 ; le groupe d'approbateurs est inchangé et comporte un membre.
  À `2026-10-23T21:55Z`, l'auto-approbation a été activée temporairement et les instructions ont précisé que R0 ne devait jamais
  être approuvé et que D3 serait annulé avant tout déploiement si C8 était faux ou illisible. Après l'état terminal de #761, le
  réglage initial a été restauré et vérifié : `requesterCannotBeApprover=true`. Le contrôle reste en place.
- **Sélection du run N.** La sélection de ressource manuelle Azure DevOps a choisi le run précis #759. La lecture REST de #761
  confirme `resources.pipelines.ci.pipeline.id = 759` et le SHA source exact ci-dessus.
- **Résultat C8.** L'aperçu publié de #761 a l'empreinte
  `ee62376499db448e67a4f5ead36900a08bb93e78dea469c53c1966bb0a7d35d2`, mais contient une opération `data-access` à l'état
  `Started` et une écriture de données prévue. C8 est KO. `Deploy_dev` a été annulé avant son démarrage ; PRD a été ignoré. Les
  détails et empreintes sont consignés dans [la preuve D3 #761](P-08-C8-D3-run-761.md). L'essai étant unique, aucun deuxième run
  n'est lancé et P-08 reste bloqué.
- **Vérifications locales après les mises à jour documentaires.** `python -m unittest discover tools/plan/tests` : 11/11 réussis.
  `python tools/plan/gate.py lint` : 105 étapes et 18 verrous cohérents. `git diff --check` a retourné le code 0 sans erreur
  de diff ; seuls des avertissements Git signalent la normalisation CRLF/LF de certains fichiers modifiés.

Cette preuve a levé le blocage lié à la fin sûre de R0 et documenté les données C5 avant D3. L'essai unique #761 a ensuite échoué
à C8 ; aucun déploiement n'a démarré. P-08 reste bloqué conformément à la règle d'arrêt après un C8 négatif.
