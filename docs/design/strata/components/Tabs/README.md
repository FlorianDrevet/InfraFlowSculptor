# Tabs

Sections d'une même page (projet, composant, ressource, révision), soulignées en `signal` quand actives.

- Compteur mono en `text-3` quand il informe ; `countTone: 'ember'` quand il demande une action (constats à traiter).
- Chaque onglet a son lien profond (`?tab=…`) pour pouvoir être partagé.
- Un onglet qui n'a pas de sens pour la ressource (Application pour un Key Vault, Enfants pour une Container App) est absent, jamais grisé.
- Ordre fixe, du plus utilisé au moins utilisé ; pas plus de huit onglets. Ressource applicative : Général, Propriétés, Liaisons, Identité et accès, Paramètres, Application, Réseau.
