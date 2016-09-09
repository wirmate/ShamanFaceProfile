using System;
using System.Collections.Generic;
using System.Linq;
using SmartBot.Database;
using SmartBot.Plugins.API;

/* Explanation on profiles :
 * 
 * All the values defined in profiles are percentage modifiers, it means that it will affect base profile's default values.
 * 
 * Modifiers values can be set within the range (-1000 - 1000)  (negative modifier has the opposite effect)
 * You can specify targets for the non-global modifiers, these target specific modifers will be added on top of global modifier + modifier for the card (without target)
 * 
 * parameters.GlobalSpellsModifier ---> Modifier applied to all spells no matter what they are. The higher is the modifier, the less likely the AI will be to play the spell
 * parameters.GlobalMinionsModifier ---> Modifier applied to all minions no matter what they are. The higher is the modifier, the less likely the AI will be to play the minion
 * 
 * parameters.GlobalAggroModifier ---> Modifier applied to enemy's health value, the higher it is, the more aggressive will be the AI
 * parameters.GlobalDefenseModifier ---> Modifier applied to friendly's health value, the higher it is, the more hp conservative will be the AI
 * 
 * parameters.SpellsModifiers ---> You can set individual modifiers to each spells there, those are ADDED to the GLOBAL modifiers !!
 * parameters.MinionsModifiers ---> You can set individual modifiers to each minions there, those are ADDED to the GLOBAL modifiers !!
 * 
 * parameters.GlobalDrawModifier ---> Modifier applied to card draw value
 * parameters.GlobalWeaponsModifier ---> Modifier applied to the value of weapons attacks
 * 
 */

namespace SmartBotProfiles
{
    [Serializable]
    public class ShamanFace : Profile
    {
        private const bool Debug = false;
        //Cards definitions
        private const Card.Cards TheCoin = Card.Cards.GAME_005;

        private const Card.Cards SteadyShot = Card.Cards.DS1h_292;
        private const Card.Cards Shapeshift = Card.Cards.CS2_017;
        private const Card.Cards LifeTap = Card.Cards.CS2_056;
        private const Card.Cards Fireblast = Card.Cards.CS2_034;
        private const Card.Cards Reinforce = Card.Cards.CS2_101;
        private const Card.Cards ArmorUp = Card.Cards.CS2_102;
        private const Card.Cards LesserHeal = Card.Cards.CS1h_001;
        private const Card.Cards DaggerMastery = Card.Cards.CS2_083b;

        private static readonly Dictionary<Card.Cards, int> _heroPowersPriorityTable = new Dictionary<Card.Cards, int>
        {
            {SteadyShot, 8},
            {Shapeshift, 6},
            {LifeTap, 7},
            {Fireblast, 5},
            {Reinforce, 4},
            {ArmorUp, 3},
            {LesserHeal, 2},
            {DaggerMastery, 1}
        };

        private static readonly Dictionary<Card.Cards, int> _minionsOverloadTable = new Dictionary<Card.Cards, int>
        {
            {Cards.TotemGolem, CardTemplate.TemplateList[Cards.TotemGolem].Overload}
        };

        private static readonly Dictionary<Card.Cards, int> _spellDamagesTable = new Dictionary<Card.Cards, int>
        {
            {Cards.LightningBolt, 3},
            {Cards.Crackle, 4},
            {Cards.LavaBurst, 5},
            {Cards.LavaShock, 2}
        };

        private static readonly Dictionary<Card.Cards, int> _spellsOverloadModifierTable = new Dictionary
            <Card.Cards, int>
        {
            {Cards.LightningBolt, 300},
            {Cards.Crackle, 400},
            {Cards.LavaBurst, 500},
            {Cards.Doomhammer, 400},
            {Cards.ElementalDestruction, 400},
            {Cards.AncestralKnowledge, 400},
            {Cards.FeralSpirit, 400}
        };

        private static readonly Dictionary<Card.Cards, int> _spellsOverloadTable = new Dictionary<Card.Cards, int>
        {
            {Cards.LightningBolt, CardTemplate.TemplateList[Cards.LightningBolt].Overload},
            {Cards.Crackle, CardTemplate.TemplateList[Cards.Crackle].Overload},
            {Cards.LavaBurst, CardTemplate.TemplateList[Cards.LavaBurst].Overload},
            {Cards.Doomhammer, CardTemplate.TemplateList[Cards.Doomhammer].Overload},
            {Cards.ElementalDestruction, CardTemplate.TemplateList[Cards.ElementalDestruction].Overload},
            {Cards.AncestralKnowledge, CardTemplate.TemplateList[Cards.AncestralKnowledge].Overload},
            {Cards.FeralSpirit, CardTemplate.TemplateList[Cards.FeralSpirit].Overload}
        };


