# Undercurrent: Combat Rules

darkcrux · moved from the claude.ai doc on 17 Oct 2026. Numbers are starting values; the live values are
in [`data/rules.json`](../data/rules.json), which wins if the two disagree. Items marked *Proposal* are
suggestions not yet confirmed.

**Implemented in `Undercurrent.Core` (M1):** turn order, moving and facing, hit and damage, crits,
counters, vision and Hidden, cooldowns, Charge, Focus, Resonance and the Blood Price, channeling, the
turret, Burning, Bleeding, Shocked, Frozen, Stunned, Braced, Suppressed, Challenged, the fallen
countdown. **Not yet:** classes beyond the prototype's eight, mastery, borrowing, unlocks, switching,
items, revives, the remaining statuses.

## Turn order

- Individual turns: units from both sides act one at a time, like Tactics Ogre.
- Each unit has a wait timer that fills by SPD minus equipment weight. At 100 the unit acts.
- Doing both (move and act) costs 100 wait; doing one costs 80; doing neither costs 60. Acting or
  moving alone brings the next turn sooner.
- The upcoming turn order is shown on screen, so players can plan around who acts next.

## A unit's turn

- Move and act in either order: move then act, or act then move, as in XCOM and D&D.
- **Act-only:** stationary abilities, such as big spells and heavy tech weapons, can't be used on a turn
  the unit moves, and the unit can't move after using one.
- A unit can also skip its action or its move.
- At the end of the turn, the unit chooses a facing. Facing matters for counters and vision.

## Stats

| Stat | What it does |
| --- | --- |
| HP | Health; at 0 the unit falls (perma-death rules depend on difficulty) |
| Resource | Focus, Charge or Resonance, depending on class line (see below) |
| ATK | Physical power: blades, bullets, fists |
| DEF | Reduces physical damage |
| ATN (Attunement) | Elemental power: how strongly the unit channels the Current |
| RES (Resistance) | Reduces elemental damage |
| HIT | Accuracy |
| SPD | Turn speed, and how hard the unit is to hit; lowered by equipment weight |
| LUK | Critical hit chance |
| MOVE, JUMP | Tiles per move and height the unit can climb; set by class and movement type, not by level-ups |

There is no separate evasion stat. Splitting physical and elemental means gear choices matter: heavy
plate stops bullets but not fire, and a ward stops fire but not bullets.

## Movement and height

Tile height is measured in levels; one level is about half a person's height, like a tall step.

| | Default | Notes |
| --- | --- | --- |
| MOVE | 4 tiles | Classes and equipment change it |
| JUMP | 2 levels up | Units can drop down up to twice their JUMP |

| Class type | MOVE | JUMP |
| --- | --- | --- |
| Heavy (armored, front line) | 3 | 1 |
| Standard | 4 | 2 |
| Light (scouts, skirmishers) | 5 | 3 |

- Allies can be passed through but not stopped on; enemies and fallen bodies block.
- Equipment adjusts both: heavy armor can cost a point of MOVE, while boots or a grappling hook can add
  MOVE or JUMP.
- High ground lowers the defender's counter chance and has no effect on hit chance.
- Guns gain +1 range when the shooter stands 2 or more levels above the target.

## The faction resource

The resource follows the class line, not the unit's origin.

| Line | Name | Starts at | Refills | How it plays |
| --- | --- | --- | --- | --- |
| Mercenary | Cooldowns | n/a | n/a | No resource; each ability waits a number of turns |
| Acolyte (Ordained) | Focus | 0 | +20 each turn | Mages ramp up and are most dangerous late in a fight |
| Engineer (Commonwealth) | Charge | Full (100) | Not on its own | Strong opening bursts that taper off |
| Awakened and Vessel (Unbound) | Resonance | 50 | +6 each turn | Steady; can spend HP when short |

Every faction has a limited way to refill. *Proposal:* tech carries spare power cells, magic uses focus
tonics, and the Unbound take a rest action that skips their attack.

## Faction combat mechanics

