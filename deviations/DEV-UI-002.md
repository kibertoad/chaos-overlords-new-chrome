# DEV-UI-002

- Departs from: RULE-UI-012, SCR-UI-004
- Reason: The detailed-sector screen's 3-by-3 minimap also draws the Siege and Big Man pylons,
  scaled, so the player can see neighbouring objective sectors. The original draws them only on
  the city map.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. The Siege and Big Man
  sectors are already marked on the city map, so the minimap tells the player nothing new; it
  saves leaving the sector view to find out whether a neighbour is an objective. It is drawn only
  from what the city map shows, removes no control and changes no rule.
- Dropped: 2026-09-27. FND-UI-018 shows that the original's nine-sector display is a crop
  of the prepared city map, which already carries the pylons, so the rebuild draws them there
  as the original does.
