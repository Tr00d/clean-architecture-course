# Bank : domaine TypeScript

Le projet de départ pour construire le domaine en TDD.

- `packages/domain` : le domaine, vide pour l'instant. C'est un paquet npm séparé, qui ne dépend d'aucune librairie.
- `packages/domain/tests` : ses tests, avec Vitest, à créer au fil des exercices.

Il n'y a rien à démarrer, ni base ni configuration. Depuis `jour-2/hexagon/ts` :

```bash
npm install
npm test            # lance les tests
npm run typecheck   # vérifie les types
```

Tant que le projet ne contient aucun test, `npm test` n'en trouve aucun et se termine sans erreur.

Le fichier `.npmrc` active `install-strategy=linked` : un paquet ne voit que ses propres dépendances. Sans lui, npm rassemble toutes les dépendances à la racine, et le domaine pourrait importer une librairie qu'il n'a jamais déclarée.