| Faction | Mechanic | The tension |
| --- | --- | --- |
| Ordained | **Channeling** | Power takes time, and the caster must be protected |
| Commonwealth | **Deployables** | Shape the board while your Charge lasts |
| Unbound | **The Blood Price** | How much of yourself are you willing to spend? |

**Channeling (Ordained).** Channelable spells can be cast over two turns: the cost is paid now, and the
spell resolves at the start of the caster's next turn at ×1.5 power with its status guaranteed. Any hit
on the caster before then interrupts it, and the Focus is lost. Resolving uses that turn's action.
*Proposal:* Ordained shrines on some maps speed up Focus.

**Deployables (Commonwealth).** Engineers place devices that stay on the map: turrets, smoke screens,
barricades, gas vents. Enemies can destroy or Jam them. The turret fires at the nearest targetable
enemy within 3 tiles at the start of its owner's turn.

**The Blood Price (Unbound).** When Resonance is short, the rest is paid in HP (0.6 HP per missing
point), and the ability deals ×1.25 damage. Each payment adds a Strained stack; each stack adds +10%
damage. At 5 stacks the unit hits Burnout and can't use abilities (only basic attacks) for 2 turns,
then Strain resets. Strain drops by 1 on each turn without an HP payment.

## Hit chance and damage

**Hit chance** = HIT − (target SPD − 10) × 2, plus 10 from the flank or 20 from behind, kept between 0%
and 100%. Height has no effect. Exposed targets are always hit.

**Base damage** uses percent reduction, so defense never makes damage zero and 100 defense halves it:

- Weapon: ATK × power × 100 ÷ (100 + DEF)
- Elemental: ATN × power × 100 ÷ (100 + RES)

**Final damage** = base damage × modifiers: Strain (+10% per stack), Backstab (×1.3 from behind), Blood
Price (×1.25), channeling (×1.5), critical (×2), counter (×0.5, or ×0.75 when Braced).

**No random spread.** The attack preview shows the exact damage. Chance lives only in the hit, critical
and counter rolls.

**Critical hits:** LUK ÷ 2, +25 when attacking from Hidden, −10 while Exposed. A crit does double damage
and halves the defender's counter chance.

**Where each faction's power comes from**

| Faction | Damage scales with | Grows through |
| --- | --- | --- |
| Ordained (magic) | ATN, fully | The caster leveling up |
| Commonwealth (tech) | Mostly the device's power, plus a smaller bonus from ATK (guns, blades) or ATN (elemental devices) | Better gear from towns |
| Unbound | ATN or ATK depending on the ability, plus a bonus when paying with HP | The body, at a cost |

## Counters

A defender who survives an attack can counter if their weapon can reach the attacker. Counters always
land and deal 50% of the defender's basic attack damage (75% when Braced).

| Attack comes from | Counter chance |
| --- | --- |
| Front | 75% |
| Flank | 50% |
| Back | 25% |

- −10% per level the attacker stands above the defender.
- +25% when the defender is Braced.
- A critical hit halves the chance.
- **No counter** when the attacker is Hidden, the defender is Suppressed or Stunned (including stunned
  by this hit), or the attacker is outside the defender's weapon reach.
- **A miss uses the normal counter chance.** Gear and skills can exploit misses: Parry (Vanguard) always
  counters a missed attack.
- Items, equipment and skills can change both the chance and the damage, all the way to 0% or 100%.
- Counters never trigger counters.
- **Attack preview:** before you commit, it shows hit chance, damage, crit chance and damage, and the
  counter chance on a hit, on a crit and on a miss, with the counter damage.

## Vision and Hidden

- Each unit sees 2 tiles straight ahead, the front diagonals, and up to 2 tiles to each side, never
  behind.
- A Hidden unit can't be targeted directly (attacks, single-target abilities, turrets) unless it is
  **spotted**: inside an enemy's vision, whether the Hidden unit moved in or the enemy turned to face
  it. A spotted unit keeps Hidden's advantages.
- Area abilities always hit Hidden units, and a hit reveals them.
- Attacking ends Hidden.

