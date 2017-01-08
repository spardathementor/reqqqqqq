#region LICENSE

/*
 Copyright 2014 - 2015 LeagueSharp
 Orbwalking.cs is part of LeagueSharp.Common.
 
 LeagueSharp.Common is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 
 LeagueSharp.Common is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.
 
 You should have received a copy of the GNU General Public License
 along with LeagueSharp.Common. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

#region

using System;
using System.Collections.Generic;
using System.Linq;

using LeagueSharp;
using LeagueSharp.Common;

using SharpDX;

using Color = System.Drawing.Color;

namespace HoolaRiven {

    /// <summary>
    ///     This class offers everything related to auto-attacks and orbwalking.
    /// </summary>
    public static class Orbwalking {
        /// <summary>
        /// An array of the last 3 targets as NetworkIDs, useful for 3-hit passives or thunderlord
        /// </summary>
        public static int[] LastTargets = new int[] { 0,0,0 };

        /// <summary>
        /// Delegate AfterAttackEvenH
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        public delegate void AfterAttackEvenH(AttackableUnit unit,AttackableUnit target);

        /// <summary>
        /// Delegate BeforeAttackEvenH
        /// </summary>
        /// <param name="args">The <see cref="BeforeAttackEventArgs"/> instance containing the event data.</param>
        public delegate void BeforeAttackEvenH(BeforeAttackEventArgs args);

        /// <summary>
        /// Delegate OnAttackEvenH
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        public delegate void OnAttackEvenH(AttackableUnit unit,AttackableUnit target);

        /// <summary>
        /// Delegate OnNonKillableMinionH
        /// </summary>
        /// <param name="minion">The minion.</param>
        public delegate void OnNonKillableMinionH(AttackableUnit minion);

        /// <summary>
        /// Delegate OnTargetChangeH
        /// </summary>
        /// <param name="oldTarget">The old target.</param>
        /// <param name="newTarget">The new target.</param>
        public delegate void OnTargetChangeH(AttackableUnit oldTarget,AttackableUnit newTarget);

        /// <summary>
        /// The orbwalking mode.
        /// </summary>
        public enum OrbwalkingMode {
            LastHit,
            Mixed,
            LaneClear,
            Combo,
            CustomMode,
            Flee,
            FastHarass,
            Burst,
            None
        }
        /// <summary>
        /// Spells that are not attacks even if they have the "attack" word in their name.
        /// </summary>
        private static readonly string[] NoAttacks =
        {
            "volleyattack", "volleyattackwithsound", "jarvanivcataclysmattack",
            "monkeykingdoubleattack", "shyvanadoubleattack",
            "shyvanadoubleattackdragon", "zyragraspingplantattack",
            "zyragraspingplantattack2", "zyragraspingplantattackfire",
            "zyragraspingplantattack2fire", "viktorpowertransfer",
            "sivirwattackbounce", "asheqattacknoonhit",
            "elisespiderlingbasicattack", "heimertyellowbasicattack",
            "heimertyellowbasicattack2", "heimertbluebasicattack",
            "annietibbersbasicattack", "annietibbersbasicattack2",
            "yorickdecayedghoulbasicattack", "yorickravenousghoulbasicattack",
            "yorickspectralghoulbasicattack", "malzaharvoidlingbasicattack",
            "malzaharvoidlingbasicattack2", "malzaharvoidlingbasicattack3",
            "kindredwolfbasicattack", "kindredbasicattackoverridelightbombfinal"
        };


        /// <summary>
        /// Spells that are attacks even if they dont have the "attack" word in their name.
        /// </summary>
        private static readonly string[] Attacks =
        {
            "caitlynheadshotmissile", "frostarrow", "garenslash2",
            "kennenmegaproc", "lucianpassiveattack", "masteryidoublestrike", "quinnwenhanced", "renektonexecute",
            "renektonsuperexecute", "rengarnewpassivebuffdash", "trundleq", "xenzhaothrust", "xenzhaothrust2",
            "xenzhaothrust3", "viktorqbuff"
        };

        /// <summary>
        /// The last auto attack tick
        /// </summary>
        public static int LastAATick;

        /// <summary>
        /// <c>true</c> if the orbwalker will attack.
        /// </summary>
        public static bool Attack = true;

        /// <summary>
        /// <c>true</c> if the orbwalker will skip the next attack.
        /// </summary>
        public static bool DisableNextAttack;

        /// <summary>
        /// <c>true</c> if the orbwalker will move.
        /// </summary>
        public static bool Move = true;

        /// <summary>
        /// The tick the most recent attack command was sent.
        /// </summary>
        public static int LastAttackCommandT;

        /// <summary>
        /// The tick the most recent move command was sent.
        /// </summary>
        public static int LastMoveCommandT;

        /// <summary>
        /// The last move command position
        /// </summary>
        public static Vector3 LastMoveCommandPosition = Vector3.Zero;

        /// <summary>
        /// The last target
        /// </summary>
        private static AttackableUnit lastTarget;

        /// <summary>
        /// The player
        /// </summary>
        private static readonly Obj_AI_Hero Player;

        /// <summary>
        /// The delay
        /// </summary>
        private static int delay;

        /// <summary>
        /// The minimum distance
        /// </summary>
        private static float minDistance = 400;

        /// <summary>
        /// <c>true</c> if the auto attack missile was launched from the player.
        /// </summary>
        private static bool missileLaunched;

        /// <summary>
        /// The champion name
        /// </summary>
        private static string championName;

        /// <summary>
        /// The random
        /// </summary>
        private static readonly Random Random = new Random(DateTime.Now.Millisecond);

        /// <summary>
        /// Initializes static members of the <see cref="Orbwalking"/> class.
        /// </summary>
        static Orbwalking() {
            Player = ObjectManager.Player;
            championName = Player.ChampionName;
            Obj_AI_Base.OnProcessSpellCast += OnProcessSpell;
            Obj_AI_Base.OnDoCast += Obj_AI_Base_OnDoCast;
            Spellbook.OnStopCast += SpellbookOnStopCast;
        }

        /// <summary>
        /// This event is fired before the player auto attacks.
        /// </summary>
        public static event BeforeAttackEvenH BeforeAttack;

        /// <summary>
        /// This event is fired when a unit is about to auto-attack another unit.
        /// </summary>
        public static event OnAttackEvenH OnAttack;

        /// <summary>
        /// This event is fired after a unit finishes auto-attacking another unit (Only works with player for now).
        /// </summary>
        public static event AfterAttackEvenH AfterAttack;

        /// <summary>
        /// Gets called on target changes
        /// </summary>
        public static event OnTargetChangeH OnTargetChange;

        ///<summary>
        /// Occurs when a minion is not killable by an auto attack.
        /// </summary>
        public static event OnNonKillableMinionH OnNonKillableMinion;

        /// <summary>
        /// Fires the before attack event.
        /// </summary>
        /// <param name="target">The target.</param>
        private static void FireBeforeAttack(AttackableUnit target) {
            if(BeforeAttack != null) {
                BeforeAttack(new BeforeAttackEventArgs { Target = target });
            } else {
                DisableNextAttack = false;
            }
        }

        /// <summary>
        /// Fires the on attack event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        private static void FireOnAttack(AttackableUnit unit,AttackableUnit target) {
            OnAttack?.Invoke(unit,target);
        }

        /// <summary>
        /// Pushes a target to the <see cref="LastTargets"/> list.
        /// </summary>
        /// <param name="networkId"></param>
        private static void PushLastTargets(int networkId) {
            LastTargets[2] = LastTargets[1];
            LastTargets[1] = LastTargets[0];
            LastTargets[0] = networkId;
        }

        /// <summary>
        /// Fires the after attack event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="target">The target.</param>
        private static void FireAfterAttack(AttackableUnit unit,AttackableUnit target) {
            if(AfterAttack != null && target.IsValidTarget()) {
                AfterAttack(unit,target);
            }
        }

        /// <summary>
        /// Fires the on target switch event.
        /// </summary>
        /// <param name="newTarget">The new target.</param>
        private static void FireOnTargetSwitch(AttackableUnit newTarget) {
            if(OnTargetChange != null && (!lastTarget.IsValidTarget() || lastTarget != newTarget)) {
                OnTargetChange(lastTarget,newTarget);
            }
        }

        /// <summary>
        /// Fires the on non killable minion event.
        /// </summary>
        /// <param name="minion">The minion.</param>
        private static void FireOnNonKillableMinion(AttackableUnit minion) {
            OnNonKillableMinion?.Invoke(minion);
        }

        /// <summary>
        /// Returns true if the spellname is an auto-attack.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns><c>true</c> if the name is an auto attack; otherwise, <c>false</c>.</returns>
        public static bool IsAutoAttack(string name) {
            return (name.ToLower().Contains("attack")
                    && !NoAttacks.Contains(name.ToLower()))
                   || Attacks.Contains(name.ToLower());
        }

        /// <summary>
        /// Returns the auto-attack range of local player with respect to the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>System.Single.</returns>
        public static float GetRealAutoAttackRange(AttackableUnit target) {
            var result = Player.AttackRange + Player.BoundingRadius;

            if(!target.IsValidTarget()) {
                return result;
            }

            var aiBase = target as Obj_AI_Base;

            if(aiBase == null) {
                return result + target.BoundingRadius;
            }

            return result + target.BoundingRadius;
        }

        /// <summary>
        /// Returns the auto-attack range of the target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns>System.Single.</returns>
        public static float GetAttackRange(Obj_AI_Hero target) {
            var result = target.AttackRange + target.BoundingRadius;
            return result;
        }

        /// <summary>
        /// Returns true if the target is in auto-attack range.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <returns><c>true</c> if XXXX, <c>false</c> otherwise.</returns>
        public static bool InAutoAttackRange(AttackableUnit target) {
            if(!target.IsValidTarget()) {
                return false;
            }
            var myRange = GetRealAutoAttackRange(target);

            return Vector2.DistanceSquared((target as Obj_AI_Base)?.ServerPosition.To2D() ?? target.Position.To2D(),Player.ServerPosition.To2D()) <= myRange * myRange;
        }

        /// <summary>
        /// Returns if the player's auto-attack is ready.
        /// </summary>
        /// <returns><c>true</c> if this instance can attack; otherwise, <c>false</c>.</returns>
        public static bool CanAttack() {
            if(Player.HasBuffOfType(BuffType.Blind)) {
                return false;
            }

            return Utils.GameTimeTickCount + Game.Ping / 2 + 25 >= LastAATick + Player.AttackDelay * 1000;
        }

        /// <summary>
        /// Returns true if moving won't cancel the auto-attack.
        /// </summary>
        /// <param name="extraWindup">The extra windup.</param>
        /// <param name="disableMissileCheck"></param>
        /// <returns><c>true</c> if this instance can move the specified extra windup; otherwise, <c>false</c>.</returns>
        public static bool CanMove(float extraWindup,bool disableMissileCheck = false) {
            if(missileLaunched && Orbwalker.MissileCheck && !disableMissileCheck) {
                return true;
            }

            return Utils.GameTimeTickCount >= LastAATick + Player.AttackCastDelay * 1000 + extraWindup;
        }

        /// <summary>
        /// Sets the movement delay.
        /// </summary>
        /// <param name="delay">The delay.</param>
        public static void SetMovementDelay(int delay) {
            Orbwalking.delay = delay;
        }

        /// <summary>
        /// Sets the minimum orbwalk distance.
        /// </summary>
        /// <param name="d">The d.</param>
        public static void SetMinimumOrbwalkDistance(float d) {
            minDistance = d;
        }

        /// <summary>
        /// Gets the last move time.
        /// </summary>
        /// <returns>System.Single.</returns>
        public static float GetLastMoveTime() {
            return LastMoveCommandT;
        }

        /// <summary>
        /// Gets the last move position.
        /// </summary>
        /// <returns>Vector3.</returns>
        public static Vector3 GetLastMovePosition() {
            return LastMoveCommandPosition;
        }

        /// <summary>
        /// Moves to the position.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <param name="holdAreaRadius">The hold area radius.</param>
        /// <param name="overrideTimer">if set to <c>true</c> [override timer].</param>
        /// <param name="useFixedDistance">if set to <c>true</c> [use fixed distance].</param>
        /// <param name="randomizeMinDistance">if set to <c>true</c> [randomize minimum distance].</param>
        public static void MoveTo(Vector3 position,
            float holdAreaRadius = 0,
            bool overrideTimer = false,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true) {
            var playerPosition = Player.ServerPosition;

            if(playerPosition.Distance(position,true) < holdAreaRadius * holdAreaRadius) {
                if(Player.Path.Length <= 0) {
                    return;
                }

                Player.IssueOrder(GameObjectOrder.Stop,playerPosition);

                LastMoveCommandPosition = playerPosition;
                LastMoveCommandT = Utils.GameTimeTickCount - 70;
                return;
            }

            var point = position;

            if(Player.Distance(point,true) < 22500) {
                point = playerPosition.Extend(position,(randomizeMinDistance ? (Random.NextFloat(0.6f,1) + 0.2f) * minDistance : minDistance));
            }

            var angle = 0f;
            var currentPath = Player.GetWaypoints();
            if(currentPath.Count > 1 && currentPath.PathLength() > 100) {
                var movePath = Player.GetPath(point);

                if(movePath.Length > 1) {
                    var v1 = currentPath[1] - currentPath[0];
                    var v2 = movePath[1] - movePath[0];
                    angle = v1.AngleBetween(v2.To2D());
                    var distance = movePath.Last().To2D().Distance(currentPath.Last(),true);

                    if((angle < 10 && distance < 250000) || distance < 250) {
                        return;
                    }
                }
            }

            if(Utils.GameTimeTickCount - LastMoveCommandT < 70 + Math.Min(60,Game.Ping) && !overrideTimer && angle < 60) {
                return;
            }

            if(angle >= 60 && Utils.GameTimeTickCount - LastMoveCommandT < 60) {
                return;
            }

            Player.IssueOrder(GameObjectOrder.MoveTo,point);
            LastMoveCommandPosition = point;
            LastMoveCommandT = Utils.GameTimeTickCount;
        }

        /// <summary>
        /// Orbwalks a target while moving to Position.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="position">The position.</param>
        /// <param name="extraWindup">The extra windup.</param>
        /// <param name="holdAreaRadius">The hold area radius.</param>
        /// <param name="useFixedDistance">if set to <c>true</c> [use fixed distance].</param>
        /// <param name="randomizeMinDistance">if set to <c>true</c> [randomize minimum distance].</param>
        public static void Orbwalk(AttackableUnit target,
            Vector3 position,
            float extraWindup = 0,
            float holdAreaRadius = 0,
            bool useFixedDistance = true,
            bool randomizeMinDistance = true) {
            if(Utils.GameTimeTickCount - LastAttackCommandT < 70 + Math.Min(60,Game.Ping)) {
                return;
            }

            try {
                if(target.IsValidTarget() && CanAttack()) {
                    DisableNextAttack = false;
                    FireBeforeAttack(target);

                    if(DisableNextAttack || !Player.IssueOrder(GameObjectOrder.AttackUnit,target)) {
                        return;
                    }

                    LastAttackCommandT = Utils.GameTimeTickCount;
                    lastTarget = target;

                    return;
                }

                if(CanMove(extraWindup) && Move) {
                    MoveTo(position,Math.Max(holdAreaRadius,30),false,useFixedDistance,randomizeMinDistance);
                }
            } catch(Exception e) {
                Console.WriteLine(e.ToString());
            }
        }

        /// <summary>
        /// Resets the Auto-Attack timer.
        /// </summary>
        public static void ResetAutoAttackTimer() {
            LastAATick = 0;
        }

        /// <summary>
        /// Fired when the spellbook stops casting a spell.
        /// </summary>
        /// <param name="spellbook">The spellbook.</param>
        /// <param name="args">The <see cref="SpellbookStopCastEventArgs"/> instance containing the event data.</param>
        private static void SpellbookOnStopCast(Spellbook spellbook,SpellbookStopCastEventArgs args) {
            if(spellbook.Owner.IsValid && spellbook.Owner.IsMe && args.DestroyMissile && args.StopAnimation) {
                ResetAutoAttackTimer();
            }
        }

        /// <summary>
        /// Fired when an auto attack is fired.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameObjectProcessSpellCastEventArgs"/> instance containing the event data.</param>
        private static void Obj_AI_Base_OnDoCast(Obj_AI_Base sender,GameObjectProcessSpellCastEventArgs args) {
            if(!sender.IsMe) {
                return;
            }

            if(Game.Ping <= 30) //First world problems kappa
            {
                Utility.DelayAction.Add(30,() => Obj_AI_Base_OnDoCast_Delayed(sender,args));
                return;
            }

            Obj_AI_Base_OnDoCast_Delayed(sender,args);
        }

        /// <summary>
        /// Fired 30ms after an auto attack is launched.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="args">The <see cref="GameObjectProcessSpellCastEventArgs"/> instance containing the event data.</param>
        private static void Obj_AI_Base_OnDoCast_Delayed(Obj_AI_Base sender,GameObjectProcessSpellCastEventArgs args) {
            if(!IsAutoAttack(args.SData.Name)) {
                return;
            }

            FireAfterAttack(sender,args.Target as AttackableUnit);
            missileLaunched = true;
        }

        /// <summary>
        /// Handles the <see cref="E:ProcessSpell" /> event.
        /// </summary>
        /// <param name="unit">The unit.</param>
        /// <param name="spell">The <see cref="GameObjectProcessSpellCastEventArgs"/> instance containing the event data.</param>
        private static void OnProcessSpell(Obj_AI_Base unit,GameObjectProcessSpellCastEventArgs spell) {
            try {
                if(!unit.IsMe) {
                    return;
                }

                var spellName = spell.SData.Name;

                if(!IsAutoAttack(spellName)) {
                    return;
                }

                if(spell.Target is Obj_AI_Base || spell.Target is Obj_BarracksDampener || spell.Target is Obj_HQ) {
                    PushLastTargets(spell.Target.NetworkId);

                    LastAATick = Utils.GameTimeTickCount - Game.Ping / 2;
                    missileLaunched = false;
                    LastMoveCommandT = 0;

                    var @base = spell.Target as Obj_AI_Base;
                    if(@base != null) {
                        var target = @base;
                        if(target.IsValid) {
                            FireOnTargetSwitch(target);
                            lastTarget = target;
                        }
                    }
                }

                FireOnAttack(unit,lastTarget);
            } catch(Exception e) {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// The before attack event arguments.
        /// </summary>
        public class BeforeAttackEventArgs:EventArgs {
            /// <summary>
            /// <c>true</c> if the orbwalker should continue with the attack.
            /// </summary>
            private bool process = true;

            /// <summary>
            /// The target
            /// </summary>
            public AttackableUnit Target;

            /// <summary>
            /// The unit
            /// </summary>
            public Obj_AI_Base Unit = ObjectManager.Player;

            /// <summary>
            /// Gets or sets a value indicating whether this <see cref="BeforeAttackEventArgs"/> should continue with the attack.
            /// </summary>
            /// <value><c>true</c> if the orbwalker should continue with the attack; otherwise, <c>false</c>.</value>
            public bool Process {
                get { return process; }
                set {
                    DisableNextAttack = !value;
                    process = value;
                }
            }
        }

        /// <summary>
        /// This class allows you to add an instance of "Orbwalker" to your assembly in order to control the orbwalking in an
        /// easy way.
        /// </summary>
        public class Orbwalker:IDisposable {
            /// <summary>
            /// The lane clear wait time modifier.
            /// </summary>
            private const float LaneClearWaitTimeMod = 2f;

            /// <summary>
            /// The configuration
            /// </summary>
            private static Menu config;

            /// <summary>
            /// The player
            /// </summary>
            private readonly Obj_AI_Hero Player;

            /// <summary>
            /// The forced target
            /// </summary>
            private Obj_AI_Base forcedTarget;

            /// <summary>
            /// The orbalker mode
            /// </summary>
            private OrbwalkingMode mode = OrbwalkingMode.None;

            /// <summary>
            /// The orbwalking point
            /// </summary>
            private Vector3 orbwalkingPoint;

            /// <summary>
            /// The previous minion the orbwalker was targeting.
            /// </summary>
            private Obj_AI_Minion _prevMinion;

            /// <summary>
            /// The instances of the orbwalker.
            /// </summary>
            public static List<Orbwalker> Instances = new List<Orbwalker>();

            /// <summary>
            /// The name of the CustomMode if it is set.
            /// </summary>
            private string customModeName;
            /// <summary>

            /// Initializes a new instance of the <see cref="Orbwalker"/> class.
            /// </summary>
            /// <param name="attachToMenu">The menu the orbwalker should attach to.</param>
            public Orbwalker(Menu attachToMenu) {
                config = attachToMenu;
                /* Drawings submenu */
                var drawings = new Menu("Drawings","drawings");

                drawings.AddItem(new MenuItem("AACircle","AACircle").SetShared()
                    .SetValue(new Circle(true,Color.FromArgb(155,255,255,0))));

                drawings.AddItem(new MenuItem("AACircle2","Enemy AA circle").SetShared()
                    .SetValue(new Circle(false,Color.FromArgb(155,255,255,0))));

                drawings.AddItem(new MenuItem("HoldZone","HoldZone").SetShared()
                    .SetValue(new Circle(false,Color.FromArgb(155,255,255,0))));

                drawings.AddItem(new MenuItem("AALineWidth","Line Width")).SetShared()
                    .SetValue(new Slider(2,1,6));

                config.AddSubMenu(drawings);

                /* Misc options */
                var misc = new Menu("Misc","Misc");

                misc.AddItem(new MenuItem("HoldPosRadius","Hold Position Radius").SetShared().SetValue(new Slider(50,50,250)));
                misc.AddItem(new MenuItem("PriorizeFarm","Priorize farm over harass").SetShared().SetValue(true));
                misc.AddItem(new MenuItem("PrioritizeCasters","Attack caster minions first").SetShared().SetValue(false));
                misc.AddItem(new MenuItem("AttackWards","Auto attack wards").SetShared().SetValue(false));
                misc.AddItem(new MenuItem("AttackGPBarrel","Auto attack gangplank barrel").SetShared()
                    .SetValue(new StringList(new[] { "Combo and Farming","Farming","No" },1)));
                misc.AddItem(new MenuItem("AttackPetsnTraps","Auto attack pets & traps").SetShared().SetValue(true));
                misc.AddItem(new MenuItem("Smallminionsprio","Jungle clear small first").SetShared().SetValue(false));
                misc.AddItem(new MenuItem("FocusMinionsOverTurrets","Focus minions over objectives").SetShared().SetValue(new KeyBind('M',KeyBindType.Toggle)));

                config.AddSubMenu(misc);

                /* Missile check */
                config.AddItem(new MenuItem("MissileCheck","Use Missile Check").SetShared().SetValue(true));

                /* Delay sliders */
                config.AddItem(
                    new MenuItem("ExtraWindup","Extra windup time").SetShared().SetValue(new Slider(60,0,200)));
                config.AddItem(new MenuItem("FarmDelay","Farm delay").SetShared().SetValue(new Slider(0,0,200)));

                /*Load the menu*/

                config.AddItem(new MenuItem("Flee","Flee").SetShared().SetValue(new KeyBind('Z',KeyBindType.Press)));

                config.AddItem(new MenuItem("LastHit","Last hit").SetShared().SetValue(new KeyBind('X',KeyBindType.Press)));

                config.AddItem(new MenuItem("Farm","Mixed").SetShared().SetValue(new KeyBind('C',KeyBindType.Press)));

                //    config.AddItem(new MenuItem("LWH", "Last Hit While Harass").SetShared().SetValue(false));

                config.AddItem(new MenuItem("LaneClear","LaneClear").SetShared().SetValue(new KeyBind('V',KeyBindType.Press)));

                config.AddItem(new MenuItem("Orbwalk","Combo").SetShared().SetValue(new KeyBind(32,KeyBindType.Press)));

                config.AddItem(new MenuItem("Burst","Burst").SetShared().SetValue(new KeyBind('T',KeyBindType.Press)));

                config.AddItem(new MenuItem("FastHarass","Fast Harass").SetShared().SetValue(new KeyBind('Y',KeyBindType.Press)));

                //config.AddItem(new MenuItem("StillCombo", "Combo without moving").SetShared().SetValue(new KeyBind('N', KeyBindType.Press)));

                Player = ObjectManager.Player;
                Game.OnUpdate += GameOnOnGameUpdate;
                Drawing.OnDraw += DrawingOnOnDraw;
                Instances.Add(this);
            }

            /// <summary>
            /// Determines if a target is in auto attack range.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <returns><c>true</c> if a target is in auto attack range, <c>false</c> otherwise.</returns>
            public virtual bool InAutoAttackRange(AttackableUnit target) {
                return Orbwalking.InAutoAttackRange(target);
            }

            /// <summary>
            /// Gets the farm delay.    
            /// </summary>
            /// <value>The farm delay.</value>
            private int FarmDelay => config.Item("FarmDelay").GetValue<Slider>().Value;

            /// <summary>
            /// Gets a value indicating whether the orbwalker is orbwalking by checking the missiles.
            /// </summary>
            /// <value><c>true</c> if the orbwalker is orbwalking by checking the missiles; otherwise, <c>false</c>.</value>
            public static bool MissileCheck => config.Item("MissileCheck").GetValue<bool>();

            /// <summary>
            /// Registers the Custom Mode of the Orbwalker. Useful for adding a flee mode and such.
            /// </summary>
            /// <param name="name">The name of the mode Ex. "Myassembly.FleeMode" </param>
            /// <param name="displayname">The name of the mode in the menu. Ex. Flee</param>
            /// <param name="key">The default key for this mode.</param>
            public virtual void RegisterCustomMode(string name,string displayname,uint key) {
                customModeName = name;

                if(config.Item(name) == null) {
                    config.AddItem(new MenuItem(name,displayname).SetShared().SetValue(new KeyBind(key,KeyBindType.Press)));
                }
            }

            /// <summary>
            /// Gets or sets the active mode.
            /// </summary>
            /// <value>The active mode.</value>
            public OrbwalkingMode ActiveMode {
                get {
                    if(mode != OrbwalkingMode.None) {
                        return mode;
                    }

                    if(config.Item("Orbwalk").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.Combo;
                    }

                    //if (config.Item("StillCombo").GetValue<KeyBind>().Active)
                    //{
                    //    return OrbwalkingMode.Combo;
                    //}

                    if(config.Item("LaneClear").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.LaneClear;
                    }

                    if(config.Item("Farm").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.Mixed;
                    }

                    if(config.Item("LastHit").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.LastHit;
                    }

                    if(config.Item("Flee").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.Flee;
                    }

                    if(config.Item("FastHarass").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.FastHarass;
                    }

                    if(config.Item("Burst").GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.Burst;
                    }

                    if(config.Item(customModeName) != null && config.Item(customModeName).GetValue<KeyBind>().Active) {
                        return OrbwalkingMode.CustomMode;
                    }

                    return OrbwalkingMode.None;
                }
                set { mode = value; }
            }

            /// <summary>
            /// Enables or disables the auto-attacks.
            /// </summary>
            /// <param name="b">if set to <c>true</c> the orbwalker will attack units.</param>
            public void SetAttack(bool b) {
                Attack = b;
            }

            /// <summary>
            /// Enables or disables the movement.
            /// </summary>
            /// <param name="b">if set to <c>true</c> the orbwalker will move.</param>
            public void SetMovement(bool b) {
                Move = b;
            }

            /// <summary>
            /// Forces the orbwalker to attack the set target if valid and in range.
            /// </summary>
            /// <param name="target">The target.</param>
            public void ForceTarget(Obj_AI_Base target) {
                forcedTarget = target;
            }

            /// <summary>
            /// Forces the orbwalker to move to that point while orbwalking (Game.CursorPos by default).
            /// </summary>
            /// <param name="point">The point.</param>
            public void SetOrbwalkingPoint(Vector3 point) {
                orbwalkingPoint = point;
            }

            /// <summary>
            /// Determines if the orbwalker should wait before attacking a minion.
            /// </summary>
            /// <returns><c>true</c> if the orbwalker should wait before attacking a minion, <c>false</c> otherwise.</returns>
            private bool ShouldWait() {
                return ObjectManager.Get<Obj_AI_Minion>().Any(minion =>
                    minion.IsValidTarget()
                    && minion.Team != GameObjectTeam.Neutral
                    && this.InAutoAttackRange(minion)
                    && MinionManager.IsMinion(minion)
                    && HealthPrediction.LaneClearHealthPrediction(minion,
                        (int)(Player.AttackDelay
                              * 1000
                              * LaneClearWaitTimeMod),
                        FarmDelay) <=
                    Player.GetAutoAttackDamage(minion));
            }

            /// <summary>
            ///     Returns if a minion should be attacked
            /// </summary>
            /// <param name="minion">The <see cref="Obj_AI_Minion" /></param>
            /// <param name="includeBarrel">Include Gangplank Barrel</param>
            /// <returns><c>true</c> if the minion should be attacked; otherwise, <c>false</c>.</returns>
            private bool ShouldAttackMinion(Obj_AI_Minion minion) {
                if(minion.Name == "WardCorpse" || minion.CharData.BaseSkinName == "jarvanivstandard") {
                    return false;
                }

                if(MinionManager.IsWard(minion)) {
                    return config.Item("AttackWards").IsActive();
                }

                return (config.Item("AttackPetsnTraps").GetValue<bool>()
                        || MinionManager.IsMinion(minion))
                       && minion.CharData.BaseSkinName != "gangplankbarrel";
            }

            private bool ShouldWaitUnderTurret(Obj_AI_Minion noneKillableMinion) {
                return ObjectManager.Get<Obj_AI_Minion>().Any(minion => (noneKillableMinion == null || noneKillableMinion.NetworkId != minion.NetworkId)
                                                                        && minion.IsValidTarget()
                                                                        && minion.Team != GameObjectTeam.Neutral
                                                                        && this.InAutoAttackRange(minion)
                                                                        && MinionManager.IsMinion(minion,false)
                                                                        && HealthPrediction.LaneClearHealthPrediction(minion,
                                                                            (int)(this.Player.AttackDelay * 1000 + (this.Player.IsMelee
                                                                                ? this.Player.AttackCastDelay * 1000
                                                                                : this.Player.AttackCastDelay * 1000
                                                                                  + 1000 * (this.Player.AttackRange + 2 * this.Player.BoundingRadius)
                                                                                  / this.Player.BasicAttack.MissileSpeed)),this.FarmDelay) <= this.Player.GetAutoAttackDamage(minion));
            }

            /// <summary>
            /// Gets the target.
            /// </summary>
            /// <returns>AttackableUnit.</returns>
            public virtual AttackableUnit GetTarget() {
                AttackableUnit result = null;
                var mode = this.ActiveMode;

                if((mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LaneClear)
                   && !config.Item("PriorizeFarm").GetValue<bool>()) {
                    var target = TargetSelector.GetTarget(-1,TargetSelector.DamageType.Physical);
                    if(target != null && this.InAutoAttackRange(target)) {
                        return target;
                    }
                }

                //GankPlank barrels
                var attackGankPlankBarrels = config.Item("AttackGPBarrel").GetValue<StringList>().SelectedIndex;
                if(attackGankPlankBarrels != 2
                   && (attackGankPlankBarrels == 0
                       || (mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed
                           || mode == OrbwalkingMode.LastHit || mode == OrbwalkingMode.Mixed))) {
                    var enemyGangPlank =
                        HeroManager.Enemies.FirstOrDefault(
                            e => e.ChampionName.Equals("gangplank",StringComparison.InvariantCultureIgnoreCase));

                    if(enemyGangPlank != null) {
                        var barrels =
                            ObjectManager.Get<Obj_AI_Minion>()
                                .Where(
                                    minion =>
                                        minion.Team == GameObjectTeam.Neutral
                                        && minion.CharData.BaseSkinName == "gangplankbarrel" && minion.IsHPBarRendered
                                        && minion.IsValidTarget() && this.InAutoAttackRange(minion));

                        foreach(var barrel in barrels) {
                            if(barrel.Health <= 1f) {
                                return barrel;
                            }

                            var t = (int)(this.Player.AttackCastDelay * 1000) + Game.Ping / 2
                                    + 1000 * (int)Math.Max(0,this.Player.Distance(barrel) - this.Player.BoundingRadius)
                                    / (int)Player.BasicAttack.MissileSpeed;

                            var barrelBuff =
                                barrel.Buffs.FirstOrDefault(
                                    b =>
                                        b.Name.Equals("gangplankebarrelactive",StringComparison.InvariantCultureIgnoreCase));

                            if(barrelBuff != null && barrel.Health <= 2f) {
                                var healthDecayRate = enemyGangPlank.Level >= 13
                                    ? 0.5f
                                    : (enemyGangPlank.Level >= 7 ? 1f : 2f);
                                var nextHealthDecayTime = Game.Time < barrelBuff.StartTime + healthDecayRate
                                    ? barrelBuff.StartTime + healthDecayRate
                                    : barrelBuff.StartTime + healthDecayRate * 2;

                                if(nextHealthDecayTime <= Game.Time + t / 1000f) {
                                    return barrel;
                                }
                            }
                        }

                        if(barrels.Any()) {
                            return null;
                        }
                    }
                }

                /*Killable Minion*/
                if(mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit
                   || mode == OrbwalkingMode.Mixed) {
                    var MinionList =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(minion => minion.IsValidTarget() && this.InAutoAttackRange(minion))
                            .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                            .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                            .ThenBy(minion => minion.Health)
                            .ThenByDescending(minion => minion.MaxHealth);

                    foreach(var minion in MinionList) {
                        var t = (int)(this.Player.AttackCastDelay * 1000) - 100 + Game.Ping / 2
                                + 1000 * (int)Math.Max(0,this.Player.Distance(minion) - this.Player.BoundingRadius)
                                / int.MaxValue;
                        //  / (int)Player.BasicAttack.MissileSpeed;

                        var predHealth = HealthPrediction.GetHealthPrediction(minion,t,this.FarmDelay);

                        if(minion.Team == GameObjectTeam.Neutral || !this.ShouldAttackMinion(minion)) {
                            continue;
                        }

                        var damage = this.Player.GetAutoAttackDamage(minion,true);
                        var killable = predHealth <= damage;

                        if(predHealth <= 0) {
                            FireOnNonKillableMinion(minion);
                        }

                        if(killable) {
                            return minion;
                        }
                    }
                }

                //Forced target
                if(this.forcedTarget.IsValidTarget() && this.InAutoAttackRange(this.forcedTarget)) {
                    return this.forcedTarget;
                }

                /* turrets / inhibitors / nexus */
                if(mode == OrbwalkingMode.LaneClear
                   && (!config.Item("FocusMinionsOverTurrets").GetValue<KeyBind>().Active
                       || !MinionManager.GetMinions(
                           ObjectManager.Player.Position,
                           GetRealAutoAttackRange(ObjectManager.Player)).Any())) {
                    /* turrets */
                    foreach(var turret in
                        ObjectManager.Get<Obj_AI_Turret>().Where(t => t.IsValidTarget() && this.InAutoAttackRange(t))) {
                        return turret;
                    }

                    /* inhibitor */
                    foreach(var turret in
                        ObjectManager.Get<Obj_BarracksDampener>()
                            .Where(t => t.IsValidTarget() && this.InAutoAttackRange(t))) {
                        return turret;
                    }

                    /* nexus */
                    foreach(var nexus in
                        ObjectManager.Get<Obj_HQ>().Where(t => t.IsValidTarget() && this.InAutoAttackRange(t))) {
                        return nexus;
                    }
                }

                /*Champions*/
                if(mode != OrbwalkingMode.LastHit) {
                    if(mode != OrbwalkingMode.LaneClear || !this.ShouldWait()) {
                        var target = TargetSelector.GetTarget(-1,TargetSelector.DamageType.Physical);
                        if(target.IsValidTarget() && this.InAutoAttackRange(target)) {
                            return target;
                        }
                    }
                }

                /*Jungle minions*/
                if(mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed) {
                    var jminions =
                        ObjectManager.Get<Obj_AI_Minion>()
                            .Where(
                                mob =>
                                    mob.IsValidTarget() && mob.Team == GameObjectTeam.Neutral && this.InAutoAttackRange(mob)
                                    && mob.CharData.BaseSkinName != "gangplankbarrel" && mob.Name != "WardCorpse");

                    result = config.Item("Smallminionsprio").GetValue<bool>()
                        ? jminions.MinOrDefault(mob => mob.MaxHealth)
                        : jminions.MaxOrDefault(mob => mob.MaxHealth);

                    if(result != null) {
                        return result;
                    }
                }

                /* UnderTurret Farming */
                if(mode == OrbwalkingMode.LaneClear || mode == OrbwalkingMode.Mixed || mode == OrbwalkingMode.LastHit
                   || mode == OrbwalkingMode.Mixed) {
                    var closestTower =
                        ObjectManager.Get<Obj_AI_Turret>()
                            .MinOrDefault(t => t.IsAlly && !t.IsDead ? this.Player.Distance(t,true) : float.MaxValue);

                    if(closestTower != null && this.Player.Distance(closestTower,true) < 1500 * 1500) {
                        Obj_AI_Minion farmUnderTurretMinion = null;
                        Obj_AI_Minion noneKillableMinion = null;
                        // return all the minions underturret in auto attack range
                        var minions =
                            MinionManager.GetMinions(this.Player.Position,this.Player.AttackRange + 200)
                                .Where(
                                    minion =>
                                        this.InAutoAttackRange(minion) && closestTower.Distance(minion,true) < 900 * 900)
                                .OrderByDescending(minion => minion.CharData.BaseSkinName.Contains("Siege"))
                                .ThenBy(minion => minion.CharData.BaseSkinName.Contains("Super"))
                                .ThenByDescending(minion => minion.MaxHealth)
                                .ThenByDescending(minion => minion.Health);
                        if(minions.Any()) {
                            // get the turret aggro minion
                            var turretMinion =
                                minions.FirstOrDefault(
                                    minion =>
                                        minion is Obj_AI_Minion && HealthPrediction.HasTurretAggro((Obj_AI_Minion)minion));

                            if(turretMinion != null) {
                                var hpLeftBeforeDie = 0;
                                var hpLeft = 0;
                                var turretAttackCount = 0;
                                var turretStarTick = HealthPrediction.TurretAggroStartTick(
                                    turretMinion as Obj_AI_Minion);
                                // from healthprediction (don't blame me :S)
                                var turretLandTick = turretStarTick + (int)(closestTower.AttackCastDelay * 1000)
                                                     + 1000
                                                     * Math.Max(
                                                         0,
                                                         (int)
                                                             (turretMinion.Distance(closestTower)
                                                              - closestTower.BoundingRadius))
                                                     / (int)(closestTower.BasicAttack.MissileSpeed + 70);
                                // calculate the HP before try to balance it
                                for(float i = turretLandTick + 50;
                                    i < turretLandTick + 10 * closestTower.AttackDelay * 1000 + 50;
                                    i = i + closestTower.AttackDelay * 1000) {
                                    var time = (int)i - Utils.GameTimeTickCount + Game.Ping / 2;
                                    var predHP =
                                        (int)
                                            HealthPrediction.LaneClearHealthPrediction(turretMinion,time > 0 ? time : 0);
                                    if(predHP > 0) {
                                        hpLeft = predHP;
                                        turretAttackCount += 1;
                                        continue;
                                    }
                                    hpLeftBeforeDie = hpLeft;
                                    hpLeft = 0;
                                    break;
                                }
                                // calculate the hits is needed and possibilty to balance
                                if(hpLeft == 0 && turretAttackCount != 0 && hpLeftBeforeDie != 0) {
                                    var damage = (int)this.Player.GetAutoAttackDamage(turretMinion,true);
                                    var hits = hpLeftBeforeDie / damage;

                                    var timeBeforeDie = turretLandTick
                                                        + (turretAttackCount + 1)
                                                        * (int)(closestTower.AttackDelay * 1000)
                                                        - Utils.GameTimeTickCount;
                                    var timeUntilAttackReady = LastAATick + (int)(this.Player.AttackDelay * 1000)
                                                               > Utils.GameTimeTickCount + Game.Ping / 2 + 15
                                        ? LastAATick + (int)(this.Player.AttackDelay * 1000)
                                          - (Utils.GameTimeTickCount + Game.Ping / 2 + 15)
                                        : 0;

                                    var timeToLandAttack = this.Player.IsMelee
                                        ? this.Player.AttackCastDelay * 1000
                                        : this.Player.AttackCastDelay * 1000
                                          + 1000
                                          * Math.Max(
                                              0,
                                              turretMinion.Distance(this.Player)
                                              - this.Player.BoundingRadius)
                                          / this.Player.BasicAttack.MissileSpeed;
                                    if(hits >= 1
                                       && hits * this.Player.AttackDelay * 1000 + timeUntilAttackReady
                                       + timeToLandAttack < timeBeforeDie) {
                                        farmUnderTurretMinion = turretMinion as Obj_AI_Minion;
                                    } else if(hits >= 1
                                              && hits * this.Player.AttackDelay * 1000 + timeUntilAttackReady
                                              + timeToLandAttack > timeBeforeDie) {
                                        noneKillableMinion = turretMinion as Obj_AI_Minion;
                                    }
                                } else if(hpLeft == 0 && turretAttackCount == 0 && hpLeftBeforeDie == 0) {
                                    noneKillableMinion = turretMinion as Obj_AI_Minion;
                                }

                                // should wait before attacking a minion.
                                if(this.ShouldWaitUnderTurret(noneKillableMinion)) {
                                    return null;
                                }

                                if(farmUnderTurretMinion != null) {
                                    return farmUnderTurretMinion;
                                }

                                // balance other minions
                                foreach(var minion in minions.Where(x =>
                                    x.NetworkId != turretMinion.NetworkId
                                    && x is Obj_AI_Minion
                                    && !HealthPrediction.HasMinionAggro((Obj_AI_Minion)x))) {
                                    var playerDamage = (int)this.Player.GetAutoAttackDamage(minion);
                                    var turretDamage = (int)closestTower.GetAutoAttackDamage(minion,true);
                                    var leftHP = (int)minion.Health % turretDamage;

                                    if(leftHP > playerDamage) {
                                        return minion;
                                    }
                                }

                                // late game
                                var lastminion =
                                    minions.LastOrDefault(
                                        x =>
                                            x.NetworkId != turretMinion.NetworkId && x is Obj_AI_Minion
                                            && !HealthPrediction.HasMinionAggro((Obj_AI_Minion)x));

                                if(lastminion == null || minions.Count() < 2) {
                                    return null;
                                }

                                if(1f / this.Player.AttackDelay >= 1f
                                   && (int)(turretAttackCount * closestTower.AttackDelay / this.Player.AttackDelay)
                                   * this.Player.GetAutoAttackDamage(lastminion) > lastminion.Health) {
                                    return lastminion;
                                }

                                if(minions.Count() >= 5 && 1f / this.Player.AttackDelay >= 1.2) {
                                    return lastminion;
                                }
                            } else {
                                if(this.ShouldWaitUnderTurret(null)) {
                                    return null;
                                }

                                // balance other minions
                                foreach(var minion in minions.Where(x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro((Obj_AI_Minion)x))) {

                                    var playerDamage = (int)this.Player.GetAutoAttackDamage(minion);
                                    var turretDamage = (int)closestTower.GetAutoAttackDamage(minion,true);
                                    var leftHP = (int)minion.Health % turretDamage;

                                    if(leftHP > playerDamage) {
                                        return minion;
                                    }
                                }

                                //late game
                                var lastminion = minions.LastOrDefault(x => x is Obj_AI_Minion && !HealthPrediction.HasMinionAggro((Obj_AI_Minion)x));

                                if(lastminion == null || minions.Count() < 2) {
                                    return null;
                                }

                                if(minions.Count() >= 5 && 1f / this.Player.AttackDelay >= 1.2) {
                                    return lastminion;
                                }
                            }
                            return null;
                        }
                    }
                }

                /*Lane Clear minions*/
                if(mode == OrbwalkingMode.LaneClear) {
                    if(this.ShouldWait()) {
                        return null;
                    }

                    if(this._prevMinion.IsValidTarget() && this.InAutoAttackRange(this._prevMinion)) {
                        var predHealth = HealthPrediction.LaneClearHealthPrediction(this._prevMinion,
                            (int)(this.Player.AttackDelay * 1000 * LaneClearWaitTimeMod),
                            this.FarmDelay);

                        if(predHealth >= 2 * this.Player.GetAutoAttackDamage(this._prevMinion)
                           || Math.Abs(predHealth - this._prevMinion.Health) < float.Epsilon) {
                            return this._prevMinion;
                        }
                    }

                    var results = (from minion in ObjectManager.Get<Obj_AI_Minion>().Where(
                        minion =>
                            minion.IsValidTarget()
                            && this.InAutoAttackRange(minion)
                            && this.ShouldAttackMinion(minion))
                        let predHealth = HealthPrediction.LaneClearHealthPrediction(
                            minion,
                            (int)(this.Player.AttackDelay * 1000 * LaneClearWaitTimeMod),
                            this.FarmDelay)
                        where
                            predHealth >= 2 * this.Player.GetAutoAttackDamage(minion)
                            || Math.Abs(predHealth - minion.Health) < float.Epsilon
                        select minion);

                    var objAiMinions = results as Obj_AI_Minion[] ?? results.ToArray();
                    result = objAiMinions.MaxOrDefault(m => !MinionManager.IsMinion(m,true) ? float.MaxValue : m.Health);

                    if(config.Item("PrioritizeCasters").GetValue<bool>()) {
                        result = objAiMinions.OrderByDescending(
                            m =>
                                m.CharData.BaseSkinName.Contains("Ranged"))
                            .FirstOrDefault();
                    }

                    if(result != null) {
                        this._prevMinion = (Obj_AI_Minion)result;
                    }
                }
                return result;
            }

            /// <summary>
            /// Fired when the game is updated.
            /// </summary>
            /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
            private void GameOnOnGameUpdate(EventArgs args) {
                try {
                    if(ActiveMode == OrbwalkingMode.None) {
                        return;
                    }

                    //Prevent canceling important spells
                    if(Player.IsCastingInterruptableSpell(true)) {
                        return;
                    }

                    var target = this.GetTarget();

                    Orbwalk(target,
                        this.orbwalkingPoint.To2D().IsValid()
                            ? this.orbwalkingPoint : Game.CursorPos,
                        config.Item("ExtraWindup").GetValue<Slider>().Value,
                        Math.Max(config.Item("HoldPosRadius").GetValue<Slider>().Value,30));
                } catch(Exception e) {
                    Console.WriteLine(e);
                }
            }

            /// <summary>
            /// Fired when the game is drawn.
            /// </summary>
            /// <param name="args">The <see cref="EventArgs"/> instance containing the event data.</param>
            private void DrawingOnOnDraw(EventArgs args) {
                if(config.Item("AACircle").GetValue<Circle>().Active) {
                    Render.Circle.DrawCircle(Player.Position,GetRealAutoAttackRange(null) + 65,
                        config.Item("AACircle").GetValue<Circle>().Color,
                        config.Item("AALineWidth").GetValue<Slider>().Value);
                }

                if(config.Item("AACircle2").GetValue<Circle>().Active) {
                    foreach(var target in HeroManager.Enemies.FindAll(target => target.IsValidTarget(1175))) {
                        Render.Circle.DrawCircle(target.Position,GetAttackRange(target),
                            config.Item("AACircle2").GetValue<Circle>().Color,
                            config.Item("AALineWidth").GetValue<Slider>().Value);
                    }
                }

                if(config.Item("HoldZone").GetValue<Circle>().Active) {
                    Render.Circle.DrawCircle(
                        Player.Position,config.Item("HoldPosRadius").GetValue<Slider>().Value,
                        config.Item("HoldZone").GetValue<Circle>().Color,
                        config.Item("AALineWidth").GetValue<Slider>().Value,true);
                }
                config.Item("FocusMinionsOverTurrets").Permashow(config.Item("FocusMinionsOverTurrets").GetValue<KeyBind>().Active);
            }

            /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
            public void Dispose() {
                Menu.Remove(config);
                Game.OnUpdate -= this.GameOnOnGameUpdate;
                Drawing.OnDraw -= this.DrawingOnOnDraw;
                Instances.Remove(this);
            }
        }
    }

}

#endregion