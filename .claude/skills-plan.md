# Plan des Skills Optimales pour BetterTrumpet

## 🎯 Objectif
Créer des skills réutilisables pour automatiser les tâches répétitives et accélérer le développement de BetterTrumpet.

---

## 📦 Skills Essentielles (High Priority)

### 1. `/build` - Build & Test Rapide
**Pourquoi :** On rebuild constamment pendant le dev
**Actions :**
- Build le projet en Debug x86
- Ignore les erreurs des projets non-critiques (ColorTool, Package)
- Vérifie que BetterTrumpet.exe est créé
- Lance automatiquement l'exe en background
- Affiche un résumé (temps de build, taille exe)

**Variantes :**
- `/build --release` : Build en Release
- `/build --clean` : Clean + Rebuild
- `/build --test` : Build + lance l'app + attend feedback

---

### 2. `/restart` - Restart BetterTrumpet
**Pourquoi :** On kill et relance l'app très souvent
**Actions :**
- Tue tous les processus BetterTrumpet.exe
- Attend 1s
- Relance Build/Debug/BetterTrumpet.exe
- Vérifie que le process démarre (tasklist)
- Affiche PID et mémoire

---

### 3. `/fix-issue <number>` - Fix une GitHub Issue
**Pourquoi :** Workflow structuré pour les bugs
**Actions :**
- Fetch l'issue depuis GitHub (gh issue view)
- Lit la description et comprend le problème
- Recherche dans le code les fichiers pertinents
- Propose un plan de fix
- Implémente le fix
- Test et vérifie
- Commente sur l'issue avec le fix

**Exemple :** `/fix-issue 13` → Fix le backdrop bug

---

### 4. `/xaml-debug` - Debug XAML Layout
**Pourquoi :** On a galéré avec le layout du device header
**Actions :**
- Lit le fichier XAML spécifié
- Analyse la structure (Grid, StackPanel, etc.)
- Identifie les problèmes de layout (Z-Index, positioning, etc.)
- Propose des corrections
- Explique le layout visually (ASCII art si nécessaire)

**Exemple :** `/xaml-debug FlyoutWindow.xaml device-header`

---

### 5. `/test-feature <feature>` - Test une Fonctionnalité
**Pourquoi :** Vérifier rapidement qu'un feature fonctionne
**Actions :**
- Build le projet
- Lance l'app
- Donne des instructions de test claires
- Attend le feedback utilisateur
- Log les résultats

**Exemple :** `/test-feature right-click-device-header`

---

## 🔧 Skills Utilitaires (Medium Priority)

### 6. `/docs-update` - Mise à Jour de la Documentation
**Actions :**
- Lit les changements récents (git diff)
- Met à jour README.md si nécessaire
- Met à jour docs/CLI.md pour les nouvelles commandes CLI
- Vérifie que la doc est cohérente

---

### 7. `/grep-usage <symbol>` - Trouver l'Usage d'un Symbol
**Actions :**
- Grep pour trouver toutes les utilisations d'une classe/méthode/propriété
- Affiche les fichiers et lignes
- Propose de naviguer vers les résultats

**Exemple :** `/grep-usage DeviceViewModel.MakeDefaultDevice`

---

### 8. `/commit-and-push` - Commit & Push Intelligent
**Actions :**
- Analyse les changements (git status, git diff)
- Propose un commit message pertinent
- Demande confirmation
- Stage les fichiers
- Commit avec co-authored-by Claude
- Push vers origin (nouvelle branche si nécessaire)

---

### 9. `/context-menu-add <target> <action>` - Ajouter un ContextMenu
**Pourquoi :** Pattern répété pour ajouter des menus
**Actions :**
- Identifie l'élément XAML cible
- Ajoute un ContextMenu avec l'action spécifiée
- Crée le handler dans le code-behind (.xaml.cs)
- Ajoute les using nécessaires
- Build et test

**Exemple :** `/context-menu-add DeviceHeader "Set as default"`

---

### 10. `/theme-debug` - Debug Theme/Styling Issues
**Pourquoi :** Le bug backdrop était un problème de thème
**Actions :**
- Analyse ThemeManager et ThemeBindingInfo
- Vérifie l'ordre d'initialization
- Identifie les timing issues
- Propose des fix pour les brush/color problems

---

## 🚀 Skills Avancées (Low Priority - Nice to Have)

### 11. `/release` - Préparer une Release
**Actions :**
- Bump la version dans les fichiers appropriés
- Update CHANGELOG.md
- Build en Release
- Crée les packages (Chocolatey, Winget si applicable)
- Crée un GitHub Release draft
- Push les tags

---

### 12. `/analyze-performance` - Analyse de Performance
**Actions :**
- Identifie les méthodes appelées fréquemment
- Analyse le temps de startup
- Suggère des optimisations
- Profile la mémoire

---

### 13. `/ui-mockup <description>` - Créer un Mockup UI
**Actions :**
- Génère un mockup XAML basé sur la description
- Crée la structure Grid/StackPanel appropriée
- Ajoute des placeholder styles
- Permet d'itérer rapidement sur le design

---

## 📝 Structure des Skills

Chaque skill devrait avoir :
```markdown
---
name: skill-name
description: One-line description
tags: [build, debug, xaml, etc.]
---

# skill-name

## Description
Detailed description

## Usage
/skill-name [options]

## Options
--option1: Description
--option2: Description

## Examples
/skill-name --example

## Implementation
Step-by-step what the skill does
```

---

## 🎨 Naming Convention
- Verbes d'action : `/build`, `/restart`, `/fix-issue`
- Kebab-case : `/xaml-debug`, `/context-menu-add`
- Court et mémorable

---

## 📊 Priorités d'Implémentation

**Phase 1 (Immédiat) :**
1. `/build` - Le plus utilisé
2. `/restart` - Le deuxième plus utilisé
3. `/fix-issue` - Workflow structuré

**Phase 2 (Cette semaine) :**
4. `/xaml-debug`
5. `/test-feature`
6. `/commit-and-push`

**Phase 3 (Futur) :**
7-13. Les autres skills selon les besoins

---

## 💡 Skills vs Scripts

**Utiliser une skill quand :**
- Tâche répétitive avec variations
- Nécessite de la logique conditionnelle
- Besoin d'interaction utilisateur
- Workflow multi-étapes

**Utiliser un script (.cmd/.ps1) quand :**
- Tâche simple et linéaire
- Pas de logique conditionnelle
- Pas d'interaction nécessaire

---

## 🔄 Maintenance

- Réviser les skills tous les mois
- Supprimer celles qui ne sont plus utilisées
- Améliorer basé sur les retours
- Documenter les cas d'usage réels