## Statuses

| Status | Effect | Source | Default duration |
| --- | --- | --- | --- |
| Hidden | See Vision and Hidden; attacks from Hidden can't be countered and get +25 crit | Skills, smoke devices, Unbound powers | Until the unit attacks, up to 3 turns |
| Exposed | Attacks against the unit always hit; lower critical chance | Skills, abilities | 2 turns |
| Drained | Resource can't build or refill | The Current | 2 turns |
| Overcharged | Next ability is stronger, then the unit becomes Drained | The Current | Until the next ability, up to 3 turns |
| Strained | Builds each time an Unbound pays with HP; each stack adds damage; at the limit, becomes Burnout | Unbound | Stacks up to 5; loses 1 each turn without paying HP |
| Burnout | Can't use abilities | Unbound | 2 turns, then Strained resets to 0 |
| Jammed | A device can't fire | Against tech | 2 turns |
| Choking | Lower HIT and SPD from smog or toxic fumes | Tech | 3 turns, or while standing in smog |
| Warded | Blocks the next elemental hit or counter | Magic | Until triggered, up to 3 turns |
| Cursed | Lower counter chance and counter damage | Magic | 3 turns |
| Burning | 7 damage at the start of each turn | Fire, any faction | 3 turns |
| Shocked | Wait timer loses 35, so the next turn comes later | Lightning, any faction | Instant |
| Frozen | Can't move (act only) | Frost, any faction | 1 turn |
| Braced | Higher counter chance and damage; ends if the unit moves | Stance | Until the unit's next turn |
| Suppressed | Can't counter | Suppressing Fire | Until the unit's next turn |
| Challenged | Must attack the challenger if it can | Challenge | 1 turn |
| Stunned | Loses its next turn and can't counter | General | 1 turn |
| Bleeding | 5 damage at the start of each turn | General | 3 turns |
| Rallied | Small boost to HIT and counter chance | Leaders, the main character | 2 turns |

Durations count the affected unit's own turns. Applying a status again refreshes its duration instead
of stacking, except Strained.

### Status rules

- **Cures:** most statuses can be removed by cure items sold in towns or by support skills that clear
  one or two statuses. Strained and Burnout can't be cured; only rest helps.
- **Bosses:** immune to Stunned, with shorter durations for the other control effects (Frozen, Shocked).
- **No chained control:** a unit that just recovered from Stunned is immune to it for 2 of its turns.
- **Combinations:** Frozen and Burning cancel each other out, and Shocked hitting a Jammed device
  overloads it for bonus damage. More to come with abilities.

## Classes

### Structure

- **Three tiers:** starter (generalist) → advanced (specialization) → expert.
- **25 classes:** 5 neutral (Mercenary line), 10 magic and tech (Acolyte and Engineer lines), 10 Unbound
  (Awakened and Vessel lines), plus unique classes for the main character and story comrades, to be
  designed later.
- **Vertical slice:** the 5 starters and 6 advanced classes. The rest slot on top later.

### Who can take what

| Unit | Can take | Classes available |
| --- | --- | --- |
| Ordained or Commonwealth | Mercenary, Acolyte and Engineer lines | 15 |
| Unbound | Mercenary and both Unbound lines | 15 |

- Magic and tech units can cross into each other's line after mastering their own starter. Both are
  tools for reaching the Current, so switching is a matter of training, not blood.
- The Acolyte line still needs a magic aptitude trait.
- Becoming Unbound only happens through story awakenings, never through a class change.

### The classes (names are placeholders)

