# ResourceIcon

Représente un type de ressource Azure par son icône officielle Microsoft (Azure Architecture Icons V24), avec une tuile d'abréviation en repli.

- `type` prend le nom technique du type de ressource du catalogue IFS (`KeyVault`, `WebApp`…). La table `Strata.resourceTypes` donne pour chaque type : nom complet du service, abréviation de nommage, catégorie, fichier d'icône.
- Tailles : 16 px dans le texte, 20–24 px dans les listes denses, 28 px dans les lignes de ressource, 40 px en en-tête de page.
- Les icônes sont embarquées en data URI dans le bundle : le composant fonctionne hors ligne, dans le canvas comme dans l'application.

**Règles Microsoft, à respecter partout** (FAQ du pack d'icônes) :
1. Ne jamais recadrer, retourner, pivoter, recolorer ou déformer une icône. Pas de filtre, pas d'opacité réduite, pas de pastille dessinée par-dessus.
2. Le nom complet du service est écrit près de l'icône, sans la chevaucher. Si le nom n'est pas affiché à côté, `alt` le porte (c'est le défaut) ; mettre `alt=""` seulement quand le nom est déjà écrit juste à côté.
3. Une icône Azure ne représente que le service Microsoft correspondant. Jamais pour IFS lui-même, un composant, un projet ou une action.

`variant="tile"` (abréviation colorée par catégorie) sert quand l'icône n'est pas souhaitable : très petite taille, légende de graphe, export où la licence ne couvre pas l'usage.

Types du catalogue v1 sans icône dans ce système (Microsoft Foundry, AI Search, PostgreSQL, Azure Managed Redis, Static Web App, NAT Gateway, zones DNS…) : tuile d'abréviation de 28 px (rayon `radius-md`, fond `surface-3`, abréviation mono), jusqu'à l'ajout de leur icône officielle. L'icône `redis-cache.svg` représente Azure Cache for Redis et ne sert pas pour Azure Managed Redis.
