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

        private const Card.Cards EarthShock = Card.Cards.EX1_245;
        private const Card.Cards LightningBolt = Card.Cards.EX1_238;
        private const Card.Cards RockbiterWeapon = Card.Cards.CS2_045;
        private const Card.Cards TunnelTrogg = Card.Cards.LOE_018;
        private const Card.Cards AncestralKnowledge = Card.Cards.AT_053;
        private const Card.Cards Crackle = Card.Cards.GVG_038;
        private const Card.Cards LaveShock = Card.Cards.BRM_011;
        private const Card.Cards TotemGolem = Card.Cards.AT_052;
        private const Card.Cards ElementalDestruction = Card.Cards.AT_051;
        private const Card.Cards FeralSpirit = Card.Cards.EX1_248;
        private const Card.Cards Hex = Card.Cards.EX1_246;
        private const Card.Cards LavaBurst = Card.Cards.EX1_241;
        private const Card.Cards ManaTideTotem = Card.Cards.EX1_575;
        private const Card.Cards DoomHammer = Card.Cards.EX1_567;
        private const Card.Cards LeperGnome = Card.Cards.EX1_029;
        private const Card.Cards BloodmageThalnos = Card.Cards.EX1_012;
        private const Card.Cards LootHoarder = Card.Cards.EX1_096;
        private const Card.Cards IronbeakOwl = Card.Cards.CS2_203;
        private const Card.Cards UnboundElemental = Card.Cards.EX1_258;
        private const Card.Cards ArcaneGolem = Card.Cards.EX1_089;
        private const Card.Cards KnifeJuggler = Card.Cards.NEW1_019;
        private const Card.Cards SludgeBelcher = Card.Cards.FP1_012;

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
            {TotemGolem, CardTemplate.TemplateList[TotemGolem].Overload}
        };

        private readonly Dictionary<Card.Cards, int> _spellDamagesTable = new Dictionary<Card.Cards, int>
        {
            {EarthShock, 1},
            {LightningBolt, 3},
            {Crackle, 4},
            {LavaBurst, 5},
            {LaveShock, 2}
        };

        private readonly Dictionary<Card.Cards, int> _spellsOverloadTable = new Dictionary<Card.Cards, int>
        {
            {LightningBolt, CardTemplate.TemplateList[LightningBolt].Overload},
            {Crackle, CardTemplate.TemplateList[Crackle].Overload},
            {LavaBurst, CardTemplate.TemplateList[LavaBurst].Overload},
            {DoomHammer, CardTemplate.TemplateList[DoomHammer].Overload},
            {ElementalDestruction, CardTemplate.TemplateList[ElementalDestruction].Overload},
            {AncestralKnowledge, CardTemplate.TemplateList[AncestralKnowledge].Overload},
            {FeralSpirit, CardTemplate.TemplateList[FeralSpirit].Overload}
        };

        public ProfileParameters GetParameters(Board board)
        {
            //Init profile parameter based on rush profile
            var parameters = new ProfileParameters(BaseProfile.Rush);

            //300% of default "Rush" profile value -> the bot will be more aggressive
            parameters.GlobalAggroModifier.Value = AggroModifier;

            //Set FeralSpirit spell modifier to 20% of the base spell value defined in "Rush" profile, the AI has more chances to play this spell
            parameters.SpellsModifiers.AddOrUpdate(FeralSpirit, new Modifier(20));

            //Set lava shock spell modifier to 200% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand without any overloaded mana
            parameters.SpellsModifiers.AddOrUpdate(LaveShock, new Modifier(200));

            //Lower TheCoin modifier
            parameters.SpellsModifiers.AddOrUpdate(TheCoin, new Modifier(70));

            //Lower earthshock modifier on Sludge belcher
            parameters.SpellsModifiers.AddOrUpdate(EarthShock, new Modifier(20, SludgeBelcher));

            //Set KnifeJuggler modifier to 30% of the base value defined in "Rush" profile, the AI has more chances to play it
            parameters.MinionsModifiers.AddOrUpdate(KnifeJuggler, new Modifier(0));

            //If we cant put down enemy's life at topdeck lethal range
            if (!HasPotentialLethalNextTurn(board))
            {
                //Set lightning bolt spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand before turn 6
                parameters.SpellsModifiers.AddOrUpdate(LightningBolt,
                    new Modifier(GetOverloadSpellConservativeModifier(board) / 3));

                //Set crackle spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand before turn 6
                parameters.SpellsModifiers.AddOrUpdate(Crackle,
                    new Modifier(GetOverloadSpellConservativeModifier(board)));

                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.SpellsModifiers.AddOrUpdate(LavaBurst,
                    new Modifier(GetOverloadSpellConservativeModifier(board)));

                //Set lava burst spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand for lethal
                parameters.MinionsModifiers.AddOrUpdate(ArcaneGolem, new Modifier(400));
            }

            if (!HasEnemyTauntOnBoard(board))
            {
                //Set silence to 150% of its base value to try to keep it in hand if there's no enemy taunt on board
                parameters.MinionsModifiers.AddOrUpdate(IronbeakOwl, new Modifier(150));
            }
            else
            {
                //Set silence to 60% of its base value to make it easier to play if theres a taunt on board
                parameters.MinionsModifiers.AddOrUpdate(IronbeakOwl, new Modifier(60));
            }

            if (!HasDoomhammerOnBoard(board)) //If we don't have doomhammer this turn
            {
                //Set rockbiter spell modifier to 400% of the base spell value defined in "Rush" profile, the bot will try to keep this spell in hand until we get doomhammer
                parameters.SpellsModifiers.AddOrUpdate(RockbiterWeapon, new Modifier(400));
            }

            if (ShouldDrawCards(board)) //If we need to draw cards
            {
                //Set AncestralKnowledge spell modifier to 0% of the base spell value defined in "Rush" profile, the bot will play the spell more easily
                parameters.SpellsModifiers.AddOrUpdate(AncestralKnowledge, new Modifier(0));
                parameters.GlobalDrawModifier = new Modifier(150);
            }
            else
            {
                parameters.GlobalDrawModifier = new Modifier(50);
            }

            //Turn specific handlers
            switch (board.TurnCount)
            {
                case 1:
                    HandleTurnOneSpecifics(board, ref parameters);
                    break;

                case 2:
                    HandleTurnTwoSpecifics(board, ref parameters);
                    break;
            }

            //If we can play doomhammer next turn we don't want to overload
            if (!HasDoomhammerOnBoard(board) && CanPlayDoomhammerNextTurn(board))
            {
                if (board.MinionFriend.Count > 0)
                    OverrideOverloadMinionsModifiers(ref parameters);

                OverrideOverloadSpellsModifiers(ref parameters);
            }

            return parameters;
        }

        public Card.Cards SirFinleyChoice(List<Card.Cards> choices)
        {
            var filteredTable = _heroPowersPriorityTable.Where(x => choices.Contains(x.Key)).ToList();
            return filteredTable.First(x => x.Value == filteredTable.Max(y => y.Value)).Key;
        }

        private void HandleTurnOneSpecifics(Board board, ref ProfileParameters parameters)
        {
            //Set TunnelTrogg modifier to -100% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
            parameters.MinionsModifiers.AddOrUpdate(TunnelTrogg, new Modifier(-100));
			
			 //Set LeperGnome modifier to -100% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
            parameters.MinionsModifiers.AddOrUpdate(LeperGnome, new Modifier(-100));
        }

        private void HandleTurnTwoSpecifics(Board board, ref ProfileParameters parameters)
        {
            //Set UnboundElemental modifier to -500% of the base value defined in "Rush" profile, the bot will try as much as possible to play the card
            parameters.MinionsModifiers.AddOrUpdate(UnboundElemental, new Modifier(-500));
        }

        private int GetOverloadSpellConservativeModifier(Board board)
        {
            return HasCardOnBoard(TunnelTrogg, board)
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
            if (board.Hand.Any(x => x.Template.Id == AncestralKnowledge) && GetManaLeftAfterPlayingMinions(board) >= 2)
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
            return GetSpellSequenceDamages(GetPlayableSpellSequence(board, altogetherWithHammer));
        }

        private int GetSecondTurnLethalRange(Board board)
        {
            return GetEnemyHealthAndArmor(board) - GetPotentialFaceDamages(board);
        }

        private bool HasPotentialLethalNextTurn(Board board)
        {
            return GetRemainingBlastDamagesAfterSequence(board) >= GetSecondTurnLethalRange(board);
        }

        private int GetSpellSequenceDamages(List<Card.Cards> sequence)
        {
            return sequence.FindAll(x => _spellDamagesTable.ContainsKey(x)).Sum(x => _spellDamagesTable[x]);
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
            var handCount = board.Hand.Count(x => x.Template.Id == RockbiterWeapon);
            var manaAvailable = altogetherWithHammer ? board.ManaAvailable - 5 : board.ManaAvailable;

            return Math.Max(handCount, manaAvailable <= 2 ? manaAvailable : handCount);
        }

        private bool HasDoomhammerOnBoard(Board board)
        {
            return board.WeaponFriend != null && board.WeaponFriend.Template.Id == DoomHammer;
        }

        private bool HasDommhammerInHand(Board board)
        {
            return board.Hand.Any(x => x.Template.Id == DoomHammer);
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