| Starter | Advanced | Expert | Role |
| --- | --- | --- | --- |
| Mercenary | Vanguard | Bulwark | Front line; Braced; Parry and counters |
| Mercenary | Skirmisher | Shade | Flanking; Hidden; back attacks |
| Engineer | Gunner | Siegebreaker | Long range beyond counter reach; heavy act-only guns |
| Engineer | Saboteur | Smogwright | Jammed, Choking; items and support |
| Acolyte | Elementalist | Invoker | Elemental damage; Burning, Shocked, Frozen |
| Acolyte | Warder | Hierophant | Wards and healing; blessings and curses |
| Awakened (outward: the Current as raw force, ATN) | Bloodcaller | Bloodsworn | Heavy damage paid with HP; Strained |
| Awakened | Stormborn | Tempest | Raw elemental power without rituals or devices; area control |
| Vessel (inward: the Current in the body, ATK and SPD) | Veilwalker | Phantom | Hidden, reflexes, counters |
| Vessel | Ironblood | Juggernaut | Front-line bruiser; heals itself by spending Resonance |

### Rosters by path

| Path | Roster | Pressure |
| --- | --- | --- |
| Ordained | Many mages, some tech, Unbound almost never | Rigid; outsiders are treated badly |
| Commonwealth | Many engineers and mercenaries | Strong early, but gear-dependent and Charge runs out |
| Unbound | A small core of Unbound, plus outcasts the Menders shelter | Few recruits, hostile towns, every death is costly |

Battles deploy the same number of units on every path; the Unbound path has a smaller recruit pool.

### Growth

- **Unit level** raises the unit's own stats, whatever its class. Each unit has its own growth rates,
  shown as letter grades (A to E) when recruiting.
- **Class modifiers** shift stats while the unit is in that class. Level-ups don't depend on the class.
- **Class mastery** runs from 1 to 5 and is earned by fighting in that class. It teaches the class's
  abilities and counts toward unlocking new classes.

### Abilities and borrowing

- Advanced and expert classes keep every ability from the earlier classes in their line.
- **Borrowed slots:** abilities from other classes the unit has mastered.

| Tier | Borrowed slots | Stats |
| --- | --- | --- |
| Starter | 2 actions + 1 passive | Lower, general |
| Advanced and expert | 1 action + 1 passive | Higher, specialized |

- At most **one expert ability** among a unit's borrowed slots.
- Borrowed abilities use the borrower's stats and pay with the borrower's resource (a Vanguard borrowing
  Mend gets it on a cooldown; a Gunner borrowing Guard pays Charge).
- Late in the game, a veteran who has mastered several lines can return to a starter class as a flexible
  utility unit, like the Freelancer in Final Fantasy V.

### Unlock conditions

Like the original Tactics Ogre, a unit's own history decides what it can become. Each class needs:

- **Mastery:** advanced classes need starter mastery 3; experts need mastery 5 in their advanced class.
- **A stat minimum** in the class's key stat (values come with the numbers pass).
- **A deed:** something the unit has done in battle, tracked per unit and always visible, with the
  current count.

| Class | Tier | Key stat | Deed |
| --- | --- | --- | --- |
| Vanguard | Advanced | DEF | Land a set number of counters |
| Skirmisher | Advanced | SPD | Land a set number of back or flank attacks |
| Gunner | Advanced | HIT | Defeat enemies from beyond their counter reach |
| Saboteur | Advanced | HIT | Inflict a set number of statuses |
| Elementalist | Advanced | ATN | Deal a set amount of elemental damage |
| Warder | Advanced | RES | Block a set amount of damage with Wards |
| Bloodcaller | Advanced | ATN | Pay HP for abilities a set number of times |
| Stormborn | Advanced | ATN | Hit several enemies with one ability a set number of times |
| Veilwalker | Advanced | SPD | Attack from Hidden a set number of times |
| Ironblood | Advanced | HP | Survive battles after dropping below a quarter HP |
| Bulwark | Expert | DEF | Protect allies: take hits and counter while Braced, across many battles |
| Shade | Expert | SPD | Defeat enemies with attacks from Hidden |
| Siegebreaker | Expert | HIT | Use act-only heavy weapons a set number of times |
| Smogwright | Expert | HIT | Keep enemies Jammed or Choking for many turns in total |
| Invoker | Expert | ATN | Use Overcharged abilities a set number of times |
| Hierophant | Expert | RES | Heal and ward many times, with few kills. The highest Ordained clergy keep their hands clean: they bless the killing and leave it to others. It's about status, not mercy. |
| Bloodsworn | Expert | ATN | Recover from Burnout several times and keep fighting |
| Tempest | Expert | ATN | Inflict elemental statuses on many enemies |
| Phantom | Expert | SPD | Land counters and Hidden strikes without being hit, across many battles |
| Juggernaut | Expert | HP | Take a large amount of total damage without falling |