        private static readonly List<Card.Cards> _tauntMinionsTable =
            CardTemplate.TemplateList.ToList().FindAll(x => x.Value.Taunt).ToDictionary(x => x.Key).Keys.ToList();

        private static readonly Dictionary<KeyValuePair<Card.Cards, Card.Cards>, int> _threatsModifiersTable = new Dictionary
            <KeyValuePair<Card.Cards, Card.Cards>, int>
        {
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.Crackle, Cards.TunnelTrogg), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.LightningBolt, Cards.TunnelTrogg), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.Crackle, Cards.VitalityTotem), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.LightningBolt, Cards.VitalityTotem), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.EarthShock, Cards.LeperGnome), 30}
        };

        public ProfileParameters GetParameters(Board board)
        {
            //Init profile parameter based on rush profile
            var parameters = new ProfileParameters(BaseProfile.Rush);

            //300% of default "Rush" profile value -> the bot will be more aggressive
            if (board.TurnCount <= 4 || board.HeroEnemy.CurrentHealth > 18)
            {
                parameters.GlobalAggroModifier.Value = 150;

                foreach (var card in board.MinionFriend)
                {
                    parameters.BoardFriendlyMinionsModifiers.AddOrUpdate(card.Template.Id, new Modifier(200));
                }
            }
            else
                parameters.GlobalAggroModifier.Value = 300;

            HandleSpells(ref parameters, board);
            HandleMinions(ref parameters, board);

            //Lower earthshock modifier on Sludge belcher
            //parameters.SpellsModifiers.AddOrUpdate(EarthShock, new Modifier(20, SludgeBelcher));
            OverrideSilenceSpellsValueOnTauntMinions(ref parameters); //Lower silences values on taunts


            Log("Potential Damages in hand : " + BoardHelper.GetTotalBlastDamagesInHand(board));
            Log("Potential Weapon: " + BoardHelper.GetPotentialWeaponDamages(board));
            Log("Potential minions: " + board.MinionFriend.Sum(x => x.CurrentAtk));
            Log("Lethal next turn : " + BoardHelper.HasPotentialLethalNextTurn(board));
            Log("Lethal next turn without spells : " + BoardHelper.HasPotentialLethalNextTurnWithoutSpells(board));
            Log("Next turn attackers from hand : " +
                string.Join(" - ",
                    BoardHelper.GetPlayableMinionSequenceAttacker(BoardHelper.GetPlayableMinionSequence(board),
                        board)));
            Log("Next turn attackers from board : " +
                string.Join(" - ", BoardHelper.GetPotentialMinionAttacker(board)));

            //If we cant put down enemy's life at topdeck lethal range
            if (!BoardHelper.HasPotentialLethalNextTurn(board)
                ||
                (BoardHelper.HasPotentialLethalNextTurn(board) &&
                 BoardHelper.HasPotentialLethalNextTurnWithoutSpells(board)))
            {
                if (BoardHelper.HasPotentialLethalNextTurnWithoutSpells(board))
                    parameters.GlobalAggroModifier.Value = 100;

                //Set crackle spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand before turn 6
                parameters.SpellsModifiers.AddOrUpdate(Cards.Crackle,
                    new Modifier(GetOverloadSpellConservativeModifier(board, Cards.Crackle)));

                parameters.SpellsModifiers.AddOrUpdate(Cards.Crackle,
                    new Modifier(GetOverloadSpellConservativeModifier(board, Cards.Crackle), board.HeroEnemy.Id));

                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.SpellsModifiers.AddOrUpdate(Cards.LavaBurst,
                    new Modifier(GetOverloadSpellConservativeModifier(board, Cards.LavaBurst)));
                parameters.SpellsModifiers.AddOrUpdate(Cards.LavaBurst,
                    new Modifier(GetOverloadSpellConservativeModifier(board, Cards.LavaBurst), board.HeroEnemy.Id));
                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.MinionsModifiers.AddOrUpdate(Cards.ArcaneGolem, new Modifier(400));
            }

            if (BoardHelper.HasPotentialLethalNextTurn(board) &&
                !BoardHelper.HasPotentialLethalNextTurnWithoutSpells(board))
            {
                PreventSpellFromBeingPlayedOnMinions(ref parameters, board);
                parameters.GlobalAggroModifier.Value = 400;
            }

            if (BoardHelper.HasDoomhammerOnBoard(board) ||
                (board.TurnCount <= 5 && !board.HasCardInHand(Cards.LightningBolt)))
                //If we don't have doomhammer this turn
            {
                //Set rockbiter spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand until we get doomhammer
                parameters.SpellsModifiers.AddOrUpdate(Cards.RockbiterWeapon,
                    new Modifier(board.TurnCount > 4 ? 200 : 100));
            }
            else
            {
                parameters.SpellsModifiers.AddOrUpdate(Cards.RockbiterWeapon, new Modifier(400));
            }

            if (BoardHelper.ShouldDrawCards(board)) //If we need to draw cards
            {
                //Set AncestralKnowledge spell modifier to 0% of the base spell value defined in "Rush" profile, the bot will play the spell more easily
                parameters.SpellsModifiers.AddOrUpdate(Cards.AncestralKnowledge, new Modifier(0));
                parameters.GlobalDrawModifier = new Modifier(150);
            }
            else
            {
                parameters.GlobalDrawModifier = new Modifier(50);
            }

            if (board.TurnCount < 5)
            {
                //Turn specific handlers
                switch (board.ManaAvailable)
                {
                    case 1:
                        HandleTurnOneSpecifics(board, ref parameters);
                        break;

                    case 2:
                        HandleTurnTwoSpecifics(board, ref parameters);
                        break;
                }
            }

            //If we can play doomhammer next turn we don't want to overload
            /*  if (!BoardHelper.HasDoomhammerOnBoard(board) && BoardHelper.CanPlayDoomhammerNextTurn(board))
            {
                if (board.MinionFriend.Count > 0)
                    OverrideOverloadMinionsModifiers(ref parameters);

                OverrideOverloadSpellsModifiers(ref parameters);
            }*/

            //Reduce spells values over threatening minions
            OverrideSpellsValuesOnThreats(ref parameters);

            Log("AggroMod : " + parameters.GlobalAggroModifier.Value);

            return parameters;
        }

        public Card.Cards SirFinleyChoice(List<Card.Cards> choices)
        {
            var filteredTable = _heroPowersPriorityTable.Where(x => choices.Contains(x.Key)).ToList();
            return filteredTable.First(x => x.Value == filteredTable.Max(y => y.Value)).Key;
        }

        private static void Log(string str)
        {
            if (Debug)
                Bot.Log(str);
        }

        private void PreventSpellFromBeingPlayedOnMinions(ref ProfileParameters parameters, Board board)
        {
            foreach (var card in board.MinionEnemy.FindAll(x => !x.IsTaunt))
            {
                parameters.SpellsModifiers.AddOrUpdate(Cards.LightningBolt, new Modifier(500, card.Id));
                parameters.SpellsModifiers.AddOrUpdate(Cards.LavaBurst, new Modifier(500, card.Id));
            }
        }

        public void HandleSpells(ref ProfileParameters parameters, Board board)
        {
            //Set FeralSpirit spell modifier to 20% of the base spell value defined in "Rush" profile, the AI has more chances to play this spell
            parameters.SpellsModifiers.AddOrUpdate(Cards.FeralSpirit, new Modifier(100));

            //Set lava shock spell modifier to 200% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand without any overloaded mana
            parameters.SpellsModifiers.AddOrUpdate(Cards.LavaShock, new Modifier(200));

            parameters.SpellsModifiers.AddOrUpdate(Cards.EarthShock, new Modifier(250));

            parameters.SpellsModifiers.AddOrUpdate(Cards.LightningBolt, new Modifier(300, board.HeroEnemy.Id));

            //Lower TheCoin modifier
            parameters.SpellsModifiers.AddOrUpdate(TheCoin, new Modifier(50));
        }

        public void HandleMinions(ref ProfileParameters parameters, Board board)
        {
            //Set KnifeJuggler modifier to 0% of the base value defined in "Rush" profile, the AI has more chances to play it
            parameters.MinionsModifiers.AddOrUpdate(Cards.KnifeJuggler, new Modifier(0));

            parameters.MinionsModifiers.AddOrUpdate(Cards.FlametongueTotem, new Modifier(250));
            foreach (var card in board.MinionFriend.FindAll(x => x.CanAttack))
            {
                if (
                    board.MinionEnemy.Any(
                        x =>
                            x.CurrentHealth <= card.CurrentAtk + 2 && x.CurrentHealth > card.CurrentAtk &&
                            !x.IsDivineShield))
                {
                    parameters.MinionsModifiers.AddOrUpdate(Cards.AbusiveSergeant, new Modifier(100));
                }
            }

            //Use abusive to kill minions
            foreach (var card in board.MinionFriend.FindAll(x => x.CanAttack))
            {
                if (
                    board.MinionEnemy.Any(
                        x =>
                            x.CurrentHealth <= card.CurrentAtk + 2 && x.CurrentHealth > card.CurrentAtk &&
                            !x.IsDivineShield))
                {
                    parameters.MinionsModifiers.AddOrUpdate(Cards.AbusiveSergeant, new Modifier(0));

                    if (board.ManaAvailable == 0)
                        parameters.SpellsModifiers.AddOrUpdate(TheCoin, new Modifier(0));
                }
            }

            if (!BoardHelper.HasEnemyTauntOnBoard(board))
            {
                //Set silence to 150% of its base value to try to keep it in hand if there's no enemy taunt on board
                parameters.MinionsModifiers.AddOrUpdate(Cards.IronbeakOwl, new Modifier(150));
            }
            else
            {
                //Set silence to 60% of its base value to make it easier to play if theres a taunt on board
                parameters.MinionsModifiers.AddOrUpdate(Cards.IronbeakOwl, new Modifier(60));
            }
        }

        private void HandleTurnOneSpecifics(Board board, ref ProfileParameters parameters)
        {
            //Prefer sirfinley over trogg 
            if (board.HasCardInHand(Cards.TunnelTrogg) && board.HasCardInHand(Cards.SirFinleyMrrgglton))
            {
                parameters.MinionsModifiers.AddOrUpdate(Cards.SirFinleyMrrgglton, new Modifier(-500));
            }
            else
            {
                //Set TunnelTrogg modifier to -100% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
                parameters.MinionsModifiers.AddOrUpdate(Cards.TunnelTrogg, new Modifier(-100));
            }

            //Set LeperGnome modifier to -100% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
            parameters.MinionsModifiers.AddOrUpdate(Cards.LeperGnome, new Modifier(-100));
        }

        private void HandleTurnTwoSpecifics(Board board, ref ProfileParameters parameters)
        {
            //Set UnboundElemental modifier to -500% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
            parameters.MinionsModifiers.AddOrUpdate(Cards.UnboundElemental, new Modifier(-500));
        }

        private int GetOverloadSpellConservativeModifier(Board board, Card.Cards id)
        {
            return BoardHelper.HasCardOnBoard(Cards.TunnelTrogg, board) && board.MinionEnemy.Count == 0
                ? _spellsOverloadModifierTable[id]/2
                : _spellsOverloadModifierTable[id];
        }

        private void OverrideOverloadSpellsModifiers(ref ProfileParameters parameters)
        {
            foreach (var i in _spellsOverloadTable)
            {
                parameters.SpellsModifiers.AddOrUpdate(i.Key, new Modifier(600));
            }
        }

        private void OverrideOverloadMinionsModifiers(ref ProfileParameters parameters)
        {
            foreach (var i in _minionsOverloadTable)
            {
                parameters.MinionsModifiers.AddOrUpdate(i.Key, new Modifier(600));
            }
        }

        private void OverrideSilenceSpellsValueOnTauntMinions(ref ProfileParameters parameters)
        {
            foreach (var card in _tauntMinionsTable)
            {
                if (CardTemplate.LoadFromId(card).Cost >= 2)
                {
                    parameters.SpellsModifiers.AddOrUpdate(Cards.EarthShock, new Modifier(20, card));
                    parameters.MinionsModifiers.AddOrUpdate(Cards.IronbeakOwl, new Modifier(20, card));
                }
            }
        }

        private void OverrideSpellsValuesOnThreats(ref ProfileParameters parameters)
        {
            foreach (var card in _threatsModifiersTable)
            {
                parameters.SpellsModifiers.AddOrUpdate(card.Key.Key, new Modifier(card.Value, card.Key.Value));
            }
        }

        public static class BoardHelper
        {
            public static bool HasEnemyTauntOnBoard(Board board)
            {
                return board.MinionEnemy.Any(x => x.IsTaunt && !x.IsStealth);
            }

            public static bool HasCardOnBoard(Card.Cards card, Board board)
            {
                return board.MinionFriend.Any(x => x.Template.Id == card);
            }

            public static bool ShouldDrawCards(Board board)
            {
                if (board.Hand.Count(x => x.Type == Card.CType.MINION) < 2 && board.ManaAvailable > 2 &&
                    board.Ability.Template.Id == LifeTap)
                {
                    return true;
                }
                if (board.Hand.Any(x => x.Template.Id == Cards.AncestralKnowledge) &&
                    GetManaLeftAfterPlayingMinions(board) >= 2)
                {
                    return true;
                }
                return false;
            }

            public static bool ShouldPlayDoomhammer(Board board)
            {
                return !HasDoomhammerOnBoard(board) && HasDommhammerInHand(board) && CanPlayDoomhammer(board);
            }

            public static int GetManaLeftAfterPlayingMinions(Board board)
            {
                var ret = board.ManaAvailable -
                          board.Hand.FindAll(x => x.Template.Type == Card.CType.MINION).Sum(x => x.CurrentCost);

                return ret < 0 ? 0 : ret;
            }

            public static int GetEnemyHealthAndArmor(Board board)
            {
                return board.HeroEnemy.CurrentHealth + board.HeroEnemy.CurrentArmor;
            }

            public static int GetSpellPower(Board board)
            {
                return board.MinionFriend.FindAll(x => x.IsSilenced == false).Sum(x => x.SpellPower);
            }

            public static int GetPlayableSpellSequenceDamages(Board board, bool altogetherWithHammer = false)
            {
                return GetSpellSequenceDamages(GetPlayableSpellSequence(board, altogetherWithHammer), board);
            }

            public static int GetSecondTurnLethalRange(Board board)
            {
                return GetEnemyHealthAndArmor(board) - GetPotentialFaceDamages(board);
            }

            public static bool HasPotentialLethalNextTurn(Board board)
            {
                if (!board.MinionEnemy.Any(x => x.IsTaunt) &&
                    (GetEnemyHealthAndArmor(board) -
                     GetPotentialMinionDamages(board) -
                     GetPotentialWeaponDamages(board) - (WillHaveDoomhammerNextTurn(board) ? 4 : 0) -
                     GetPlayableMinionSequenceDamages(GetPlayableMinionSequence(board), board) <=
                     GetTotalBlastDamagesInHand(board)))
                    return true;
                return GetRemainingBlastDamagesAfterSequence(board) >= GetSecondTurnLethalRange(board);
            }

            public static bool HasPotentialLethalNextTurnWithoutSpells(Board board)
            {
                if (!board.MinionEnemy.Any(x => x.IsTaunt) &&
                    (GetEnemyHealthAndArmor(board) -
                     GetPotentialMinionDamages(board) -
                     GetPotentialWeaponDamages(board) - (WillHaveDoomhammerNextTurn(board) ? 4 : 0) -
                     GetPlayableMinionSequenceDamages(GetPlayableMinionSequence(board), board) <=
                     0))
                    return true;
                return false;
            }


            public static int GetSpellSequenceDamages(List<Card.Cards> sequence, Board board)
            {
                return
                    sequence.FindAll(x => _spellDamagesTable.ContainsKey(x))
                        .Sum(x => _spellDamagesTable[x] + GetSpellPower(board));
            }

            public static bool WillHaveDoomhammerNextTurn(Board board)
            {
                if (board.WeaponFriend == null) return false;
                return board.WeaponFriend.CurrentDurability - board.HeroFriend.CountAttack >= 2;
            }

            public static List<Card.Cards> GetPlayableSpellSequence(Board board, bool altogetherWithHammer = false)
            {
                var ret = new List<Card.Cards>();
                var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

                foreach (var card in board.Hand.OrderBy(x => x.CurrentCost))
                {
                    if (_spellDamagesTable.ContainsKey(card.Template.Id) == false) continue;
                    if (manaAvailable < card.CurrentCost) continue;

                    ret.Add(card.Template.Id);
                    manaAvailable -= card.CurrentCost;
                }

                return ret;
            }

            public static List<Card.Cards> GetPlayableMinionSequence(Board board, bool altogetherWithHammer = false)
            {
                var ret = new List<Card.Cards>();
                var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

                foreach (var card in board.Hand.OrderByDescending(x => x.CurrentCost))
                {
                    if (card.Type != Card.CType.MINION) continue;
                    if (manaAvailable < card.CurrentCost) continue;

                    ret.Add(card.Template.Id);
                    manaAvailable -= card.CurrentCost;
                }

                return ret;
            }

            public static int GetPlayableMinionSequenceDamages(List<Card.Cards> minions, Board board)
            {
                return GetPlayableMinionSequenceAttacker(minions, board).Sum(x => CardTemplate.LoadFromId(x).Atk);
            }

            public static List<Card.Cards> GetPlayableMinionSequenceAttacker(List<Card.Cards> minions, Board board)
            {
                var minionscopy = minions.ToArray().ToList();
                foreach (var mi in board.MinionEnemy.OrderByDescending(x => x.CurrentAtk))
                {
                    if (
                        minions.OrderByDescending(x => CardTemplate.LoadFromId(x).Atk)
                            .Any(x => CardTemplate.LoadFromId(x).Health <= mi.CurrentAtk))
                    {
                        var tar =
                            minions.OrderByDescending(x => CardTemplate.LoadFromId(x).Atk)
                                .FirstOrDefault(x => CardTemplate.LoadFromId(x).Health <= mi.CurrentAtk);
                        minionscopy.Remove(tar);
                    }
                }

                return minionscopy;
            }

            public static int GetPotentialMinionDamages(Board board)
            {
                return GetPotentialMinionAttacker(board).Sum(x => x.CurrentAtk);
            }

            public static List<Card> GetPotentialMinionAttacker(Board board)
            {
                var minionscopy = board.MinionFriend.ToArray().ToList();
                foreach (var mi in board.MinionEnemy.OrderByDescending(x => x.CurrentAtk))
                {
                    if (
                        board.MinionFriend.OrderByDescending(x => x.CurrentAtk)
                            .Any(x => x.CurrentHealth <= mi.CurrentAtk))
                    {
                        var tar =
                            board.MinionFriend.OrderByDescending(x => x.CurrentAtk)
                                .FirstOrDefault(x => x.CurrentHealth <= mi.CurrentAtk);
                        minionscopy.Remove(tar);
                    }
                }

                return minionscopy;
            }

            public static int GetPotentialFaceDamages(Board board)
            {
                return GetPotentialWeaponDamages(board) +
                       GetPlayableSpellSequenceDamages(board, ShouldPlayDoomhammer(board));
            }

            public static int GetRemainingBlastDamagesAfterSequence(Board board)
            {
                return GetTotalBlastDamagesInHand(board) -
                       GetPlayableSpellSequenceDamages(board, ShouldPlayDoomhammer(board));
            }

            public static int GetTotalBlastDamagesInHand(Board board)
            {
                return
                    board.Hand.FindAll(x => _spellDamagesTable.ContainsKey(x.Template.Id))
                        .Sum(x => _spellDamagesTable[x.Template.Id] + GetSpellPower(board));
            }

            public static int GetPotentialWeaponDamages(Board board)
            {
                if (HasDoomhammerOnBoard(board))
                {
                    return (2 + GetPlayableRockbiters(board)*3)*(2 - board.HeroFriend.CountAttack);
                }

                if (HasDommhammerInHand(board) && CanPlayDoomhammer(board))
                {
                    return (2 + GetPlayableRockbiters(board, true)*3)*(2 - board.HeroFriend.CountAttack);
                }

                return 0;
            }

            public static int GetPlayableRockbiters(Board board, bool altogetherWithHammer = false)
            {
                var handCount = board.Hand.Count(x => x.Template.Id == Cards.RockbiterWeapon);
                var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

                if (manaAvailable < handCount)
                {
                    handCount = manaAvailable;
                }

                return handCount < 0 ? 0 : handCount;
            }

            public static bool HasDoomhammerOnBoard(Board board)
            {
                return board.WeaponFriend != null && board.WeaponFriend.Template.Id == Cards.Doomhammer;
            }

            public static bool HasDommhammerInHand(Board board)
            {
                return board.Hand.Any(x => x.Template.Id == Cards.Doomhammer);
            }

            public static bool CanPlayDoomhammer(Board board)
            {
                return board.ManaAvailable >= 5;
            }

            public static bool CanPlayDoomhammerNextTurn(Board board)
            {
                return board.ManaAvailable - board.LockedMana + 1 >= 5 && HasDommhammerInHand(board);
            }
        }
    }
}