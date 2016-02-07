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

        private const int AggroModifier = 300;
        private const int OverloadSpellsConservativeModifier = 400;

        private readonly Dictionary<Card.Cards, int> _heroPowersPriorityTable = new Dictionary<Card.Cards, int>
        {
            {SteadyShot, 8},
            {Shapeshift, 7},
            {LifeTap, 6},
            {Fireblast, 5},
            {Reinforce, 4},
            {ArmorUp, 3},
            {LesserHeal, 2},
            {DaggerMastery, 1}
        };

        private readonly Dictionary<Card.Cards, int> _minionsOverloadTable = new Dictionary<Card.Cards, int>
        {
            {Cards.TotemGolem, CardTemplate.TemplateList[Cards.TotemGolem].Overload}
        };

        private readonly Dictionary<Card.Cards, int> _spellDamagesTable = new Dictionary<Card.Cards, int>
        {
            {Cards.EarthShock, 1},
            {Cards.LightningBolt, 3},
            {Cards.Crackle, 4},
            {Cards.LavaBurst, 5},
            {Cards.LavaShock, 2}
        };

        private readonly Dictionary<Card.Cards, int> _spellsOverloadTable = new Dictionary<Card.Cards, int>
        {
            {Cards.LightningBolt, CardTemplate.TemplateList[Cards.LightningBolt].Overload},
            {Cards.Crackle, CardTemplate.TemplateList[Cards.Crackle].Overload},
            {Cards.LavaBurst, CardTemplate.TemplateList[Cards.LavaBurst].Overload},
            {Cards.Doomhammer, CardTemplate.TemplateList[Cards.Doomhammer].Overload},
            {Cards.ElementalDestruction, CardTemplate.TemplateList[Cards.ElementalDestruction].Overload},
            {Cards.AncestralKnowledge, CardTemplate.TemplateList[Cards.AncestralKnowledge].Overload},
            {Cards.FeralSpirit, CardTemplate.TemplateList[Cards.FeralSpirit].Overload}
        };

        private readonly List<Card.Cards> _tauntMinionsTable =
            CardTemplate.TemplateList.ToList().FindAll(x => x.Value.Taunt).ToDictionary(x => x.Key).Keys.ToList();

        private readonly Dictionary<KeyValuePair<Card.Cards, Card.Cards>, int> _threatsModifiersTable = new Dictionary
            <KeyValuePair<Card.Cards, Card.Cards>, int>
        {
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.Crackle, Cards.TunnelTrogg), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.LightningBolt, Cards.TunnelTrogg), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.Crackle, Cards.VitalityTotem), 30},
            {new KeyValuePair<Card.Cards, Card.Cards>(Cards.LightningBolt, Cards.VitalityTotem), 30}
        };

        public ProfileParameters GetParameters(Board board)
        {
            //Init profile parameter based on rush profile
            var parameters = new ProfileParameters(BaseProfile.Rush);

            //300% of default "Rush" profile value -> the bot will be more aggressive
            parameters.GlobalAggroModifier.Value = AggroModifier;

            //Set FeralSpirit spell modifier to 20% of the base spell value defined in "Rush" profile, the AI has more chances to play this spell
            parameters.SpellsModifiers.AddOrUpdate(Cards.FeralSpirit, new Modifier(20));

            //Set lava shock spell modifier to 200% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand without any overloaded mana
            parameters.SpellsModifiers.AddOrUpdate(Cards.LavaShock, new Modifier(200));

            parameters.SpellsModifiers.AddOrUpdate(Cards.EarthShock, new Modifier(250));

            //Lower TheCoin modifier
            parameters.SpellsModifiers.AddOrUpdate(TheCoin, new Modifier(70));

            //Lower earthshock modifier on Sludge belcher
            //parameters.SpellsModifiers.AddOrUpdate(EarthShock, new Modifier(20, SludgeBelcher));
            OverrideSilenceSpellsValueOnTauntMinions(ref parameters); //Lower silences values on taunts

            //Set KnifeJuggler modifier to 30% of the base value defined in "Rush" profile, the AI has more chances to play it
            parameters.MinionsModifiers.AddOrUpdate(Cards.KnifeJuggler, new Modifier(0));

            //If we cant put down enemy's life at topdeck lethal range
            if (!HasPotentialLethalNextTurn(board))
            {
                //Set lightning bolt spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand before turn 6
                parameters.SpellsModifiers.AddOrUpdate(Cards.LightningBolt,
                    new Modifier(GetOverloadSpellConservativeModifier(board)));

                //Set crackle spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand before turn 6
                parameters.SpellsModifiers.AddOrUpdate(Cards.Crackle,
                    new Modifier(GetOverloadSpellConservativeModifier(board)));

                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.SpellsModifiers.AddOrUpdate(Cards.LavaBurst,
                    new Modifier(GetOverloadSpellConservativeModifier(board)));

                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.MinionsModifiers.AddOrUpdate(Cards.ArcaneGolem, new Modifier(400));
            }

            if (!HasEnemyTauntOnBoard(board))
            {
                //Set silence to 150% of its base value to try to keep it in hand if there's no enemy taunt on board
                parameters.MinionsModifiers.AddOrUpdate(Cards.IronbeakOwl, new Modifier(150));
            }
            else
            {
                //Set silence to 60% of its base value to make it easier to play if theres a taunt on board
                parameters.MinionsModifiers.AddOrUpdate(Cards.IronbeakOwl, new Modifier(60));
            }

            if (!HasDoomhammerOnBoard(board)) //If we don't have doomhammer this turn
            {
                //Set rockbiter spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand until we get doomhammer
                parameters.SpellsModifiers.AddOrUpdate(Cards.RockbiterWeapon, new Modifier(400));
            }

            if (ShouldDrawCards(board)) //If we need to draw cards
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
            if (!HasDoomhammerOnBoard(board) && CanPlayDoomhammerNextTurn(board))
            {
                if (board.MinionFriend.Count > 0)
                    OverrideOverloadMinionsModifiers(ref parameters);

                OverrideOverloadSpellsModifiers(ref parameters);
            }

            //Reduce spells values over threatening minions
            OverrideSpellsValuesOnThreats(ref parameters);

            return parameters;
        }

        public Card.Cards SirFinleyChoice(List<Card.Cards> choices)
        {
            var filteredTable = _heroPowersPriorityTable.Where(x => choices.Contains(x.Key)).ToList();
            return filteredTable.First(x => x.Value == filteredTable.Max(y => y.Value)).Key;
        }

        private void HandleTurnOneSpecifics(Board board, ref ProfileParameters parameters)
        {
            if (
                board.Hand.Count(
                    x => x.CurrentCost == 1 && x.Type == Card.CType.MINION && x.Template.Id != Cards.AbusiveSergeant) ==
                1)
                parameters.SpellsModifiers.AddOrUpdate(Card.Cards.GAME_005, new Modifier(200));

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

        private int GetOverloadSpellConservativeModifier(Board board)
        {
            return HasCardOnBoard(Cards.TunnelTrogg, board) && board.MinionEnemy.Count == 0
                ? OverloadSpellsConservativeModifier/2
                : OverloadSpellsConservativeModifier;
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

        private bool HasEnemyTauntOnBoard(Board board)
        {
            return board.MinionEnemy.Any(x => x.IsTaunt && !x.IsStealth);
        }

        private bool HasCardOnBoard(Card.Cards card, Board board)
        {
            return board.MinionFriend.Any(x => x.Template.Id == card);
        }

        private bool ShouldDrawCards(Board board)
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

        private bool ShouldPlayDoomhammer(Board board)
        {
            return !HasDoomhammerOnBoard(board) && HasDommhammerInHand(board) && CanPlayDoomhammer(board);
        }

        private int GetManaLeftAfterPlayingMinions(Board board)
        {
            var ret = board.ManaAvailable -
                      board.Hand.FindAll(x => x.Template.Type == Card.CType.MINION).Sum(x => x.CurrentCost);

            return ret < 0 ? 0 : ret;
        }

        private int GetEnemyHealthAndArmor(Board board)
        {
            return board.HeroEnemy.CurrentHealth + board.HeroEnemy.CurrentArmor;
        }

        private int GetSpellPower(Board board)
        {
            return board.MinionFriend.FindAll(x => x.IsSilenced == false).Sum(x => x.SpellPower);
        }

        private int GetPlayableSpellSequenceDamages(Board board, bool altogetherWithHammer = false)
        {
            return GetSpellSequenceDamages(GetPlayableSpellSequence(board, altogetherWithHammer), board);
        }

        private int GetSecondTurnLethalRange(Board board)
        {
            return GetEnemyHealthAndArmor(board) - GetPotentialFaceDamages(board);
        }

        private bool HasPotentialLethalNextTurn(Board board)
        {
            return GetRemainingBlastDamagesAfterSequence(board) >= GetSecondTurnLethalRange(board);
        }

        private int GetSpellSequenceDamages(List<Card.Cards> sequence, Board board)
        {
            return
                sequence.FindAll(x => _spellDamagesTable.ContainsKey(x))
                    .Sum(x => _spellDamagesTable[x] + GetSpellPower(board));
        }

        private List<Card.Cards> GetPlayableSpellSequence(Board board, bool altogetherWithHammer = false)
        {
            var ret = new List<Card.Cards>();
            var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

            foreach (var card in board.Hand)
            {
                if (_spellDamagesTable.ContainsKey(card.Template.Id) == false) continue;
                if (manaAvailable < card.CurrentCost) continue;

                ret.Add(card.Template.Id);
                manaAvailable -= card.CurrentCost;
            }

            return ret;
        }

        private int GetPotentialFaceDamages(Board board)
        {
            return GetPotentialWeaponDamages(board) +
                   GetPlayableSpellSequenceDamages(board, ShouldPlayDoomhammer(board));
        }

        private int GetRemainingBlastDamagesAfterSequence(Board board)
        {
            return GetTotalBlastDamagesInHand(board) -
                   GetPlayableSpellSequenceDamages(board, ShouldPlayDoomhammer(board));
        }

        private int GetTotalBlastDamagesInHand(Board board)
        {
            return
                board.Hand.FindAll(x => _spellDamagesTable.ContainsKey(x.Template.Id))
                    .Sum(x => _spellDamagesTable[x.Template.Id] + GetSpellPower(board));
        }

        private int GetPotentialWeaponDamages(Board board)
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

        private int GetPlayableRockbiters(Board board, bool altogetherWithHammer = false)
        {
            var handCount = board.Hand.Count(x => x.Template.Id == Cards.RockbiterWeapon);
            var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

            if (manaAvailable < handCount)
            {
                handCount = manaAvailable;
            }

            return handCount < 0 ? 0 : handCount;
        }

        private bool HasDoomhammerOnBoard(Board board)
        {
            return board.WeaponFriend != null && board.WeaponFriend.Template.Id == Cards.Doomhammer;
        }

        private bool HasDommhammerInHand(Board board)
        {
            return board.Hand.Any(x => x.Template.Id == Cards.Doomhammer);
        }

        private bool CanPlayDoomhammer(Board board)
        {
            return board.ManaAvailable >= 5;
        }

        private bool CanPlayDoomhammerNextTurn(Board board)
        {
            return board.ManaAvailable - board.LockedMana + 1 >= 5 && HasDommhammerInHand(board);
        }
    }
}