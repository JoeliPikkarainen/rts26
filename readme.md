# 3rd person rts demo

## Idea Description

Meta information:

* Third person, control single charcter directly.

* RTS style 10 - 20 minute match instances againts PVP or AI.

* Rival has similar game loop with player being the central character.

Game loop:

* Collect resources and powerups.

* Recruit NPCs

* Put NPCs to collection work/defence/attack by direct interaction.

* Build base and homes for NPCs, defence towers, walls, resource gathering camps, storages.

* Defend from rival player or AI and attack.

* Use wardrum powerup to send all NPCs to power full attack.

## Demo Project Roadmap

| Topic                   | Description                                                                                                                              | Status      |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------------------------------- | ----------- |
| Character               | Player controller integrated with imported character model, collider alignment, basic rotation and movement polish for demo playability. | Done (Demo) |
| Camera                  | Third-person follow camera with adjustable crosshair vertical position and consistent aim ray behavior.                                  | Done (Demo) |
| Resource (Tree)         | Tree resource interaction flow implemented: target/hover info, hit interaction, and pickup loop for resource collection.                 | Done (Demo) |
| Resource Tick Drops     | Gather nodes now drop resources in chunks during harvesting (every health milestone) instead of only dropping at depletion.               | Done (Demo) |
| Building                | Build mode implemented with menu, placement preview, placement validation, prefab-based buildable interface, and inventory cost checks.  | Done (Demo) |
| NPC Recruitment         | Recruitable NPCs implemented with ownership tracking and hostile/non-recruitable variants.                                               | Done (Demo) |
| NPC Task Assignment     | NPC command menu implemented with Follow, Idle, Gather, Defend, Attack, and Wander behavior assignment.                                  | Done (Demo) |
| NPC Gather Loop         | Gather AI implemented: search gatherables, move to resource, gather by damage, pick up dropped items into NPC inventory.                 | Done (Demo) |
| Enemy NPC Combat        | Hostile NPC behavior implemented with autonomous target search, attack, and player/NPC damage handling.                                  | Done (Demo) |
| Resource (Stone)        | Add rock gather nodes, stone pickup item, and stone costs in selected buildings/recipes.                                                 | Done (Demo) |
| Gather Target Selection | Add gather submenu for selecting preferred resource type or fallback to closest gatherable target.                                       | Done (Demo) |
| Resource Storage        | Add storage buildings/chests and transfer flow between player inventory and base storage.                                                | Done (Demo) |
| Base Defense Structures | Add towers and wall segments with simple placement and blocking/collision rules.                                                         | Planned     |
| Enemy Wave/AI           | Add wave spawning, scaling difficulty, and timing system to pressure base defense loop.                                                  | Planned     |
| Wardrum Powerup         | Implement wardrum effect to trigger temporary all-in NPC attack behavior.                                                                | Planned     |
| Save/Load               | Save world state (built structures, recruited NPC ownership/state, inventories).                                                         | Planned     |
| UI/UX Polish            | Improve build menu readability, inventory HUD, status feedback, and control hints for demo clarity.                                      | Planned     |

