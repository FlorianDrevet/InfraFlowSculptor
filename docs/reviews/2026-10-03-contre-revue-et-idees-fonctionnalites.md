# Contre-revue des corrections et idées de fonctionnalités

**Date :** 3 octobre 2026. **Version examinée :** commit `0a25751`, comparé à `a077020`.

Documents : corpus `docs/specs`, [revue initiale](2026-10-03-revue-fonctionnelle-v1.md), [réponse à la revue](2026-10-03-traitement-revue-v1.md). Les décisions nouvelles vont de DEC-85 à DEC-98.

**Nature du travail :** revue documentaire et vérification ciblée de documentation officielle. Aucun déploiement, prototype ni test d'implémentation n'a été exécuté. « Corrigé dans la spécification » et « fonctionnement démontré » sont deux statuts distincts.

## Synthèse

Les corrections améliorent sensiblement le produit : pilote réduit, séparation infrastructure/application, contrôle des effets indirects, révocation explicite, catalogue figé, prise en compte des échecs partiels et registre des preuves. Je ne maintiens pas les quarante observations précédentes comme si rien n'avait changé.

En revanche, **je ne considère pas que tous les problèmes sont clos**. Plusieurs corrections se contredisent lorsqu'on les combine. Le cas le plus net est la révocation obligatoire des rôles Azure dans des groupes protégés par un verrou qui interdit justement cette suppression. Le second est la reprise après un détachement réussi suivi d'une interruption avant la révocation.

**Verdict : non prêt pour le développement du mécanisme de livraison en l'état. Prêt pour le cadrage détaillé et les prototypes ciblés du pilote.** Les écrans et le modèle peuvent avancer sur leurs parties stables. La v1 commerciale ne doit pas être figée avant résolution des contrats de livraison, de droits et de reprise. Les constats concernant les lots ultérieurs ne bloquent pas le pilote si leurs cas sont explicitement exclus.

---

## 1. Compréhension, faits et hypothèses

### Produit compris

IFS est un service de modélisation d'infrastructures Azure qui génère du code, des pipelines et un kit d'installation dans les dépôts du client. Il vise notamment les équipes plateforme, les développeurs et les intégrateurs. Sa valeur est de maintenir les conventions et le câblage d'un service pendant ses évolutions, avec une livraison autonome chez le client.

Le pilote décrit désormais deux composants, deux environnements, Bicep, Azure DevOps, Azure Repos et un conteneur témoin. La création seule ne suffit plus : une modification, une révocation et une reprise font partie de la démonstration.

### Faits présents dans les documents

- Le pilote vise 3 à 5 équipes ; SQL est facultatif selon leurs besoins : [04 § 2.0](../specs/04-perimetre-et-lots.md).
- Les preuves P1 à P6 sont au statut **Spécifiée**, pas « Vérifiée » : [04 § 7.2](../specs/04-perimetre-et-lots.md).
- Les pipelines s'exécutent chez le client, sans dépendance d'exécution à IFS : [01](../specs/01-principes.md), [22](../specs/22-pipelines.md).
- Les rôles par composant et les mots de passe générés sont au jalon 2 ; GitHub Actions au lot 2.
- Le catalogue est figé par projet, mais des exceptions de sécurité modifient les versions supportées : DEC-92.

### Hypothèses utilisées pour challenger

- **H1.** Une interruption brutale d'agent est un incident à couvrir, même si elle ne permet pas de publier un artefact final.
- **H2.** Certains clients laisseront leurs développeurs modifier du YAML ou des points d'extension. La frontière de confiance doit donc être explicite ; je ne suppose pas que tous les auteurs de pipelines sont administrateurs Azure.
- **H3.** « Réexécutable » signifie qu'une relance converge sans travail de reconstruction manuel, sauf exception annoncée.
- **H4.** Deux équipes différentes peuvent demander et approuver un accès sans qu'une personne possède les droits de modification sur leurs deux composants.

H2 et H4 doivent être confirmées. Si elles sont écartées, il faut réduire la promesse d'autonomie et définir le rôle du coordinateur ou de l'administrateur commun.

### Informations encore nécessaires

Frontière de confiance des pipelines ; stockage durable des opérations inachevées ; traitement des verrous pendant les révocations ; acteur chargé des accès SQL entre composants ; sémantique d'une approbation d'accès ; seuils de sortie du pilote ; coût humain de l'accompagnement. Ce sont des décisions fonctionnelles, pas seulement des détails d'implémentation.

## 2. Ce qui est réellement mieux traité

| Sujet de la première revue | Évolution constatée | Appréciation |
|---|---|---|
| Périmètre et valeur — A01, A02, A19, A36 | Pilote restreint ; temps actif distingué de l'attente ; activation exigeant une seconde modification | Bonne correction de cadrage. Il manque une règle de décision commerciale pour le pilote. |
| Droits — A03 à A07 | Effets indirects vérifiés, plafond d'attribution, génération autorisée au projet, dépôts contrôlés | Contrats plus précis. L'approbation entre équipes et la frontière CI restent à terminer. |
| Premier déploiement — A08 | Coffres avant consommateurs, droits attribués pendant le déploiement, étapes ordonnées | Avancée réelle. Deux contre-exemples subsistent : coffre existant sans mot de passe et identité SQL dans un autre composant. |
| Retraits — A11, A16, A31 | Autorisations supprimées, retraits calculés contre l'unité réelle, rapport et état partiel | Bonne intention métier. Verrous, interruption brutale et suppression du composant ne sont pas résolus. |
| Aperçu — A21, A22 | What-if ARM distinct de la pile, aperçu avant approbation, contrôle d'empreinte | Architecture plus crédible. Le verrou ne couvre pas le premier aperçu et l'empreinte reste à définir. |
| Azure Repos et SQL — A23 | Politique de validation de branche ; création SQL par SID plutôt que recherche d'annuaire | Corrections utiles. Je ne réitère pas l'obligation Graph comme si la solution choisie était toujours `FROM EXTERNAL PROVIDER`. |
| Catalogue et fichiers — A18, A30 | Montée explicite, fin du numéro de révision dans les en-têtes | Bonne direction ; corrections de sécurité et identifiant de génération doivent rester reproductibles. |
| Secrets — A26 | Détection avant enregistrement, expurgation, rappel de renouvellement | Meilleur traitement de l'incident ; périmètre incomplet pour les révisions déjà publiées. |
| MCP — A27 | Administration exclue, publication avec confirmation humaine serveur, propositions par défaut | Risque mieux borné dans la spec. À préserver dans les critères d'acceptation. |
| Exploitation et interface — A29, A33 à A35 | Budgets de files, récupération sans administrateur, diagnostic partageable, saisie préservée, focus/toucher | Réponses documentaires concrètes. Pas de nouveau défaut majeur établi sur ces corrections. |