No class needs a specific wing. Restrictive deeds, like the Hierophant's few kills, are shown up front
with the current count.

### Training and switching

All training and switching happens in towns (or Unbound safehouses), only between battles. If nobody in
your party has a class yet, gaining it costs something; once anyone in the party has it, any eligible
unit can switch to it for free.

| Line | How a new class is gained |
| --- | --- |
| Mercenary | **Hired in.** Pay a fee at a mercenary guild. |
| Commonwealth (tech) | **Bought.** Each class comes from a blueprint bought at a workshop. On rival paths, from the Common Hand or the black market. |
| Ordained (magic) | **Taught.** A party member with that class at high enough mastery teaches it for free; otherwise a sanctum charges for ordination. On rival paths, the Penitents teach in secret. |
| Unbound | **Become.** No payment and no teacher, but a unit that switches into a different Unbound class sits out the next battle. |

Your faction's towns offer your line and Mercenary at a fair price; neutral towns offer Mercenary; rival
towns offer their line only through a secret reform-wing contact; hostile towns offer nothing.

**What a switch keeps:** level, stats, growth rates, mastery in every class and deed progress. The new
class's modifiers apply, borrowed slots adjust to the new tier, and gear the class can't use goes back
to the inventory.

## Abilities

### Framework

| Tier | Abilities | When learned |
| --- | --- | --- |
| Starter | 3 (2 actions + 1 passive) | Mastery 1, 3, 5 |
| Advanced | 4 (3 actions + 1 passive) | Mastery 1 to 4 |
| Expert | 3 (2 signature actions + 1 strong passive) | Mastery 1, 3, 5 |

About 85 abilities in total; the slice needs about 35.

| Element | Status | Who uses it |
| --- | --- | --- |
| Fire | Burning | Any faction |
| Lightning | Shocked | Any faction |
| Frost | Frozen | Any faction |
| Raw Current | None, but no common resistance either | Unbound only |

Every ability lists range, area, cost, element and tags: **act-only**, **channel**, **deployable**, and
**no counter** (rare and expensive). Damaging abilities trigger counters normally if the defender can
reach the attacker.

### Starter kits (proposal)

| Starter | Action | Action | Passive |
| --- | --- | --- | --- |
| Mercenary | Guard: take a Braced stance | Dash: extra movement this turn | Hardy: small boost to max HP |
| Engineer | Patch Up: small heal or repair | Snapshot: a quick shot that brings the next turn sooner | Efficient: abilities cost less Charge |
| Acolyte | Mend: small heal | Spark: small elemental bolt | Attuned: Focus builds faster |
| Awakened | Surge: pay HP for a stronger next attack | Fade: become Hidden briefly | Old Blood: Resonance recovers a little faster |
| Vessel | Endure: shrug off the next hit by spending Resonance | Lunge: a fast strike that closes distance | Quickened: small boost to SPD |

### Mercenary line (cooldowns)

**Vanguard: holds the line**

| Ability | Type | Effect | Cooldown |
| --- | --- | --- | --- |
| Shield Bash | Action | Damage plus a chance to Stun | 4 turns |
| Challenge | Action | The target must attack the Vanguard on its next turn if it can, walking into the counter | 3 turns |
| Shield Wall | Action | Braced, and adjacent allies take less damage until the Vanguard's next turn | 3 turns |
| Parry | Passive | Always counters a missed attack | — |

**Skirmisher: gets behind enemies**

| Ability | Type | Effect | Cooldown |
| --- | --- | --- | --- |
| Smoke Step | Action | Becomes Hidden | 3 turns |
| Hamstring | Action | Damage plus Bleeding; the target loses 1 MOVE | 3 turns |
| Tumble | Action | After attacking, move up to 2 more tiles | 2 turns |
| Backstab | Passive | Back attacks deal bonus damage | — |

