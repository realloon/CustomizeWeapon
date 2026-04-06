# Customize Weapon

Customize Weapon is a weapon customization framework Mod designed for RimWorld. It provides a powerful underlying system that allows developers to create highly configurable weapons and add deep customization features to existing ones.

CWF aims to be an extensible platform, enabling other Mod authors to easily introduce new weapon parts, modules, and customization options to RimWorld.

## Development Environment

1. **Clone**: Run `git clone https://github.com/realloon/CustomizeWeapon.git` in your local RimWorld mod directory.
2. **Link**: This repository does not include textures, so symlink the `Textures` folder from the Steam Workshop version of [Customize Weapon](https://steamcommunity.com/sharedfiles/filedetails/?id=3550585103) into this repository.
3. **Build**: Run `dotnet build ./Source/CWF.slnx`. NuGet dependencies will be restored automatically.

Example symlink command:

```bash
ln -s \
  "/path/to/your/Steam/steamapps/workshop/content/294100/3550585103/Textures" \
  "/path/to/your/RimWorld/Mods/CustomizeWeapon/Textures"
```