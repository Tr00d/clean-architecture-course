# clean-architecture-course

Cours de 4 jours sur les architectures centrées sur le domaine (hexagonale, Onion, Clean), à partir d'un Bank Kata disponible en C# et en TypeScript.

## Objectifs

À la fin du cours, vous saurez :

- Expliquer pourquoi une architecture N-tier rend le métier difficile à tester et à isoler
- Inverser une dépendance, en comprenant à qui appartient une interface
- Construire un hexagone en TDD, avec ses ports et ses adapters
- Migrer progressivement une application existante vers l'hexagone
- Comparer les architectures hexagonale, Onion et Clean
- Vérifier automatiquement une architecture avec des tests
- Dire quand ces architectures valent leur coût, et quand elles ne le valent pas

## Prérequis

- Savoir écrire des tests automatisés dans votre stack
- Connaître le cycle du TDD : rouge, vert, refactoring
- Connaître la notion de Seam, vue dans le module Legacy Code

## Programme

| Jour                       | Sujet                                             | Ce que vous faites                                                               | Point de départ                 |
|----------------------------|---------------------------------------------------|----------------------------------------------------------------------------------|---------------------------------|
| [Jour 1](jour-1/README.md) | Qu'est-ce qu'une architecture ?                   | Vous testez et faites évoluer une application N-tier, et vous en sentez les limites | `jour-1/start/`                 |
| [Jour 2](jour-2/README.md) | Mettre le domaine au centre                       | Vous concevez un hexagone, puis vous construisez son domaine en TDD              | `jour-2/hexagon/`               |
| [Jour 3](jour-3/README.md) | Refactorer vers l'hexagone                        | Vous migrez l'application, puis vous ajoutez des règles et un nouveau point d'entrée | `jour-3/start/`                 |
| [Jour 4](jour-4/README.md) | Onion, Clean Architecture et tests d'architecture | Vous passez à la Clean Architecture, puis vous protégez votre architecture par des tests | `jour-4/start/`                 |

## Avant le premier jour

Faites ces étapes avant le cours : le premier démarrage télécharge plusieurs images Docker et peut prendre plusieurs minutes.

### 1. Installer les outils

- [Docker](https://docs.docker.com/get-docker/), démarré
- [Supabase CLI](https://supabase.com/docs/guides/local-development/cli/getting-started), version 3 ou supérieure
- Pour C# : le [SDK .NET 10](https://dotnet.microsoft.com/download/dotnet/10.0)
- Pour TypeScript : [Node.js 24 ou supérieur](https://nodejs.org/)
- Un éditeur ou un IDE pour votre stack

Vérifiez vos versions :

```bash
docker --version
supabase --version
dotnet --version   # pour C#
node --version     # pour TypeScript
```

### 2. Démarrer la base de données

Clonez ce dépôt, puis lancez ces commandes depuis sa racine :

```bash
supabase start
supabase db reset
```

### 3. Vérifier votre stack

Suivez le README de votre projet dans `jour-1/start/` pour créer son fichier de configuration, puis lancez ses tests. Pour C#, depuis `jour-1/start/csharp` :

```bash
dotnet test
```

Pour TypeScript, depuis `jour-1/start/ts` :

```bash
npm install
npm test
```

Le test doit passer. Vous êtes prêts pour le premier jour.

Pour arrêter la base, lancez `supabase stop` depuis la racine du dépôt.

## Comment utiliser ce dépôt

Chaque dossier `jour-x/` contient :

- `README.md` : l'énoncé de la journée
- Un ou plusieurs projets de départ, un par stack
- `solution.<stack>.md` : un guide qui reprend les exercices pas à pas

Quelques règles pour que tout se passe bien :

- **Chaque jour a son propre projet.** Vous pouvez repartir du projet du jour, même si vous n'avez pas terminé la veille.
- **La configuration n'est pas versionnée.** Chaque projet relié à la base a besoin de son propre fichier, `appsettings.local.json` en C# ou `.env` en TypeScript, décrit dans son README. Le projet du deuxième jour n'en a pas besoin.
- **La base est commune à tous les jours.** Elle se lance depuis la racine du dépôt, et `supabase db reset` la remet dans son état initial.
- **Gardez votre travail sur une branche Git.** Vous retrouvez ainsi vos changements d'un jour à l'autre, et vous pouvez les partager avec votre mob.
- **Cherchez avant d'ouvrir une solution.** Il en va de même pour les sections repliables des README : leur intérêt tient à la réflexion qui les précède.

## Modifier les guides

Les fichiers `solution.csharp.md` et `solution.ts.md` sont générés à partir de `solution.src.md`. Modifiez uniquement ce dernier, puis régénérez-les :

```bash
python3 scripts/generate-guides.py
```