### 2.1 Registre des problèmes résiduels

**Probabilité** : appréciation qualitative, sans données d'exploitation. « Certaine dans le cas décrit » signifie que le contre-exemple suit directement les règles écrites ; cela ne prédit pas sa fréquence chez les clients. **Bloquant** porte sur le parcours concerné, pas sur tout travail possible dans le projet.

| ID | Thème | Observation / problème | Pourquoi c'est important | Impact potentiel | Probabilité | Priorité | Recommandation concrète | Question à trancher |
|---|---|---|---|---|---|---|---|---|
| B01 | Sécurité ; cycle de vie | DEC-85 impose de supprimer les rôles ; [12 § 2](../specs/12-composants-et-groupes-de-ressources.md) active `CanNotDelete` sur les RG protégés. | Le verrou bloque aussi les suppressions RBAC et de certains objets de configuration. | Révocation impossible en production ; droits conservés. | Certaine sous ce verrou | **Bloquant — pilote** | Choisir une stratégie de protection compatible et son acteur privilégié ; ajouter le verrou réel à P2. | Comment révoquer sans ouvrir une fenêtre incontrôlée de suppression des données ? |
| B02 | Reprise ; données ; support | DEC-91 retrouve les retraits depuis la pile courante et produit le rapport en fin de release ; DEC-85 détache avant de révoquer. | Après interruption entre les deux, la pile ne contient plus l'accès restant à supprimer. | Droit orphelin ; inventaire perdu ; relance annoncée idempotente mais incomplète. | Plausible ; déterministe si interruption au mauvais endroit | **Bloquant — pilote** | Journal durable des opérations à effectuer, enregistré avant mutation dans l'environnement client ; reprise des opérations non confirmées. | Quelle preuve survit à la disparition de l'agent et à l'indisponibilité d'IFS ? |
| B03 | Sécurité ; permissions | [22 § 3.1](../specs/22-pipelines.md) utilise la connexion d'infrastructure « en lecture » avant approbation ; [23 § 2](../specs/23-kit-installation.md) lui donne Contributor et des droits RBAC. La CI applicative utilise une identité également capable de déployer. | Le comportement attendu du script ne réduit pas les permissions de son identité. | Contournement possible de l'approbation ou de l'isolation entre composants si H2 est vraie. | Conditionnelle à l'accès aux pipelines et extensions | **Élevé — avant pilote avec utilisateurs non administrateurs** | Matrice précise aperçu/build/déploiement ; autorisations et contrôles sur les connexions ; définir le YAML de confiance. Séparer les capacités lorsque nécessaire. | Qui peut modifier un pipeline et obtenir chacune des identités ? |
| B04 | Parcours ; concurrence | RG-PIP-05 et RG-APP-02 disent que le verrou empêche une livraison entre aperçu et déploiement, mais l'aperçu n'utilise pas l'environnement verrouillé. | La garantie écrite est impossible sur cet intervalle. Le contrôle d'empreinte peut compenser si son contrat est complet. | Réapplication d'une ancienne image ou approbations à répéter inutilement. | Élevée sur cible active | **Élevé — pilote** | Relecture sous verrou des images, du trafic et des objets gérés ; empreinte canonique de toutes les conséquences à approuver ; nouvelle approbation si changement pertinent. | Quels changements invalident l'approbation et lesquels sont simplement reconduits ? |
| B05 | Métier ; dépendances | DEC-98 affecte l'utilisateur SQL au composant de la base ; la liaison d'accès ordonne le consommateur après la base. | L'identité système de l'application n'existe pas encore lorsque la base doit créer son utilisateur. | Premier déploiement impossible dans l'ordre annoncé ; besoin d'une seconde passe cachée. | Certaine pour ce montage | **Élevé — bloquant si inclus dans P1** | Phase explicite d'accès après création des deux extrémités, ou propriété confiée au consommateur ; borner temporairement le pilote si besoin. | Qui possède, exécute et révoque l'accès SQL entre composants ? |
| B06 | Utilisateurs ; permissions ; UX | DEC-89 transforme l'accès en proposition, mais son application exige tous les droits de modification directe. | Un approbateur du composant cible seul ne peut pas appliquer une commande qui modifie aussi le composant source. | Parcours entre équipes dépendant d'un administrateur commun. | Certaine si H4 | **Élevé — jalon 2** | Consentement source + approbation cible, bornés à un effet précis ; réévaluation des droits et de l'empreinte avant application. | L'approbateur autorise-t-il un accès ou devient-il éditeur de l'autre composant ? |
| B07 | Secrets ; premier déploiement | RG-PAR-17 autorise un coffre existant ; le secret est généré avant déploiement, mais doit déjà exister au stage Aperçu. | Aucun composant du coffre n'existe à déployer préalablement dans IFS. | Première release toujours en échec dans ce cas. | Certaine dans ce cas | **Élevé — jalon 2** | Préparation explicite du secret avant aperçu, ou exclusion de ce mode avec prérequis manuel visible. | La préparation qui écrit un secret nécessite-t-elle une approbation distincte ? |
| B08 | Cycle de vie ; opérations | UC-CMP-04 retire les fichiers du composant et laisse l'unité pour suppression manuelle. DEC-85 promet la révocation à la release. | Il n'existe plus de release générée pour finir les retraits du composant supprimé. | Accès persistants ; responsabilité et état final incertains. | Élevée sur suppression de composant | **Élevé — maintenance du pilote** | État « retrait demandé », paquet de décommissionnement conservé, révocations prouvées avant archivage ; intervention manuelle explicite pour les données. | Quand la suppression est-elle considérée terminée, cible par cible ? |
| B09 | Confidentialité ; incident | RG-HIS-12 expurge seulement les révisions non publiées ; les autres artefacts internes restent hors contrat d'expurgation. | Le secret peut continuer à être téléchargeable depuis IFS après l'action de remédiation. | Exposition prolongée malgré une interface annonçant l'expurgation. | Plausible | **Élevé — avant production** | Bloquer l'accès aux artefacts contaminés, expurger ou retirer les révisions touchées selon une exception documentée ; empêcher une restauration de réintroduire la valeur. | Quelles copies internes sont neutralisées et lesquelles subsistent avec une durée annoncée ? |
| B10 | Données ; maintenabilité | DEC-92 promet régénération identique et correction des versions supportées. | Un même numéro de catalogue peut produire un autre résultat après correctif. | Révision non reproductible ; diff inattendu ; confusion en audit. | Élevée à la première correction | **Élevé — avant conception du versionnement** | Versions de correctif immuables et manifeste complet des émetteurs/modules ; une mise à jour produit une nouvelle révision, une ancienne reste identifiable. | La version dangereuse reste-t-elle seulement consultable ou encore régénérable ? |
| B11 | Import ; qualité des données | RG-PRJ-09 affirme qu'un nouveau code implique de nouveaux noms. Les noms forcés et gabarits sans jeton projet restent possibles. | Une copie peut viser les mêmes identifiants Azure sous une autre unité de gestion. | Collision, gestion concurrente ou modification d'une ressource existante. | Élevée avec conventions personnalisées | **Élevé — avant livraison de l'import JSON** | Recalculer et comparer les identifiants effectifs par cible ; imposer renommage, référence existante ou transfert explicite. | Un clone est-il obligatoirement isolé de toutes les ressources gérées d'origine ? |
| B12 | Intégrations | Aperçu GitHub sans `environment:`, mais fédérations, variables et secrets définis par environnement dans le kit. | Le job n'obtient pas le contrat d'identité et les données de l'environnement qu'il n'utilise pas. | Aperçu GitHub impossible ou réintroduction prématurée de l'approbation. | Certaine avec le kit décrit | **Élevé — lot 2 ; à explorer dans P6** | Définir un contexte d'aperçu distinct et ses permissions ; décrire séparément l'accès aux secrets nécessaires. | Quel sujet OIDC et quel magasin de données pour l'aperçu et la CI ? |
| B13 | Données ; propriété | RG-PAR-08 donne l'écriture d'un secret de coffre existant au composant consommateur ; plusieurs consommateurs restent permis. | Deux composants peuvent réclamer la même propriété si le secret et son alimentation sont identiques. | Écritures concurrentes ; rotation/redémarrage ambigu ; retrait prématuré. | Plausible | **Élevé — avant partage de coffres existants** | Propriétaire explicite unique pour chaque couple coffre/secret résolu par cible ; autres composants lecteurs ; transfert de propriété documenté. | Comment est choisi l'écrivain si le coffre n'est pas un composant IFS ? |
| B14 | Adoption ; intégrations | RG-APP-22 accepte les pipelines du client, mais son contrat ne mentionne pas la participation au verrou commun ; le code source reste un champ applicable à tous. | L'autonomie offerte n'a pas encore un contrat fonctionnel complet. | Courses infra/application ; obligations de build inutiles pour un usage infrastructure seule. | Plausible | **Élevé — avant livraison de ce mode** | Spécifier synchronisation, champs facultatifs et validation propres à ce mode ; afficher les garanties et observations indisponibles. | Un pipeline existant peut-il respecter ce contrat sans être réécrit ? |
| B15 | Valeur ; finance ; gouvernance | Cibles globales définies, mais sortie du pilote laissée à « les mesures justifient l'élargissement » ; P5 est dans la démonstration, pas dans la condition P1–P4. | On peut déclarer le pilote réussi après beaucoup d'assistance ou sans reprise démontrée. | Faux signal commercial ; extension prématurée du coût et du périmètre. | Élevée sans règle préalable | **Moyen — avant recrutement** | Exiger P5, fixer délai et dénominateur d'activation, mesurer support et attente, comparer à la méthode habituelle, fixer les critères de poursuite. | Quel résultat conduit à poursuivre, resserrer le segment ou arrêter ? |
| B16 | Conformité ; audit | EXG-06 présente 13 mois comme « dans la fourchette recommandée par la CNIL ». | La recommandation générale citée est de six mois à un an ; au-delà il faut une justification adaptée. | Justification de conservation inexacte dans la documentation. | Certaine pour la formulation | **Moyen — avant production** | Corriger la justification : durée produit fondée sur les besoins documentés, ou ajuster la durée. | Pourquoi treize mois sont-ils nécessaires pour ce traitement ? |