**Bulwark (expert): an immovable wall**

| Ability | Type | Effect | Cooldown |
| --- | --- | --- | --- |
| Bastion | Action | Adjacent allies also become Braced | 4 turns |
| Unbreakable | Action | Immune to Stunned and Frozen, and can't drop below 1 HP until its next turn | 6 turns |
| Interpose | Passive | Once per round, takes an attack aimed at an adjacent ally | — |

**Shade (expert): the assassin**

| Ability | Type | Effect | Cooldown |
| --- | --- | --- | --- |
| Vanish | Action | Becomes Hidden even while next to enemies | 4 turns |
| Assassinate | Action | From Hidden only: a guaranteed critical hit | 5 turns |
| Shadow's Return | Passive | Regains Hidden after defeating an enemy (may need a limit) | — |

### Engineer line (Charge)

Costs are Low, Medium or High until the numbers pass.

**Gunner: fights from beyond counter reach**

| Ability | Type | Effect | Charge |
| --- | --- | --- | --- |
| Long Shot | Action, act-only | A shot with +2 range | Low |
| Suppressing Fire | Action | Light damage in a small area; targets can't counter until their next turn | Medium |
| Deploy Turret | Deployable | A turret that fires at the nearest enemy in range at the start of each of the Gunner's turns | High |
| Steady Aim | Passive | +HIT on turns the Gunner doesn't move | — |

**Saboteur: disrupts and denies ground**

| Ability | Type | Effect | Charge |
| --- | --- | --- | --- |
| Jam | Action | The target's devices are Jammed | Low |
| Gas Canister | Deployable | A smog cloud; anyone inside is Choking | Medium |
| Smoke Screen | Deployable | A smoke cloud; allies inside are Hidden | Medium |
| Quick Hands | Passive | Using an item doesn't count as the action, once per turn | — |

**Siegebreaker (expert): heavy artillery**

| Ability | Type | Effect | Charge |
| --- | --- | --- | --- |
| Mortar | Action, act-only, fire | Long-range area blast that leaves targets Burning | High |
| Breaching Charge | Action | Heavy damage, destroys barricades, and leaves the target Exposed | High |
| Heavy Ordnance | Passive | Act-only abilities cost less Charge | — |

**Smogwright (expert): pollution as a weapon**

| Ability | Type | Effect | Charge |
| --- | --- | --- | --- |
| Choke Field | Deployable | A large smog area: Choking, and spellcasters inside are Drained | High |
| Short Circuit | Action, lightning | Lightning damage plus Shocked; against a Jammed target it triggers the overload combo | Medium |
| Iron Lungs | Passive | Immune to Choking; the Smogwright's smog lasts 1 turn longer | — |

### Acolyte line (Focus)

Damaging and healing spells can be channeled for a stronger version.

**Elementalist: elemental damage and control**

| Ability | Type | Effect | Focus | Channeled |
| --- | --- | --- | --- | --- |
| Flame Burst | Action, fire | Small area damage, Burning | Medium | Bigger area |
| Arc Lightning | Action, lightning | Hits the target and jumps to 1 more nearby enemy, Shocked | Medium | Jumps to 2 more |
| Frost Lance | Action, frost | Long-range single target, Frozen | Medium | Frozen lasts 2 turns |
| Deep Channel | Passive | Channeled spells always apply their status | — | — |

**Warder: protection and healing**

| Ability | Type | Effect | Focus | Channeled |
| --- | --- | --- | --- | --- |
| Ward | Action | An ally becomes Warded | Low | — |
| Heal | Action | Medium heal | Medium | Heals everyone in a small area |
| Sanctify | Action | Blesses an area for 3 turns: allies inside get +RES and build Focus faster | High | — |
| Unshaken Rite | Passive | The first hit doesn't interrupt the Warder's channeling | — | — |

**Invoker (expert): overwhelming power**