### 2.2 Contre-exemples et solutions à préciser

#### B01 — Protection des données et révocation des accès

Scénario : `prd` protégée → RG avec `CanNotDelete` → attribution Key Vault Secrets User → retrait de la liaison → détachement Bicep → suppression RBAC refusée. Microsoft indique explicitement qu'un verrou de suppression sur une ressource ou un RG empêche de supprimer ses attributions RBAC. Les ressources d'extension héritent aussi des verrous. [Documentation Microsoft des verrous](https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/lock-resources).

**Décision nécessaire :** protection Azure retenue, acteur capable de la modifier, restauration de la protection après interruption. Déplacer simplement le verrou du RG vers la ressource ne suffit pas nécessairement, à cause de cet héritage. Une levée temporaire est une option à évaluer, pas une solution acquise. Le pilote doit choisir et démontrer un comportement complet.

#### B02 — La trace doit précéder la mutation

1. Une pile gère l'attribution R.
2. La nouvelle révision retire R ; la release met à jour la pile, qui la détache.
3. L'agent s'arrête avant la suppression de R et avant l'artefact final.
4. À la relance, la pile ne gère plus R ; la différence pile/modèle ne retrouve donc pas R.

Le rapport final est utile pour informer IFS ; il ne suffit pas comme mécanisme de reprise. Recommandation : un journal appartenant au client, indexé par unité et cible, contenant les opérations en attente et leurs identifiants stables. Il doit survivre à l'agent et ne pas dépendre de la disponibilité d'IFS. Les tentatives ultérieures consomment aussi ce journal, même si elles déploient une révision plus récente.

Élargir également « partiellement appliquée » : une étape IaC peut elle-même échouer après avoir créé plusieurs ressources. Ne pas réserver cet état aux étapes qui suivent un déploiement IaC réussi. Sans preuve suffisante, afficher « résultat indéterminé, réconciliation nécessaire », jamais « aucun changement » par déduction de l'absence de rapport.

#### B03 et B04 — Une approbation porte sur un effet et une capacité

Les contrôles Azure DevOps s'appliquent aux ressources consommées par un stage ; un verrou de l'environnement ne protège pas un stage qui ne l'utilise pas. La documentation prévoit aussi des contrôles sur les service connections. [Approbations et contrôles Azure DevOps](https://learn.microsoft.com/en-us/azure/devops/pipelines/process/approvals?view=azure-devops).

**Inférence à partir de la spec :** si un auteur autorisé peut modifier un pipeline qui utilise la connexion privilégiée avant le stage protégé, l'approbation de cet environnement ne constitue pas à elle seule la barrière annoncée. Cela dépend des droits réels sur le dépôt, les pipelines et les connexions ; ce n'est pas une faille observée dans un logiciel exécuté.

Spécifier les capacités effectives de quatre usages : aperçu, build/push, déploiement applicatif, déploiement infrastructure. Cela peut conduire à davantage de connexions ou à des modèles imposés et protégés. Les contrôles de modèle requis sur une connexion sont une possibilité documentée, à intégrer à un contrat cohérent. [Modèles de sécurité Azure Pipelines](https://learn.microsoft.com/en-us/azure/devops/pipelines/security/templates?view=azure-devops).

Pour l'empreinte, inclure les retraits, les opérations de données, les images et routages reconduits, la cible, les versions et l'état pertinent. Exclure les horodatages et variations sans conséquence. Un seul résumé textuel de what-if n'est pas un contrat suffisant. Le résultat attendu en cas de modification concurrente doit être explicite : conserver la nouvelle image sans changer les effets approuvés, ou demander une nouvelle approbation.

#### B05 et B13 — « Un propriétaire » ne définit pas encore un ordre exécutable

Exemple SQL : base dans `data`, application avec identité système dans `orders`. La règle ordonne `data` avant `orders`. Le script de `data` veut pourtant l'identifiant de l'identité créée dans `orders`. Le propriétaire de l'opération, sa phase et ses dépendances doivent être définis ensemble. Une simple référence externe ne crée pas une identité encore absente.

Exemple de secret : `orders` et `billing` consomment `shared-token` dans le même Key Vault existant. L'alimentation peut être identique, donc le contrôle actuel des alimentations différentes ne suffit pas. Définir aussi l'unicité sur les identifiants Azure résolus, pas uniquement sur les objets du modèle. Un coffre `Single` partagé entre `dev` et `prd` nécessite le même raisonnement : même nom de secret signifie même emplacement physique.

#### B06 — Approbation entre équipes

Alice modifie `orders`, Bob modifie `core`. Alice demande un accès au coffre `core`. Bob peut autoriser cet accès mais ne possède pas les droits sur `orders`. Si appliquer la proposition exige tous les droits de l'auteur d'une modification directe, Bob est bloqué. Un architecte ayant les deux portées résout ce cas, au prix d'une dépendance humaine qui doit être assumée.

La solution de produit est un consentement précis des deux parties : demande source, autorisation cible, portée et effets approuvés, expiration de l'approbation si ces effets changent. Le service applique uniquement cette opération, sans attribuer de droits d'édition permanents supplémentaires. Pour le pilote à coordinateur unique, cette fonction peut rester différée.

#### B09 et B10 — Exceptions à l'immuabilité

Deux contrats différents sont nécessaires : retirer un artefact contaminé et remplacer une version vulnérable. Dans le premier cas, préserver la trace de l'incident sans rendre le secret consultable. Dans le second, conserver des identifiants immuables de versions et signaler les versions devenues non utilisables. Ne pas promettre simultanément modification silencieuse d'une version et reproduction identique de son contenu.

#### B12 — Aperçu GitHub

Le sujet OIDC par défaut dépend notamment de l'environnement associé au job ; sans cet environnement, il suit un autre contexte, par exemple la branche. Le kit qui ne crée que le sujet `repo:…:environment:…` ne couvre donc pas le job d'aperçu sans environnement. [Référence OIDC GitHub](https://docs.github.com/en/actions/reference/security/oidc). La séparation infrastructure/application exige aussi de clarifier les noms des variables d'identité du kit GitHub.

#### B16 — Conservation des journaux

La CNIL recommande généralement six mois à un an et prévoit des durées supérieures justifiées selon les finalités et risques. Treize mois n'est donc pas automatiquement interdit ; la justification « dans la fourchette » est incorrecte. [Recommandation CNIL sur la journalisation](https://www.cnil.fr/fr/la-cnil-publie-une-recommandation-relative-aux-mesures-de-journalisation).

### 2.3 Cohérence documentaire à terminer

- **Bicep :** DEC-85 dit « la pile détache tout » ; le tableau [22 § 3.2](../specs/22-pipelines.md) autorise encore `actionOnUnmanage` en suppression selon le composant. Choisir la règle et son effet sur les ressources à supprimer hors production.
- **Numéro de révision :** DEC-94 supprime le bruit dans les fichiers ; RG-SUI-02 exige le numéro dans les variables et le nom d'exécution. Préciser qu'il est chargé à l'exécution depuis le manifeste, si c'est l'intention, pour éviter de réécrire chaque pipeline à chaque génération.
- **Lots :** les brouillons et restaurations du jalon 2 ainsi que les demandes d'accès dépendent des propositions, annoncées au jalon 3 avec MCP. Extraire le mécanisme interne de proposition de la livraison du serveur MCP ; de même, aligner l'export du pilote avec l'export annoncé au jalon 3.
- **Statut des questions :** remplacer « aucune question sans réponse » par « arbitrages connus consignés ; points et preuves ouverts ci-dessous ». La préséance des décisions ne remplace pas l'alignement des documents opérationnels.

### 2.4 Couverture des regards et des thèmes

| Regard / thème demandé | Conclusion de cette passe |
|---|---|
| Utilisateur et UX | Une erreur doit dire qui peut agir et quel état a réellement changé : B02, B06, B07, B08, B14. |
| Produit et business | Pilote bien plus pertinent ; mesurer l'évolution sans assistance et la volonté de poursuivre : B15. |
| Métier et opérations | Décommissionnement et propriété des accès sont des processus à part entière : B05, B08, B13. |
| Architecture et intégrations | Les principaux trous sont aux frontières entre phases, identités et fournisseurs : B01 à B05, B10, B12. |
| QA | Les preuves actuelles doivent inclure interruptions brutales, verrous réels, effets partiels dans IaC et deux équipes sans administrateur commun. Voir § 3. |
| Sécurité et conformité | Révocation réelle, capacité de déploiement, copies contaminées et conservation : B01, B03, B09, B16. |
| Support | L'opérateur a besoin d'une opération identifiable et reprenable, même sans artefact final : B02, B08. |
| Finance et direction | Coût d'accompagnement, reproductibilité et support d'un parc de versions : B10, B15. |
| Performance et disponibilité | Les budgets ajoutés répondent à un manque. Le verrou par cible sérialise désormais tous les composants : mesurer attente et débit avec les pilotes avant de promettre la grande échelle. Aucune capacité réelle n'a été mesurée ici. |
| Accessibilité, internationalisation, compatibilité | Les corrections focus/toucher et mobile sont prises en compte. Aucun nouveau blocage établi dans cette passe ; prévoir validation de ces exigences lors de la réalisation. |
| Mesure et gouvernance | Ne pas confondre réponse écrite, preuve technique et adoption : B15, registre P1–P6. |

## 3. Cas limites à ajouter aux critères d'acceptation

Ces scénarios sont proposés ; ils n'ont pas été exécutés.

| Cas | Situation | Comportement attendu | Décision à prendre |
|---|---|---|---|
| T01 | Retrait RBAC sur RG protégé par `CanNotDelete` | Révocation effective démontrée et conservation des données ; erreur explicite sinon. | Stratégie de verrou et acteur habilité — B01. |
| T02 | Agent tué après détachement, avant suppression et rapport | Prochaine exécution retrouve et finit le retrait ; pas de succès supposé. | Journal durable et stockage — B02. |
| T03 | IaC crée deux ressources puis échoue sur la troisième | État partiel ou indéterminé, liste des opérations connues et reprise. | Preuve de mutation pendant l'étape IaC — B02. |
| T04 | Une application est livrée après l'aperçu d'infrastructure | L'infrastructure ne restaure pas silencieusement l'image précédente. | Relecture et invalidation de l'empreinte — B04. |
| T05 | Alice et Bob n'ont chacun accès qu'à leur composant | Demande approuvable sans droits permanents sur les deux composants ; refus tracé si modèle centralisé choisi. | Double consentement ou administrateur commun — B06. |
| T06 | Demande approuvée puis destination ou rôle modifié | Approbation invalidée ; nouvelle relecture requise. | Empreinte, durée et revalidation des consentements. |
| T07 | Base et identité système consommatrice dans deux composants | Déploiement selon un ordre publié, sans passe cachée. | Propriétaire et phase des opérations de données — B05. |
| T08 | Mot de passe généré absent d'un coffre existant | Préparation guidée ou blocage dès la validation ; pas d'instruction de déployer un composant inexistant. | Préparation avant aperçu — B07. |
| T09 | Deux consommateurs, un secret partagé dans un coffre existant | Un écrivain connu ; supprimer un lecteur ne supprime pas le secret utile à l'autre. | Déduplication, références et transfert — B13. |
| T10 | Composant supprimé alors qu'une cible est indisponible | Retrait en attente visible ; artefacts de retrait conservés pour cette cible. | Délai et responsabilité de fin de vie — B08. |
| T11 | Secret expurgé puis téléchargement d'une vieille révision ou restauration d'une sauvegarde | Secret neutralisé ou accès bloqué ; incident et copies externes clairement signalés. | Exception à l'immuabilité et réapplication des retraits — B09. |
| T12 | Correctif catalogue après publication ; régénération ancienne | Ancienne empreinte reproductible, ou refus motivé ; jamais remplacement silencieux sous le même identifiant. | Patch immuable et politique de retrait — B10. |
| T13 | Import d'un clone contenant des noms Azure forcés | Collisions signalées avant publication ; choix explicite pour chaque ressource. | Copie, référence ou transfert — B11. |
| T14 | Aperçu GitHub avec fédération limitée à l'environnement protégé | Authentification avec identité d'aperçu prévue ; pas de suppression de l'approbation pour faire fonctionner le job. | Contrat OIDC et variables — B12. |
| T15 | Pipeline applicatif externe pendant une mise à jour IFS | Synchronisation respectée ou avertissement bloquant sur garantie non assurée. | Contrat de livraison client — B14. |
| T16 | Reprise avec une révision plus récente ou une cible ayant sauté plusieurs révisions | Les anciennes opérations en attente sont conservées jusqu'à résolution ; état final rattaché au bon manifeste. | Priorité entre journal d'opérations, état géré et nouvelle intention — B02. |

## 4. Idées de fonctionnalités : valeur et périmètre supplémentaire

Le graphe, l'historique, les modèles de projet, les coûts, la dérive, les notifications et MCP sont déjà prévus. Les propositions ci-dessous indiquent leur **apport supplémentaire**. Les efforts sont relatifs, sans estimation en jours : faible = extension locale ; moyen = parcours et contrat supplémentaires ; élevé = plusieurs composants ou intégrations à coordonner.

### 4.1 Indispensables au MVP : compléter les engagements existants

Ces éléments sont des corrections de cohérence, pas une nouvelle liste de promesses commerciales.

| ID / proposition | Besoin et valeur | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|
| M01 — Fiche d'opération reprenable | Après incident, montrer « fait / restant / incertain », l'exécution à relancer et le responsable ; éviter de perdre un retrait. | B02, journal durable, schéma de rapport et identifiant d'opération. | Élevé | **Bloquant** | Fausse certitude si l'état est déduit de logs incomplets. |
| M02 — Parcours de retrait d'un composant | Conserver les artefacts nécessaires, présenter les accès à révoquer et les données conservées, prouver la fin par cible. | B01, B08, inventaire et règles de propriété. | Moyen | **Élevé** | Suppression prématurée des accès nécessaires au retrait lui-même. |
| M03 — Contrat de préparation par cible | Avant la première release, montrer les prérequis réellement vérifiés, ceux seulement déclarés et leur propriétaire. | Contrôles du kit, sorties non sensibles, dates de vérification. | Moyen | **Élevé** | Présenter une vérification ancienne comme encore certaine. |

M03 approfondit une suggestion précédente refusée comme déjà couverte par `-WhatIf`. L'apport serait une vérification effective de l'authentification, des accès nécessaires, de la connectivité et des dépendances depuis le bon exécuteur. Un mode qui affiche seulement les actions prévues ne démontre pas ces propriétés. Pour le pilote, une commande client et un rapport lisible suffisent ; pas besoin d'un service supplémentaire dans IFS.

### 4.2 Forte valeur après le pilote

| ID / idée | Besoin utilisateur ou métier ; apport supplémentaire | Valeur attendue | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| F01 — Accès avec motif, responsable et date de revue | Prolonger les demandes DEC-89 par un motif métier, un propriétaire et une échéance de réexamen. L'expiration effective serait exécutée par un pipeline client planifié. | Réduire les accès oubliés et faciliter la revue de droits. | B01, B02, B06 ; identité stable des liaisons, planification client et contrôle de validité à chaque déploiement pour éviter de recréer un accès expiré. | Moyen | **Élevé** pour équipes réglementées | Coupure de service si expiration automatique sans notification ni renouvellement. Commencer par rappels et revue. |
| F02 — Mise à jour de modèles réutilisables déjà instanciés | Les modèles actuels démarrent une copie. Ajouter version d'origine, comparaison avec la nouvelle version et adoption sélective des changements. | Maintenir des dizaines de services sans copier-coller les corrections plateforme. | Propositions, diff sémantique, propriété locale vs modèle, versions immuables. | Élevé | **Élevé** après validation du besoin | Écrasement de personnalisations et conflits difficiles. |
| F03 — Rapport de livraison pour pipelines existants | Étendre RG-APP-22 par un format documenté et un petit adaptateur CI qui publie le rapport attendu. IFS le lit quand disponible. | Conserver la matrice de suivi sans remplacer la chaîne du client. | Schéma `ifs-report.json` versionné, corrélation fiable, lecture CI autorisée. | Moyen | **Élevé** pour adoption | Rapports trompeurs ou partiels ; afficher leur provenance et leur couverture. |
| F04 — Diagnostic guidé d'un déploiement échoué | Dépasser l'archive de diagnostic : relier un code d'erreur à l'objet du modèle, au responsable, aux étapes connues et à une action documentée. | Moins d'allers-retours support ; développeur capable de corriger les erreurs courantes. | Rapports structurés, catalogue de causes, filtrage des données sensibles. | Moyen | **Élevé** | Conseil incorrect ; distinguer cause établie, piste et action sûre. Commencer sans IA. |
| F05 — Versions applicatives éligibles au retour arrière | Compléter « relancer le build précédent » par existence de l'artefact, digest, dernière réussite, compatibilité déclarée et lien de relance. | Éviter de découvrir en incident qu'une ancienne image a été purgée ou n'est plus compatible. | Lecture du registre/pipeline par un job client, rétention, déclaration de compatibilité. | Moyen | **Élevé** si retour arrière vendu | Impossible de garantir automatiquement la compatibilité des données applicatives. |
| F06 — Dossier de transmission et de sortie | Compléter ZIP + JSON par manifeste des versions, propriétaires, procédures de reprise, dépendances et vérification des artefacts nécessaires à l'autonomie. | Livrable exploitable pour ESN, changement d'équipe et départ d'IFS. | Export existant, inventaire, versions épinglées ; aucune valeur de secret. | Moyen | **Moyen** | Confondre autonomie du pipeline et disponibilité éternelle de ses dépendances externes. |
| F07 — Revue des ressources conservées avec coût et responsable | Enrichir l'inventaire des ressources détachées par responsable, motif, date de revue et coût estimé connu/inconnu. | Réduire les dépenses résiduelles sans supprimer automatiquement des données. | Inventaire fiable B02, estimation lot 2, notifications existantes. | Moyen | **Moyen** | Coût incomplet ou obsolète ; garder la suppression dans le processus client. |

### 4.3 Différenciantes, à valider avant d'investir

| ID / idée | Besoin utilisateur ou métier ; apport supplémentaire | Valeur attendue | Prérequis / dépendances | Effort | Priorité | Risques |
|---|---|---|---|---|---|---|
| F08 — Ensemble de versions validé pour une promotion | Associer révisions d'infrastructure et digests applicatifs d'un ensemble de composants, avec la preuve que cet ensemble a fonctionné en dev. | Réduire les combinaisons non maîtrisées lors du passage en production. | Rapports, identifiants immuables, conventions de compatibilité ; exécution toujours dans la CI client. | Élevé | **Moyen** | Glissement vers un orchestrateur général et dépendance à IFS. Limiter d'abord au manifeste et aux contrôles générés. |
| F09 — Accepter une dérive comme proposition de modèle | Au lieu de reporter manuellement une modification Azure, préparer une proposition pour les propriétés supportées, à partir du rapport client. | Réconcilier une intervention d'urgence avec le modèle sans ressaisie. | Dérive lot 2, valeurs déclaratives autorisées, mapping catalogue et validation des droits. | Élevé | **Moyen** | Importer du bruit, une modification malveillante ou un secret ; aucune application automatique. |
| F10 — Dossier de preuve d'architecture par révision | Réunir changements, motifs, approbations, version des politiques, preuves de livraison et limites connues ; version lisible pour l'équipe sécurité. | Réduire le temps de revue et faciliter la transmission aux auditeurs. | Audit, propositions, rapports fiables ; modèle de dossier stable. | Moyen | **Moyen** | Présenter le dossier comme une certification de conformité. Il prouve des faits bornés. |
| F11 — Mode démonstration sans droits Azure | Parcours du modèle à une PR exemple avec données fictives, étapes d'installation expliquées et déploiements clairement simulés. | Tester l'intérêt avec des développeurs avant les délais de consentement du client. | Projet témoin, génération hors connexion, scénario UX limité. | Faible à moyen | **Moyen** | Activation artificielle ; séparer absolument démonstration et réussite réelle dans les métriques. |
| F12 — Comparaison de deux variantes d'architecture | Deux propositions sur la même base ; comparer conséquences, interventions nécessaires, exposition et coûts déjà estimables. | Aider l'architecte à choisir entre variantes avant de les intégrer au modèle. | Brouillons, analyse d'impact et estimation existantes ; aucune simulation de performance inventée. | Élevé | **Faible** | Élargissement de l'éditeur et comparaison trompeuse lorsque données ou prix manquent. |

### 4.4 À éviter ou repousser

| Proposition | Besoin supposé | Valeur aujourd'hui | Dépendances / effort | Priorité | Risque et alternative |
|---|---|---|---|---|---|
| Correcteur autonome d'infrastructure par IA | Réduire le travail d'exploitation. | Non démontrée avec les pilotes. | Droits Azure, observation fiable, décisions de reprise ; **élevé**. | **Faible — repousser** | Change la frontière de responsabilité. Préférer F04 et des propositions relues. |
| Marketplace publique de modules et de modèles | Accélérer la réutilisation. | Prématurée sans base active et demande mesurée. | Validation, provenance, maintenance, support ; **élevé**. | **Faible — repousser** | Coût d'écosystème avant preuve produit. Commencer par F02 dans une organisation. |
| Infrastructure éphémère complète pour chaque PR | Tester tous les changements isolément. | Très variable selon le client. | Création, quotas, coûts, secrets, nettoyage ; **élevé**. | **Faible — hors périmètre confirmé** | Ne pas réouvrir ce chantier pour le pilote ; exploiter validations et environnement de test partagé. |
| Extension immédiate de tous les profils de build et langages IaC | Servir davantage de clients. | Dilue l'apprentissage du pilote. | Multiplication des variantes et de leur support ; **élevé**. | **Faible avant signal d'adoption** | Conserver la feuille de route et avancer seulement selon la demande prouvée. |

**Sélection recommandée après le socle :** F04 diagnostic guidé, F03 intégration du suivi aux pipelines existants, F02 mise à jour des modèles réutilisés. Ce sont trois paris distincts : réduction du support, réduction du coût d'adoption, et différenciation dans la maintenance. Les choisir selon les observations des pilotes, pas les lancer ensemble par défaut.

## 5. Challenge du MVP corrigé

### Ce qui peut encore être supprimé ou reporté

- SQL du pilote tant qu'aucune équipe recrutée n'en a besoin ; une app, un secret et un accès réel suffisent à éprouver le premier cycle.
- Rôles par composant et double approbation entre équipes pendant le pilote à coordinateur unique, avec cette limitation affichée.
- Multiples profils de build, import d'infrastructure, graphe éditable, catalogue de modules client et stratégies de livraison avancées.
- Interface riche de reprise : commencer par un rapport lisible et une commande sûre. Le mécanisme durable de reprise, lui, reste indispensable.

### Ce qui manque pour que le MVP tienne sa promesse

Révocation compatible avec la protection Azure ; reprise après interruption ; frontière de confiance des connexions ; préservation des images livrées ; preuve de l'état effectivement appliqué. Si la suppression de composant est proposée, son parcours de retrait doit être complet. Sinon l'action doit être explicitement indisponible sur un composant déployé pendant le pilote.

### Plus petit périmètre livrable

Conserver le jalon 0 : un dépôt, Azure DevOps, Bicep, deux environnements, socle + service, un conteneur, identités managées, Key Vault, PR, approbation production. Exiger une création, une évolution de configuration, un ajout/retrait d'accès et une reprise après incident. Garder l'export du code et du modèle pour prouver la réversibilité commerciale.

**Hypothèse principale à tester :** une équipe peut créer puis faire évoluer son service avec moins de temps actif et moins d'assistance plateforme que sa méthode habituelle, sans corrections manuelles des fichiers gérés.

### Décisions avant développement du moteur de livraison

1. Protection contre la suppression et procédure de révocation.
2. Journal durable et machine d'états d'une opération interrompue.
3. Capacité réelle de chaque identité et droits des auteurs de pipelines.
4. Contenu de l'empreinte approuvée et règles de concurrence.
5. Propriétaire, phase et dépendances de chaque objet dérivé.
6. Versionnement immuable du catalogue et des générateurs.

## 6. Verdict, priorités et plan d'action

### Verdict global

**Non prêt pour le développement du mécanisme de livraison tel qu'écrit ; prêt pour sa conception détaillée et les prototypes du pilote.** Les corrections ont réduit le risque de périmètre et explicité de nombreux comportements. Les problèmes restants touchent les garanties centrales : retirer un droit, reprendre une exécution, approuver les effets réels et réussir le premier déploiement.

### Les cinq risques prioritaires

1. **Révocation bloquée par la protection Azure** — B01.
2. **Opérations perdues après une interruption** — B02.
3. **Permissions de CI permettant plus que le parcours approuvé** — B03.
4. **Concurrence entre aperçu, déploiement d'infrastructure et livraison applicative** — B04.
5. **Ordre inexécutable pour certains objets dérivés** — B05, B07, B13.

### Les cinq opportunités les plus intéressantes

1. Diagnostic guidé de livraison — F04.
2. Suivi compatible avec les pipelines existants — F03.
3. Mise à jour des modèles déjà instanciés — F02.
4. Gestion de la durée et de la responsabilité des accès — F01.
5. Ensemble de versions validé pour une promotion — F08, après preuve du besoin.

### Les dix questions à trancher

1. Comment supprimer effectivement un rôle dans une cible dont les ressources sont protégées contre la suppression ?
2. Où persiste une révocation en attente avant le détachement, et comment une nouvelle exécution la retrouve-t-elle ?
3. Quels acteurs peuvent modifier le YAML ou les extensions, et quelles identités peuvent-ils obtenir avant approbation ?
4. Que couvre précisément l'empreinte approuvée, notamment l'image et le trafic déjà en service ?
5. Quelle unité crée les utilisateurs SQL quand la base et l'identité consommatrice appartiennent à deux composants ?
6. Comment deux équipes sans droits croisés approuvent-elles un accès ?
7. Qui prépare un mot de passe absent dans un coffre existant, et qui devient écrivain unique d'un secret partagé ?
8. Quel événement prouve la fin du retrait d'un composant dans toutes ses cibles ?
9. Quelles exceptions documentées à l'immuabilité permettent d'expurger un secret publié et de retirer une version vulnérable ?
10. Quels seuils de réussite, d'assistance, de délai et d'intention de poursuite justifient l'élargissement commercial ?

### Plan d'action en trois horizons

| Horizon | Actions prioritaires | Livrable de décision |
|---|---|---|
| **Avant cadrage / conception détaillée** | Confirmer le segment pilote, H2 et H4 ; distinguer promesses pilote et lots futurs ; fixer critères de sortie et coût acceptable d'assistance ; sélectionner une seule piste de fonctionnalité à investiguer. | Fiche pilote avec cas couverts/exclus, responsables, mesures et décision de poursuite. |
| **Avant développement du mécanisme concerné** | Résoudre B01–B05 ; spécifier opérations durables et identités ; traiter B07/B13 si dans le périmètre ; aligner versions et lots ; enrichir P1–P6 avec T01–T04/T07/T16. | Contrats de livraison cohérents et résultats des prototypes, joints au registre des preuves. |
| **Avant mise en production** | Démontrer révocation et reprise sous interruption ; borner le mode pipelines client ; fermer B08/B09 ; documenter conservation B16 ; achever le pilote avec P5 et mesures d'autonomie. | Critères d'acceptation satisfaits, procédures d'incident, preuves de récupération et décision explicite de lancement. |

**Conseil final :** ne pas répondre à cette contre-revue en ajoutant seulement seize décisions. Pour chaque correction, joindre un scénario complet avec état initial, acteur, permissions, ordre des effets, interruption possible, état final et preuve attendue. C'est ce qui permet de fermer le constat durablement.