| Ability | Type | Effect | Focus |
| --- | --- | --- | --- |
| Cataclysm | Action, channel only | A huge area blast in the element of the Invoker's choice | High |
| Overcharge | Action | The Invoker or an ally becomes Overcharged | Medium |
| Unbroken Will | Passive | Immune to Drained | — |

**Hierophant (expert): the high clergy**

| Ability | Type | Effect | Focus |
| --- | --- | --- | --- |
| Restore Life | Action, channel only | Revives a fallen ally still within their countdown | High |
| Anathema | Action | The target is Cursed and Drained | Medium |
| Authority | Passive | Adjacent allies are Rallied at the start of the Hierophant's turn | — |

Restore Life is the main way to save a fallen unit, so it's deliberately slow and risky. *Proposal:*
revive items also exist, but are rare and expensive.

### Unbound lines (Resonance, then the Blood Price)

**Bloodcaller (Awakened): damage paid in blood**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Blood Lance | Action, raw Current | Long-range piercing damage | Medium |
| Hemorrhage | Action | Damage plus Bleeding; heals for half the damage | Medium |
| Sacrifice | Action | Pay 15 HP for 30 Resonance (adds Strained) | — |
| Blood Frenzy | Passive | Bigger damage bonus when paying with HP | — |

**Stormborn (Awakened): raw elements with no tools**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Storm Surge | Action, lightning | Hits every adjacent enemy, Shocked | Medium |
| Wildfire | Action, fire | A line of fire, Burning | Medium |
| Whiteout | Action, frost | Area frost, Frozen | High |
| Living Conduit | Passive | Never needs to channel and is never act-only | — |

**Bloodsworn (expert, Awakened): all-in**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Blood Oath | Action | For 3 turns, every ability is paid in HP with double the bonus | — |
| Exsanguinate | Action, raw Current | Huge damage that grows with each Strained stack | High |
| Last Rite | Passive | On entering Burnout, releases a raw Current burst on adjacent enemies | — |

**Tempest (expert, Awakened): a walking storm**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Maelstrom | Action | A large area blast in the element of choice | High |
| Eye of the Storm | Action | For 3 turns, enemies who start their turn adjacent take lightning damage and are Shocked | Medium |
| Elemental Body | Passive | Immune to Burning, Shocked and Frozen | — |

**Veilwalker (Vessel): reflexes and shadow**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Ghost Step | Action | Short teleport, then Hidden | Medium |
| Mirror Feint | Action | The next attack against the Veilwalker misses, triggering its counter | Low |
| Reflex Strike | Action | The next counter is guaranteed and deals full damage | Low |
| Heightened Senses | Passive | +25% counter chance from every direction | — |

**Ironblood (Vessel): the bruiser**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Iron Skin | Action | +DEF and +RES for 2 turns | Medium |
| Mend Flesh | Action | Heals self | Medium |
| Rampage | Action | Attacks every adjacent enemy | Medium |
| Unyielding | Passive | Below a quarter HP, takes less damage | — |

**Phantom (expert, Vessel): never where you strike**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Phase | Action | Can't be targeted until its next turn | High |
| Thousand Cuts | Action | Strikes each adjacent enemy from behind | High |
| Unseen | Passive | Starts every battle Hidden | — |

**Juggernaut (expert, Vessel): an unstoppable body**

| Ability | Type | Effect | Resonance |
| --- | --- | --- | --- |
| Unstoppable Charge | Action | Charges in a line through enemies, damaging and knocking back each one | High |
| Blood Fortress | Action | Until its next turn, part of all damage taken becomes Resonance | Medium |
| Regeneration | Passive | Heals a little at the start of each turn | — |

*Watch in testing:* Phantom's Unseen and the Bloodsworn's Blood Oath may be too strong.

## Still to design

- [ ] Unique classes for the main character and story comrades
- [ ] Exact numbers: stat ranges, stat minimums for unlocks, deed counts, ability costs
- [ ] AI that weighs tile danger (fragile casters currently walk into the open)
- [ ] Hit chance tuning: current stats give 93 to 100%
